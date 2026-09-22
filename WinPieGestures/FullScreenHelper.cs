using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures;

public static class FullScreenHelper
{
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct MONITORINFO
	{
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	private const uint MONITOR_DEFAULTTONEAREST = 2u;
	private const int GWL_EXSTYLE = -20;
	private const int GWL_STYLE = -16;
	private const int WS_CAPTION = 0x00C00000;
	private const int WS_EX_LAYERED = 0x00080000;
	private const int WS_EX_TRANSPARENT = 0x00000020;
	private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern nint GetShellWindow();

	[DllImport("user32.dll")]
	private static extern nint GetDesktopWindow();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
	private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int GetWindowLong(nint hWnd, int nIndex);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsZoomed(nint hWnd);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint hWnd, int attribute,
		out RECT value, int valueSize);

	private static nint _cachedHwnd = IntPtr.Zero;
	private static bool _cachedFullScreen = false;
	private static long _cachedTick = 0;
	private static readonly object _lock = new object();

	public static bool IsActiveWindowFullScreen(nint knownForegroundWindow = 0, string? knownProcessName = null)
	{
		nint foregroundWindow = knownForegroundWindow != IntPtr.Zero ? knownForegroundWindow : GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return false;
		}

		long now = Environment.TickCount64;
		lock (_lock)
		{
			if (foregroundWindow == _cachedHwnd && (now - _cachedTick) < 150)
			{
				return _cachedFullScreen;
			}
		}

		if (foregroundWindow == GetShellWindow() || foregroundWindow == GetDesktopWindow())
		{
			lock (_lock) { _cachedHwnd = foregroundWindow; _cachedFullScreen = false; _cachedTick = now; }
			return false;
		}

		StringBuilder sbClass = new StringBuilder(256);
		GetClassName(foregroundWindow, sbClass, 256);
		string className = sbClass.ToString();

		if (string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "SHELLDLL_DefView", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "SysListView32", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "ScreenClippingHost", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "SnippingTool", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "SnippingToolHost", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "Snipaste", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "PixPin", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "ScreenCaptureWnd", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "CChatRoomScreenCaptureWnd", StringComparison.OrdinalIgnoreCase))
		{
			lock (_lock) { _cachedHwnd = foregroundWindow; _cachedFullScreen = false; _cachedTick = now; }
			return false;
		}

		string activeProc = knownProcessName ?? ActiveWindowHelper.GetActiveWindowProcessName();
		if (string.Equals(activeProc, "explorer.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "screenclippinghost.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "snippingtool.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "snippingtoolhost.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "snipaste.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "pixpin.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "sharex.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "flameshot.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "lightshot.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "shellexperiencehost.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "startmenuexperiencehost.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "searchhost.exe", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(activeProc, "textinputhost.exe", StringComparison.OrdinalIgnoreCase))
		{
			lock (_lock) { _cachedHwnd = foregroundWindow; _cachedFullScreen = false; _cachedTick = now; }
			return false;
		}

		if (!GetWindowRect(foregroundWindow, out var lpRect))
		{
			lock (_lock) { _cachedHwnd = foregroundWindow; _cachedFullScreen = false; _cachedTick = now; }
			return false;
		}
		nint hMonitor = MonitorFromWindow(foregroundWindow, 2u);
		if (hMonitor == IntPtr.Zero)
		{
			lock (_lock) { _cachedHwnd = foregroundWindow; _cachedFullScreen = false; _cachedTick = now; }
			return false;
		}
		MONITORINFO lpmi = default(MONITORINFO);
		lpmi.cbSize = Marshal.SizeOf(lpmi);
		if (!GetMonitorInfo(hMonitor, ref lpmi))
		{
			lock (_lock) { _cachedHwnd = foregroundWindow; _cachedFullScreen = false; _cachedTick = now; }
			return false;
		}

		bool isFs = false;
		if (lpRect.Left <= lpmi.rcMonitor.Left && lpRect.Top <= lpmi.rcMonitor.Top && lpRect.Right >= lpmi.rcMonitor.Right && lpRect.Bottom >= lpmi.rcMonitor.Bottom)
		{
			// 检查是否为透明/分层窗口 (如截屏工具遮罩、透明悬浮窗、HUD等)，这类窗口绝非独占全屏游戏
			int exStyle = GetWindowLong(foregroundWindow, GWL_EXSTYLE);
			if ((exStyle & WS_EX_LAYERED) == 0 && (exStyle & WS_EX_TRANSPARENT) == 0)
			{
				// GetWindowRect includes invisible resize borders. A normally maximized
				// window can therefore cover the monitor on paper while its visible
				// frame ends at the taskbar work area. True fullscreen extends past it.
				bool workAreaSmaller = lpmi.rcWork.Left > lpmi.rcMonitor.Left ||
					lpmi.rcWork.Top > lpmi.rcMonitor.Top ||
					lpmi.rcWork.Right < lpmi.rcMonitor.Right ||
					lpmi.rcWork.Bottom < lpmi.rcMonitor.Bottom;
				bool visibleFitsWorkArea = false;
				bool normalMaximizedBorder = false;
				if (DwmGetWindowAttribute(foregroundWindow,
					DWMWA_EXTENDED_FRAME_BOUNDS, out RECT visible, Marshal.SizeOf<RECT>()) >= 0)
				{
					const int tolerance = 2;
					visibleFitsWorkArea = workAreaSmaller && visible.Right > visible.Left && visible.Bottom > visible.Top &&
						visible.Left >= lpmi.rcWork.Left - tolerance &&
						visible.Top >= lpmi.rcWork.Top - tolerance &&
						visible.Right <= lpmi.rcWork.Right + tolerance &&
						visible.Bottom <= lpmi.rcWork.Bottom + tolerance;

					// With an auto-hidden taskbar, rcWork equals rcMonitor. In that
					// layout a maximized framed window still has a small invisible
					// resize border outside the monitor; F11 borderless Chrome does not.
					int leftBorder = lpmi.rcMonitor.Left - lpRect.Left;
					int topBorder = lpmi.rcMonitor.Top - lpRect.Top;
					int rightBorder = lpRect.Right - lpmi.rcMonitor.Right;
					int bottomBorder = lpRect.Bottom - lpmi.rcMonitor.Bottom;
					bool smallOuterBorder = leftBorder is >= 0 and <= 48 &&
						topBorder is >= 0 and <= 48 && rightBorder is >= 0 and <= 48 &&
						bottomBorder is >= 0 and <= 48 &&
						(leftBorder + topBorder + rightBorder + bottomBorder) > 0;
					normalMaximizedBorder = IsZoomed(foregroundWindow) &&
						(GetWindowLong(foregroundWindow, GWL_STYLE) & WS_CAPTION) == WS_CAPTION &&
						smallOuterBorder && visible.Left <= lpmi.rcMonitor.Left + tolerance &&
						visible.Top <= lpmi.rcMonitor.Top + tolerance &&
						visible.Right >= lpmi.rcMonitor.Right - tolerance &&
						visible.Bottom >= lpmi.rcMonitor.Bottom - tolerance;
				}
				isFs = !visibleFitsWorkArea && !normalMaximizedBorder;
			}
		}

		lock (_lock)
		{
			_cachedHwnd = foregroundWindow;
			_cachedFullScreen = isFs;
			_cachedTick = now;
		}
		return isFs;
	}
}
