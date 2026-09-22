using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WinPieGestures;

public static class MemoryOptimizer
{
	private static int _isTrimming = 0;

	private static DateTime _lastTrimTime = DateTime.MinValue;

	[DllImport("psapi.dll", SetLastError = true)]
	private static extern int EmptyWorkingSet(nint hwProc);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetProcessWorkingSetSize(nint proc, nint min, nint max);

	public static void TrimMemory(bool force = false)
	{
		if ((!force && (DateTime.UtcNow - _lastTrimTime).TotalSeconds < 5.0) || Interlocked.Exchange(ref _isTrimming, 1) == 1)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				_lastTrimTime = DateTime.UtcNow;
				if (force)
				{
					// 用户在设置中手动点击【立即压缩物理内存】时才执行深度工作集剥离
					GC.Collect(2, GCCollectionMode.Forced, blocking: true);
					GC.WaitForPendingFinalizers();
					GC.Collect(2, GCCollectionMode.Forced, blocking: true);
					if (Environment.OSVersion.Platform == PlatformID.Win32NT)
					{
						nint handle = Process.GetCurrentProcess().Handle;
						EmptyWorkingSet(handle);
						SetProcessWorkingSetSize(handle, new IntPtr(-1), new IntPtr(-1));
					}
				}
				else
				{
					// 日常关闭隐藏或后台驻留采用非阻塞温和回收 Gen 0, 1, 2 及大对象堆 LOH，
					// 释放位图与数据堆内存，同时保留热代码在 RAM 中，杜绝唤出硬缺页顿卡
					GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
					GC.WaitForPendingFinalizers();
				}
			}
			catch (Exception)
			{
			}
			finally
			{
				Interlocked.Exchange(ref _isTrimming, 0);
			}
		});
	}
}
