using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures;

public static class ActiveWindowHelper
{
	private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern bool QueryFullProcessImageName(nint hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(nint hObject);

	private static nint _cachedForegroundHwnd = IntPtr.Zero;
	private static string _cachedProcessName = "unknown.exe";
	private static long _cachedTick = 0;
	private static readonly object _cacheLock = new object();

	public static string GetActiveWindowProcessName()
	{
		return GetActiveWindowInfo(out _);
	}

	public static string GetActiveWindowInfo(out nint foregroundWindow)
	{
		foregroundWindow = IntPtr.Zero;
		try
		{
			foregroundWindow = GetForegroundWindow();
			if (foregroundWindow == IntPtr.Zero)
			{
				return "unknown.exe";
			}

			long now = Environment.TickCount64;
			lock (_cacheLock)
			{
				if (foregroundWindow == _cachedForegroundHwnd && (now - _cachedTick) < 150)
				{
					return _cachedProcessName;
				}
			}

			GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId);
			if (lpdwProcessId == 0)
			{
				return "unknown.exe";
			}

			nint hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, lpdwProcessId);
			if (hProcess != IntPtr.Zero)
			{
				try
				{
					uint size = 1024;
					StringBuilder sb = new StringBuilder((int)size);
					if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
					{
						string fullPath = sb.ToString();
						string fileName = Path.GetFileName(fullPath);
						if (!string.IsNullOrEmpty(fileName))
						{
							string procName = fileName.ToLowerInvariant();
							lock (_cacheLock)
							{
								_cachedForegroundHwnd = foregroundWindow;
								_cachedProcessName = procName;
								_cachedTick = Environment.TickCount64;
							}
							return procName;
						}
					}
				}
				finally
				{
					CloseHandle(hProcess);
				}
			}

			lock (_cacheLock)
			{
				_cachedForegroundHwnd = foregroundWindow;
				_cachedProcessName = "unknown.exe";
				_cachedTick = Environment.TickCount64;
			}
			return "unknown.exe";
		}
		catch
		{
			return "unknown.exe";
		}
	}
}
