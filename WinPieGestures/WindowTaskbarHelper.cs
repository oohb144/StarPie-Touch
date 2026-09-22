using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinPieGestures;

/// <summary>
/// 任务栏第 N 窗口：按"任务栏按钮"语义枚举当前桌面窗口、激活到前台、提取窗口图标。
/// 托盘驻留(隐藏/离屏)程序不参与计数，避免被误切换。
/// </summary>
public static class WindowTaskbarHelper
{
	// ---- 枚举与过滤 ----
	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	// ---- 任务栏工具栏（Win+N 槽位顺序）----
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint FindWindow(string lpClassName, string? lpWindowName);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string lpszClass, string? lpszWindow);

	private const uint TB_BUTTONCOUNT = 0x0418;
	private const uint TB_GETBUTTON = 0x0417;
	private const uint TB_GETIMAGELIST = 0x0419;
	private const uint TB_GETBUTTONINFO = 0x0441;
	private const uint TBIF_IMAGE = 0x00000002;
	private const uint ILD_TRANSPARENT = 0x00000001;

	[StructLayout(LayoutKind.Sequential)]
	private struct TBBUTTONINFO
	{
		public uint cbSize;
		public uint dwMask;
		public int idCommand;
		public int iImage;
		public byte fsState;
		public byte fsStyle;
		public ushort cx;
		public nint lParam;
		public nint pszText;
		public int cchText;
	}

	[DllImport("comctl32.dll")]
	private static extern nint ImageList_GetIcon(nint himl, int i, uint flags);

	[StructLayout(LayoutKind.Sequential)]
	private struct TBBUTTON
	{
		public int iBitmap;
		public int idCommand;
		public byte fsState;
		public byte fsStyle;
		public byte bReserved0;
		public byte bReserved1;
		public nint dwData;
		public nint iString;
	}

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint hWnd, uint uCmd);

	private const uint GW_OWNER = 4;

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

	private const int GWL_EXSTYLE = -20;
	private const long WS_EX_TOOLWINDOW = 0x00000080L;

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

	private const uint MONITOR_DEFAULTTONEAREST = 2;

	[DllImport("user32.dll")]
	private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MONITORINFO
	{
		public uint cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	// ---- 前台激活 ----
	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	private const int SW_RESTORE = 9;
	private const int SW_MINIMIZE = 6;
	private const byte VK_MENU = 0x12;
	private const uint KEYEVENTF_KEYUP = 0x0002;
	private static readonly nint HWND_TOPMOST = new nint(-1);
	private static readonly nint HWND_NOTOPMOST = new nint(-2);
	private const uint SWP_NOSIZE = 0x0001;
	private const uint SWP_NOMOVE = 0x0002;
	private const uint SWP_NOACTIVATE = 0x0010;
	private const uint SWP_SHOWWINDOW = 0x0040;

	[DllImport("user32.dll")]
	private static extern bool BringWindowToTop(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nint dwExtraInfo);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

	// ---- 图标提取 ----
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern nint SendMessageTimeout(nint hWnd, uint msg, nint wParam, nint lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

	private const uint SMTO_ABORTIFHUNG = 0x0002;

	private const uint WM_GETICON = 0x007F;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint GetClassLongPtr(nint hWnd, int nIndex);

	private const int GCLP_HICON = -14;

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(nint hIcon);

	private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern bool QueryFullProcessImageName(nint hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(nint hObject);

	// ---- 当前虚拟桌面过滤（Win10+；不可用则跳过）----
	[ComImport, Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
	private class VirtualDesktopManager
	{
	}

	[ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IVirtualDesktopManager
	{
		int IsWindowOnCurrentVirtualDesktop(nint topLevelWindow, out int onCurrentDesktop);

		int GetWindowDesktopId(nint topLevelWindow, out Guid desktopId);

		int MoveWindowToDesktop(nint topLevelWindow, ref Guid desktopId);
	}


	private static IVirtualDesktopManager? CreateVdm()
	{
		try
		{
			return (IVirtualDesktopManager)new VirtualDesktopManager();
		}
		catch
		{
			return null;
		}
	}

	/// <summary>当前进程 PID（轮盘/设置窗口自身不计入候选）。</summary>
	private static uint SelfPid => (uint)Environment.ProcessId;

	/// <summary>
	/// 按任务栏按钮语义枚举当前桌面的可切换窗口（z 序自上而下，即任务栏从左到右）。
	/// 过滤：可见、无 owner、非 WS_EX_TOOLWINDOW、排除 StarPie 自身、与所属显示器工作区相交（防离屏"托盘驻留"窗口）、当前虚拟桌面。
	/// </summary>
	public static List<nint> GetTaskbarWindows()
	{
		return GetTaskbarWindows(CreateVdm());
	}

	private static List<nint> GetTaskbarWindows(IVirtualDesktopManager? virtualDesktopManager)
	{
		List<nint> list = new List<nint>();
		uint selfPid = SelfPid;
		EnumWindows(delegate (nint hWnd, nint lParam)
		{
			try
			{
				if (!IsWindowVisible(hWnd))
				{
					return true;
				}
				if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
				{
					return true;
				}
				if ((GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0L)
				{
					return true;
				}
				GetWindowThreadProcessId(hWnd, out uint pid);
				if (pid == selfPid)
				{
					return true;
				}
				// 最小化窗口的矩形是任务栏处占位矩形，不参与"离屏"判定（其任务栏按钮必然存在）
				if (!IsIconic(hWnd) && GetWindowRect(hWnd, out RECT rc))
				{
					nint mon = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
					if (mon != IntPtr.Zero)
					{
						MONITORINFO mi = default;
						mi.cbSize = (uint)Marshal.SizeOf<MONITORINFO>();
						if (GetMonitorInfo(mon, ref mi))
						{
							bool intersects = rc.Left < mi.rcWork.Right && rc.Right > mi.rcWork.Left && rc.Top < mi.rcWork.Bottom && rc.Bottom > mi.rcWork.Top;
							if (!intersects)
							{
								return true;
							}
						}
					}
				}
				if (virtualDesktopManager != null)
				{
					int onCurrent = 0;
					if (virtualDesktopManager.IsWindowOnCurrentVirtualDesktop(hWnd, out onCurrent) == 0 && onCurrent == 0)
					{
						return true;
					}
				}
				list.Add(hWnd);
			}
			catch
			{
			}
			return true;
		}, IntPtr.Zero);
		return list;
	}

	/// <summary>找到任务栏应用按钮工具栏（Win10 经典任务栏）；Win11 XAML 任务栏返回 0。</summary>
	private static nint FindTaskbarToolbar()
	{
		try
		{
			nint taskbar = FindWindow("Shell_TrayWnd", null);
			if (taskbar == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			nint toolbar = FindWindowEx(taskbar, IntPtr.Zero, "MSTaskSwWClass", null);
			if (toolbar == IntPtr.Zero)
			{
				toolbar = FindWindowEx(taskbar, IntPtr.Zero, "MSTaskListWClass", null);
			}
			return toolbar;
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	/// <summary>
	/// 任务栏"应用槽位"（与 Win+N 计数一致）：按任务栏按钮顺序，只保留应用按钮——
	/// 运行窗口（idCommand=窗口句柄）与固定应用（未运行，idCommand=0 但 dwData 有应用标识）；
	/// 系统按钮（任务视图/搜索等 idCommand=0 且 dwData=0）被剔除。
	/// </summary>
	private static (nint toolbar, List<(int rawIndex, nint hwnd)> slots)? GetTaskbarAppSlots()
	{
		nint toolbar = FindTaskbarToolbar();
		if (toolbar == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			int count = (int)SendMessage(toolbar, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
			if (count <= 0)
			{
				return null;
			}
			List<(int, nint)> slots = new List<(int, nint)>(count);
			byte[] buf = new byte[Marshal.SizeOf<TBBUTTON>()];
			GCHandle pin = GCHandle.Alloc(buf, GCHandleType.Pinned);
			try
			{
				for (int i = 0; i < count; i++)
				{
					if (SendMessage(toolbar, TB_GETBUTTON, (nint)i, pin.AddrOfPinnedObject()).ToInt64() <= 0)
					{
						continue;
					}
					TBBUTTON btn = Marshal.PtrToStructure<TBBUTTON>(pin.AddrOfPinnedObject());
					// 应用按钮：运行窗口 idCommand=窗口句柄；固定应用 dwData 携带应用标识。系统按钮两者皆 0。
					if (btn.idCommand != 0 || btn.dwData != IntPtr.Zero)
					{
						slots.Add((i, (nint)btn.idCommand));
					}
				}
			}
			finally
			{
				pin.Free();
			}
			return slots.Count > 0 ? (toolbar, slots) : null;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// 任务栏从左到右的应用按钮标题（UI Automation，Win10/Win11 均可靠）。
	/// 树序 = 视觉顺序；系统按钮（开始/任务视图/托盘）不匹配候选窗口标题，对序无影响。
	/// </summary>
	private static List<string>? GetTaskbarButtonNames()
	{
		try
		{
			AutomationElement root = AutomationElement.RootElement;
			AutomationElement tray = root.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
			if (tray == null)
			{
				return null;
			}
			List<string> names = new List<string>();
			CollectTaskbarButtons(tray, names);
			return names;
		}
		catch
		{
			return null;
		}
	}

	private static void CollectTaskbarButtons(AutomationElement element, List<string> names)
	{
		try
		{
			AutomationElementCollection children = element.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition);
			foreach (AutomationElement child in children)
			{
				string name = child.Current.Name ?? "";
				if (!string.IsNullOrEmpty(name))
				{
					ControlType ct = child.Current.ControlType;
					if (ct == ControlType.Button || ct == ControlType.ListItem || ct == ControlType.TabItem)
					{
						names.Add(name);
					}
				}
				CollectTaskbarButtons(child, names);
			}
		}
		catch
		{
		}
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	private static string TitleOf(nint hWnd)
	{
		try
		{
			StringBuilder sb = new StringBuilder(512);
			GetWindowText(hWnd, sb, 512);
			return sb.ToString();
		}
		catch
		{
			return "";
		}
	}

	/// <summary>窗口是否在当前虚拟桌面（供平铺等全量枚举时过滤）。</summary>
	public static bool IsOnCurrentVirtualDesktop(nint hWnd)
	{
		try
		{
			if (hWnd == IntPtr.Zero)
			{
				return false;
			}
			IVirtualDesktopManager? vdm = CreateVdm();
			if (vdm == null)
			{
				// VDM 整体不可用（如系统组件缺失）：fail-open，交由上层过滤兜底，
				// 否则所有窗口会被静默丢弃导致平铺"看不见"任何窗口。
				LogVdmUnavailableOnce();
				return true;
			}
			// COM 调用返回错误码（提权窗口、部分系统窗口常见）≠ 不在当前桌面：
			// 仅当 HRESULT 成功时信任判定结果（BOOL 非 0 = 在当前桌面），失败则 fail-open，避免误丢真实可见窗口。
			int onCurrent = 0;
			if (vdm.IsWindowOnCurrentVirtualDesktop(hWnd, out onCurrent) == 0)
			{
				return onCurrent != 0;
			}
			LogVdmUnavailableOnce();
			return true;
		}
		catch
		{
			LogVdmUnavailableOnce();
			return true;
		}
	}

	private static bool s_vdmWarned;

	private static void LogVdmUnavailableOnce()
	{
		if (s_vdmWarned)
		{
			return;
		}
		s_vdmWarned = true;
		AppLogger.LogWarn("[VDM] IVirtualDesktopManager 不可用/调用失败，虚拟桌面过滤已放宽（fail-open）");
	}

	/// <summary>抗权限获取进程 exe 路径（OpenProcess+QueryFullProcessImageName）；失败返回 null。</summary>
	public static string? GetProcessImageNameByPid(uint pid)
	{
		try
		{
			nint hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
			if (hProcess == IntPtr.Zero)
			{
				return null;
			}
			try
			{
				uint size = 1024;
				StringBuilder sb = new StringBuilder((int)size);
				if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
				{
					return sb.ToString();
				}
			}
			finally
			{
				CloseHandle(hProcess);
			}
		}
		catch
		{
		}
		return null;
	}

	/// <summary>任务栏第 n 个运行窗口的窗口标题（无则返回 null）；供轮盘中心显示"将要激活的窗口"。</summary>
	public static string? GetNthTaskbarWindowTitle(int n)
	{
		nint hWnd = GetNthTaskbarWindow(n);
		if (hWnd == IntPtr.Zero)
		{
			return null;
		}
		string title = TitleOf(hWnd);
		return string.IsNullOrWhiteSpace(title) ? null : title;
	}

	private static bool NameMatchesWindow(string name, string title)
	{
		if (string.IsNullOrEmpty(title))
		{
			return false;
		}
		if (string.Equals(name, title, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (title.StartsWith(name, StringComparison.OrdinalIgnoreCase) && name.Length >= 3)
		{
			return true;
		}
		if (name.StartsWith(title, StringComparison.OrdinalIgnoreCase) && title.Length >= 3)
		{
			return true;
		}
		return false;
	}

	/// <summary>候选运行窗口，按任务栏可见顺序排列（带 TTL 快照缓存）。
	/// 非阻塞刷新：已有线程计算时立即返回旧快照，UI 与动作线程不等待 UIA/COM 扫描。</summary>
	public static List<nint> GetTaskbarOrderedWindows()
	{
		DateTime now = DateTime.UtcNow;
		lock (s_snapshotLock)
		{
			if (s_snapshot != null && (now - s_snapshotAt).TotalMilliseconds < SNAPSHOT_TTL_MS)
			{
				return s_snapshot;
			}
		}

		if (!System.Threading.Monitor.TryEnter(s_refreshLock))
		{
			lock (s_snapshotLock)
			{
				return s_snapshot ?? new List<nint>();
			}
		}

		try
		{
			now = DateTime.UtcNow;
			lock (s_snapshotLock)
			{
				if (s_snapshot != null && (now - s_snapshotAt).TotalMilliseconds < SNAPSHOT_TTL_MS)
				{
					return s_snapshot;
				}
			}

			lock (s_procDescCache)
			{
				s_procDescCache.Clear();
			}
			lock (s_iconCache)
			{
				s_iconCache.Clear();
			}

			// COM 对象在本次刷新调用所在的线程中创建、使用，不再由任意首次调用者静态初始化并跨 Apartment 共享。
			IVirtualDesktopManager? virtualDesktopManager = CreateVdm();
			List<nint> computed = ComputeOrderedWindows(virtualDesktopManager);
			lock (s_snapshotLock)
			{
				s_snapshot = computed;
				s_snapshotAt = DateTime.UtcNow;
				return s_snapshot;
			}
		}
		finally
		{
			System.Threading.Monitor.Exit(s_refreshLock);
		}
	}

	/// <summary>实际计算：只保留能对应到任务栏按钮的窗口（鬼窗口丢弃）；同进程多窗口聚合到同一按钮槽位、组内按句柄升序（稳定）。</summary>
	private static List<nint> ComputeOrderedWindows(IVirtualDesktopManager? virtualDesktopManager)
	{
		List<nint> candidates = GetTaskbarWindows(virtualDesktopManager);
		if (candidates.Count == 0)
		{
			return candidates;
		}
		List<string>? names = GetTaskbarButtonNames();
		if (names != null && names.Count > 0)
		{
			HashSet<nint> used = new HashSet<nint>();
			List<nint> ordered = new List<nint>(candidates.Count);
			foreach (string name in names)
			{
				List<nint> matched = new List<nint>();
				foreach (nint h in candidates)
				{
					if (used.Contains(h))
					{
						continue;
					}
					if (NameMatchesWindowOrProcess(name, h))
					{
						matched.Add(h);
					}
				}
				if (matched.Count > 0)
				{
					matched.Sort((a, b) => a.ToInt64().CompareTo(b.ToInt64())); // 组内稳定序（句柄≈创建先后）
					foreach (nint h in matched)
					{
						ordered.Add(h);
						used.Add(h);
					}
				}
			}
			// 未匹配到任何按钮的候选 = 鬼窗口，丢弃：任务栏没有的按钮，菜单里就没有 N
			return ordered;
		}
		var app = GetTaskbarAppSlots();
		if (app != null && app.Value.slots.Count > 0)
		{
			HashSet<nint> used2 = new HashSet<nint>();
			List<nint> bySlots = new List<nint>(candidates.Count);
			foreach (var s in app.Value.slots)
			{
				if (s.hwnd != IntPtr.Zero && candidates.Contains(s.hwnd) && used2.Add(s.hwnd))
				{
					bySlots.Add(s.hwnd);
				}
			}
			foreach (nint h in candidates.OrderBy(c => c.ToInt64()))
			{
				if (used2.Add(h))
				{
					bySlots.Add(h);
				}
			}
			return bySlots;
		}
		// 兜底：按句柄排序，保证顺序稳定（不再用 z 序）
		return candidates.OrderBy(c => c.ToInt64()).ToList();
	}

	/// <summary>窗口是否对应任务栏按钮名：标题匹配（精确/前缀），或所属程序的进程描述/文件名匹配（支持分组应用）。</summary>
	private static bool NameMatchesWindowOrProcess(string name, nint hWnd)
	{
		if (NameMatchesWindow(name, TitleOf(hWnd)))
		{
			return true;
		}
		string? desc = ProcessDescriptionOf(hWnd);
		if (string.IsNullOrEmpty(desc))
		{
			return false;
		}
		if (string.Equals(desc, name, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (name.StartsWith(desc, StringComparison.OrdinalIgnoreCase) && desc.Length >= 3)
		{
			return true;
		}
		if (desc.StartsWith(name, StringComparison.OrdinalIgnoreCase) && name.Length >= 3)
		{
			return true;
		}
		return false;
	}

	/// <summary>窗口所属进程的显示名（exe 的 FileDescription，取不到用文件名），按进程缓存；便于匹配任务栏分组按钮名。</summary>
	private static string? ProcessDescriptionOf(nint hWnd)
	{
		GetWindowThreadProcessId(hWnd, out uint pid);
		if (pid == 0) return null;
		lock (s_procDescCache)
		{
			if (s_procDescCache.TryGetValue(pid, out string? desc))
			{
				return desc;
			}
		}
		string? computed = null;
		try
		{
			nint hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
			if (hProcess != IntPtr.Zero)
			{
				try
				{
					uint size = 1024;
					StringBuilder sb = new StringBuilder((int)size);
					if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
					{
						string exePath = sb.ToString();
						try
						{
							var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
							computed = !string.IsNullOrEmpty(fvi.FileDescription) ? fvi.FileDescription : System.IO.Path.GetFileNameWithoutExtension(exePath);
						}
						catch
						{
							computed = System.IO.Path.GetFileNameWithoutExtension(exePath);
						}
					}
				}
				finally
				{
					CloseHandle(hProcess);
				}
			}
		}
		catch
		{
			computed = null;
		}
		if (computed != null)
		{
			lock (s_procDescCache)
			{
				s_procDescCache[pid] = computed;
			}
		}
		return computed;
	}

	// ---- 手势级快照缓存（一次手势内：所有槽位图标 + 激活共用一次枚举/UIA/进程读取）----
	private static readonly object s_snapshotLock = new object();
	private static readonly object s_refreshLock = new object();
	private static List<nint>? s_snapshot;
	private static DateTime s_snapshotAt;
	private static int s_prefetchRunning;
	private static readonly Dictionary<uint, string> s_procDescCache = new Dictionary<uint, string>();
	private static readonly Dictionary<nint, BitmapSource> s_iconCache = new Dictionary<nint, BitmapSource>();
	private const double SNAPSHOT_TTL_MS = 2500.0;

	/// <summary>在线程池 MTA 中调度一次任务栏快照预热；重复请求合并为一个后台刷新。</summary>
	public static void Prefetch()
	{
		if (System.Threading.Interlocked.CompareExchange(ref s_prefetchRunning, 1, 0) != 0)
		{
			return;
		}
		try
		{
			System.Threading.ThreadPool.QueueUserWorkItem(delegate
			{
				try
				{
					GetTaskbarOrderedWindows();
				}
				catch
				{
				}
				finally
				{
					System.Threading.Volatile.Write(ref s_prefetchRunning, 0);
				}
			});
		}
		catch
		{
			System.Threading.Volatile.Write(ref s_prefetchRunning, 0);
			throw;
		}
	}

	/// <summary>
	/// 任务栏第 n 个运行窗口（按任务栏可见顺序）；越界返回 0。快照缓存：同一次手势内所有取值共用一份。
	/// </summary>
	public static nint GetNthTaskbarWindow(int n)
	{
		if (n <= 0)
		{
			return IntPtr.Zero;
		}
		List<nint> list = GetTaskbarOrderedWindows();
		if (n > list.Count)
		{
			return IntPtr.Zero;
		}
		return list[n - 1];
	}

	/// <summary>任务栏第 n 个运行窗口的图标（与激活同一顺序快照，天然一致；图标按窗口缓存）。</summary>
	public static BitmapSource? GetNthWindowIcon(int n)
	{
		if (n <= 0)
		{
			return null;
		}
		List<nint> list = GetTaskbarOrderedWindows();
		if (n > list.Count)
		{
			return null;
		}
		return GetWindowIcon(list[n - 1]);
	}

	/// <summary>切换到任务栏第 n 个运行窗口（与图标同一槽位列表，由构造保证显示与启动一致）。</summary>
	public static bool ActivateTaskbarSlot(int n)
	{
		if (n <= 0)
		{
			return false;
		}
		nint hWnd = GetNthTaskbarWindow(n);
		if (hWnd == IntPtr.Zero)
		{
			return false;
		}
		return ActivateWindow(hWnd);
	}

	/// <summary>将窗口激活到前台：处理 Windows 前台锁（AttachThreadInput 到前台/目标线程 + BringWindowToTop + SetForegroundWindow，失败再最小化还原兜底强制前台）。</summary>
	public static bool ActivateWindow(nint hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			nint hForeground = GetForegroundWindow();
			uint foregroundThread = (hForeground != IntPtr.Zero) ? GetWindowThreadProcessId(hForeground, out _) : 0u;
			uint targetThread = GetWindowThreadProcessId(hWnd, out _);
			uint currentThread = GetCurrentThreadId();

			bool attachedForeground = false;
			bool attachedTarget = false;
			if (foregroundThread != 0u && foregroundThread != currentThread && foregroundThread != targetThread)
			{
				attachedForeground = AttachThreadInput(foregroundThread, currentThread, true);
			}
			if (targetThread != currentThread)
			{
				attachedTarget = AttachThreadInput(currentThread, targetThread, true);
			}

			if (IsIconic(hWnd))
			{
				ShowWindow(hWnd, SW_RESTORE);
			}
			BringWindowToTop(hWnd);
			bool ok = SetForegroundWindow(hWnd);

			// 兜底 1：模拟一次 Alt 键——系统将我们视为"近期有用户输入"的进程，从而授予前台权限
			// （对刚启动的任务管理器等新窗口有效；Alt 按下即松开，不触发菜单）
			if (!ok)
			{
				keybd_event(VK_MENU, 0, 0, IntPtr.Zero);
				keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
				ok = SetForegroundWindow(hWnd);
			}

			// 兜底 2：前台可能是全屏/无边框全屏窗口——置顶+显示激活后取消置顶，
			// 可把新窗口抬到全屏之上（独占全屏游戏除外，只能靠切走/Alt-Tab）
			if (!ok)
			{
				SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
				ok = SetForegroundWindow(hWnd);
				SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
			}

			if (attachedTarget)
			{
				AttachThreadInput(currentThread, targetThread, false);
			}
			if (attachedForeground)
			{
				AttachThreadInput(foregroundThread, currentThread, false);
			}
			return ok;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 提取窗口图标：WM_GETICON(大)→(小)→类图标 → 进程 exe 图标(ExtractIconEx，需 DestroyIcon)；
	/// 系统持有的窗口/类图标不销毁，只做一次性拷贝。
	/// </summary>
	public static BitmapSource? GetWindowIcon(nint hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return null;
		}
		lock (s_iconCache)
		{
			if (s_iconCache.TryGetValue(hWnd, out BitmapSource? cached))
			{
				return cached;
			}
		}
		BitmapSource? bmp = ComputeWindowIcon(hWnd);
		if (bmp != null)
		{
			lock (s_iconCache)
			{
				s_iconCache[hWnd] = bmp;
			}
		}
		return bmp;
	}

	private static BitmapSource? ComputeWindowIcon(nint hWnd)
	{
		try
		{
			// WM_GETICON 用 SendMessageTimeout（带 SMTO_ABORTIFHUNG）——超时缩短至 25ms，目标窗口假死时不阻塞轮盘渲染
			nint hIcon = IntPtr.Zero;
			nint result = IntPtr.Zero;
			if (SendMessageTimeout(hWnd, WM_GETICON, (nint)1, IntPtr.Zero, SMTO_ABORTIFHUNG, 25u, out result) != IntPtr.Zero && result != IntPtr.Zero)
			{
				hIcon = result;
			}
			if (hIcon == IntPtr.Zero && SendMessageTimeout(hWnd, WM_GETICON, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 25u, out result) != IntPtr.Zero && result != IntPtr.Zero)
			{
				hIcon = result;
			}
			if (hIcon == IntPtr.Zero)
			{
				hIcon = GetClassLongPtr(hWnd, GCLP_HICON);
			}
			if (hIcon != IntPtr.Zero)
			{
				BitmapSource? bmp = ToBitmapSource(hIcon);
				if (bmp != null)
				{
					return bmp;
				}
			}

			// 进程 exe 图标兜底
			GetWindowThreadProcessId(hWnd, out uint pid);
			if (pid != 0)
			{
				try
				{
					nint hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
					if (hProcess != IntPtr.Zero)
					{
						try
						{
							uint size = 1024;
							StringBuilder sb = new StringBuilder((int)size);
							if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
							{
								string exe = sb.ToString();
								if (!string.IsNullOrEmpty(exe) && ExtractIconEx(exe, 0, out nint big, out _, 1) > 0 && big != IntPtr.Zero)
								{
									BitmapSource? bmp = ToBitmapSource(big);
									DestroyIcon(big);
									if (bmp != null)
									{
										return bmp;
									}
								}
							}
						}
						finally
						{
							CloseHandle(hProcess);
						}
					}
				}
				catch
				{
				}
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	private static BitmapSource? ToBitmapSource(nint hIcon)
	{
		try
		{
			BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bmp.Freeze();
			return bmp;
		}
		catch
		{
			return null;
		}
	}
}