using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using StarPie.Plugin;
using WinPieGestures.Insight;
using Clipboard = System.Windows.Clipboard;

namespace WinPieGestures.Plugins;

internal sealed class PluginContentInsightService(string pluginId, PluginCapability capabilities)
    : IHostContentInsightService
{
    private readonly object _sync = new();
    private CancellationTokenSource? _active;
    internal void Require(PluginCapability capability)
    {
        if ((capabilities & capability) != capability)
            throw new PluginCapabilityDeniedException(capability, nameof(IHostContentInsightService), pluginId);
    }

    public async Task RunAsync(InsightSessionRequest request, Func<string, InsightAnalysis> classify,
        CancellationToken cancellationToken)
    {
        Require(PluginCapability.Ui);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(classify);
        ValidateSearchTemplate(request.SearchUrlTemplate);
        if (request.Source == InsightSource.Screen) Require(PluginCapability.ScreenCapture);
        if (request.Source == InsightSource.Clipboard) Require(PluginCapability.Clipboard);
        using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_sync) { _active?.Cancel(); _active = session; }
        InsightWindow? window = null;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            string text = "";
            string notice = "";
            if (request.Source == InsightSource.Selection)
            {
                var read = await InsightContentReader.ReadSelectionAsync((nint)request.Context.ForegroundWindowHandle, session.Token);
                text = read.Text;
                if (text.Length == 0 && !read.IsPassword && request.AllowCopyFallback)
                {
                    Require(PluginCapability.InputSimulation);
                    Require(PluginCapability.Clipboard);
                    text = await InsightContentReader.CopySelectionAsync((nint)request.Context.ForegroundWindowHandle, session.Token);
                }
                if (text.Length == 0) notice = InsightText.T("NoSelection");
            }
            else if (request.Source == InsightSource.Clipboard)
            {
                text = await ReadClipboardAsync();
                if (text.Length == 0) notice = InsightText.T("EmptyClipboard");
            }
            if (text.Length > 8192) { text = ""; notice = InsightText.T("TooLong"); }
            session.Token.ThrowIfCancellationRequested();
            InsightAnalysis analysis = classify(text);
            // Only an explicitly selected, entire URL can navigate without a card.
            if (request.Source == InsightSource.Selection && request.OpenSelectedUrlImmediately &&
                analysis.Candidates.Count > 0 && analysis.Candidates[0].Operation == InsightOperation.OpenUrl &&
                IsCompleteUrl(text, analysis.Candidates[0].Value))
            {
                if (Execute(analysis.Candidates[0], request.SearchUrlTemplate)) return;
                notice = InsightText.T("Failed");
            }
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (session.IsCancellationRequested) return;
                window = new InsightWindow(this, request, classify, session.Token);
                window.Closed += (_, _) => closed.TrySetResult();
                window.SetContent(text, request.Source, notice);
                window.Show();
            });
            if (window == null) return;
            if (request.Source == InsightSource.Screen)
                await Application.Current.Dispatcher.InvokeAsync(() => window.StartScreenCapture());
            await closed.Task.WaitAsync(session.Token);
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
        finally
        {
            if (window != null && !Application.Current.Dispatcher.HasShutdownStarted)
                await Application.Current.Dispatcher.InvokeAsync(() => window.Close());
            lock (_sync) { if (ReferenceEquals(_active, session)) _active = null; }
        }
    }

    internal async Task<string> ReadClipboardAsync()
    {
        Require(PluginCapability.Clipboard);
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : ""; }
            catch (ExternalException ex)
            {
                AppLogger.LogWarn($"[plugin:{pluginId}] Clipboard unavailable: {ex.Message}");
                return ""; // A busy clipboard is an environmental condition, not a plugin failure.
            }
        });
    }

    internal static void ValidateSearchTemplate(string template)
    {
        if (template.Length > 2048 || template.Split("{query}", StringSplitOptions.None).Length != 2 ||
            !IsWebUrl(template.Replace("{query}", "test", StringComparison.Ordinal)))
            throw new ArgumentException("Search template must contain one {query} and use HTTP(S).");
    }
    internal static bool IsWebUrl(string value) => !value.Any(char.IsWhiteSpace) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https") &&
        uri.Host.Length > 0 && uri.UserInfo.Length == 0;
    private static bool IsCompleteUrl(string input, string candidate)
    {
        string value = input.Trim();
        if (!value.Contains("://", StringComparison.Ordinal)) value = "https://" + value;
        return IsWebUrl(value) && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.AbsoluteUri == candidate;
    }

    internal bool Execute(InsightCandidate candidate, string searchTemplate, CancellationToken cancellation = default)
    {
        // Capability checks precede exception-to-status conversion, just like other new services.
        Require(candidate.Operation == InsightOperation.Copy ? PluginCapability.Clipboard : PluginCapability.Process);
        if (candidate.Operation is InsightOperation.OpenPath or InsightOperation.LocatePath) Require(PluginCapability.FileSystem);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            switch (candidate.Operation)
            {
                case InsightOperation.Copy:
                    if (candidate.Value.Length > 8192) return false;
                    Clipboard.SetText(candidate.Value);
                    return true;
                case InsightOperation.Search:
                    ValidateSearchTemplate(searchTemplate);
                    string searchUrl = searchTemplate.Replace("{query}", Uri.EscapeDataString(candidate.Value), StringComparison.Ordinal);
                    return Open(searchUrl);
                case InsightOperation.OpenUrl:
                    return IsWebUrl(candidate.Value) && Open(candidate.Value);
                case InsightOperation.OpenPath:
                case InsightOperation.LocatePath:
                    string path = candidate.Value;
                    if (!Path.IsPathFullyQualified(path) || (!File.Exists(path) && !Directory.Exists(path))) return false;
                    cancellation.ThrowIfCancellationRequested();
                    if (candidate.Operation == InsightOperation.OpenPath)
                        return Open(path);
                    // Absolute path validated above; launch through host, never build a shell command.
                    return Open("explorer.exe", "/select,\"" + path + "\"");
                default: return false;
            }
        }
        catch (Exception ex) { AppLogger.LogWarn($"[plugin:{pluginId}] Insight operation failed: {ex.Message}"); return false; }
    }

    // Keep result-card errors in the card. Legacy actions can show modal error windows
    // or toggle an existing application; a confirmed content operation should just open it.
    private static bool Open(string target, string arguments = "")
    {
        using var process = Process.Start(new ProcessStartInfo { FileName = target, Arguments = arguments, UseShellExecute = true });
        return true;
    }
}
