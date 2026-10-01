using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using StarPie.Plugin;

namespace WinPieGestures.Insight;

internal static class InsightOrigin
{
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int length);
    internal static nint CaptureWindow() => GetForegroundWindow();
    internal static ActionContext Capture() => FromWindow(CaptureWindow(), new System.Windows.Point(
        System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y));
    internal static ActionContext FromWindow(nint hwnd, System.Windows.Point anchor)
    {
        string process = "";
        var title = new StringBuilder(512);
        try
        {
            GetWindowText(hwnd, title, title.Capacity);
            GetWindowThreadProcessId(hwnd, out uint pid);
            using var target = Process.GetProcessById((int)pid);
            process = target.ProcessName;
        }
        catch { }
        return new ActionContext { ForegroundWindowHandle = hwnd, ForegroundWindowTitle = title.ToString(),
            ForegroundProcessName = process, CursorX = (int)anchor.X, CursorY = (int)anchor.Y,
            LanguageCode = I18n.CurrentLanguageCode, IsElevated = ConfigManager.IsElevated() };
    }
}
