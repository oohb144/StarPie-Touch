using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using Clipboard = System.Windows.Clipboard;

namespace WinPieGestures.Insight;

internal readonly record struct SelectionRead(string Text, bool IsPassword = false);

internal static class InsightContentReader
{
    private static int _automationBusy;
    private static readonly SemaphoreSlim CopyGate = new(1, 1);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    internal static async Task<SelectionRead> ReadSelectionAsync(nint origin, CancellationToken cancellation)
    {
        if (!IsWindow(origin) || InsightOrigin.GetForegroundWindow() != origin ||
            Interlocked.CompareExchange(ref _automationBusy, 1, 0) != 0) return new("");
        // One outstanding UIA call at most. A slow external accessibility provider cannot spawn
        // an unbounded collection of abandoned calls or block the input/UI thread.
        var task = Task.Run(() =>
        {
            try
            {
                var focused = AutomationElement.FocusedElement;
                if (focused == null || !BelongsToWindow(focused, origin)) return new SelectionRead("");
                if (focused.Current.IsPassword) return new SelectionRead("", true);
                var element = focused;
                for (int i = 0; i < 8 && element != null; i++)
                {
                    if (element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern))
                    {
                        var selections = ((TextPattern)pattern).GetSelection();
                        var parts = new List<string>();
                        int remaining = 8193;
                        foreach (var range in selections)
                        {
                            if (remaining <= 0) break;
                            string part = range.GetText(remaining);
                            parts.Add(part);
                            remaining -= part.Length;
                        }
                        string text = string.Join("\n", parts);
                        if (text.Length > 8192) return new SelectionRead("");
                        if (!string.IsNullOrWhiteSpace(text)) return new SelectionRead(text);
                    }
                    if (element.Current.NativeWindowHandle == (int)origin) break;
                    element = TreeWalker.RawViewWalker.GetParent(element);
                }
            }
            catch { }
            finally { Volatile.Write(ref _automationBusy, 0); }
            return new SelectionRead("");
        });
        try { return await task.WaitAsync(TimeSpan.FromMilliseconds(700), cancellation).ConfigureAwait(false); }
        catch (TimeoutException) { return new("", true); } // Unknown password status: do not inject Ctrl+C.
    }

    private static bool BelongsToWindow(AutomationElement element, nint origin)
    {
        for (int i = 0; i < 32 && element != null; i++)
        {
            if (element.Current.NativeWindowHandle == (int)origin) return true;
            element = TreeWalker.RawViewWalker.GetParent(element);
        }
        return false;
    }

    internal static async Task<string> CopySelectionAsync(nint origin, CancellationToken cancellation)
    {
        await CopyGate.WaitAsync(cancellation).ConfigureAwait(false);
        System.Windows.IDataObject? previous = null;
        uint copiedSequence = 0;
        try
        {
            if (InsightOrigin.GetForegroundWindow() != origin) return "";
            uint before = 0;
            bool snapshotted = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                before = GetClipboardSequenceNumber();
                var snapshot = SnapshotClipboard(Clipboard.GetDataObject());
                previous = snapshot.Data;
                return snapshot.Success && GetClipboardSequenceNumber() == before;
            });
            if (!snapshotted) return "";
            bool sent = await ActionExecutor.RunSerializedInputAsync(() =>
            {
                if (cancellation.IsCancellationRequested || InsightOrigin.GetForegroundWindow() != origin ||
                    GetClipboardSequenceNumber() != before) return false;
                ActionExecutor.ExecuteHotkey("Ctrl+C");
                return true;
            }).ConfigureAwait(false);
            if (!sent) return "";
            for (int i = 0; i < 16; i++)
            {
                await Task.Delay(25, cancellation).ConfigureAwait(false);
                uint current = GetClipboardSequenceNumber();
                if (current == before) continue;
                GetWindowThreadProcessId(origin, out uint sourcePid);
                GetWindowThreadProcessId(GetClipboardOwner(), out uint clipboardPid);
                if (sourcePid == 0 || sourcePid != clipboardPid) return "";
                copiedSequence = current;
                string text = await Application.Current.Dispatcher.InvokeAsync(() => Clipboard.ContainsText() ? Clipboard.GetText() : "");
                // An unrelated app may have replaced the clipboard during the read.
                if (GetClipboardSequenceNumber() != copiedSequence || InsightOrigin.GetForegroundWindow() != origin) return "";
                return text.Length <= 8192 ? text : "";
            }
            return "";
        }
        catch (OperationCanceledException) { throw; }
        catch { return ""; }
        finally
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Restore only the sequence produced by this copy. Never overwrite a newer copy.
                    if (copiedSequence == 0 || GetClipboardSequenceNumber() != copiedSequence) return;
                    if (previous != null) Clipboard.SetDataObject(previous, true); else Clipboard.Clear();
                });
            }
            catch { }
            CopyGate.Release();
        }
    }

    // Materialize clipboard data before Ctrl+C. A lazy OLE IDataObject can become invalid
    // when its owner replaces the clipboard. Unknown or large formats skip the opt-in fallback.
    internal static (bool Success, System.Windows.IDataObject? Data) SnapshotClipboard(System.Windows.IDataObject? source)
    {
        if (source == null) return (true, null);
        try
        {
            string[] formats = source.GetFormats(false);
            if (formats.Length > 32) return (false, null);
            var snapshot = new System.Windows.DataObject();
            long budget = 4 * 1024 * 1024;
            foreach (string format in formats)
            {
                object? value = source.GetData(format, false);
                object? copy = value switch
                {
                    string text when (budget -= text.Length * 2L) >= 0 => text,
                    string[] paths when (budget -= paths.Sum(p => p.Length * 2L)) >= 0 => paths.ToArray(),
                    byte[] bytes when (budget -= bytes.Length) >= 0 => bytes.ToArray(),
                    int number => number,
                    MemoryStream stream when (budget -= stream.Length) >= 0 => new MemoryStream(stream.ToArray(), false),
                    _ => null
                };
                if (copy == null) return (false, null);
                snapshot.SetData(format, copy, false);
            }
            return (true, snapshot);
        }
        catch { return (false, null); }
    }
}
