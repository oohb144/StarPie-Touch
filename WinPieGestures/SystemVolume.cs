using System;
using System.Runtime.InteropServices;

namespace WinPieGestures;

/// <summary>
/// 系统主音量读写（CoreAudio IAudioEndpointVolume）。
/// 相比向系统注入硬件音量键（单次 ≈±1~2%），这里可直接把主音量连续设置为 0~1 的任意值，即时生效，
/// 供"拖距调音"（音量加/减扇区按鼠标拖动距离映射音量）使用。
/// </summary>
public static class SystemVolume
{
	[ComImport]
	[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
	private class MMDeviceEnumeratorComObject
	{
	}

	[ComImport]
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDeviceEnumerator
	{
		[PreserveSig]
		int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);

		[PreserveSig]
		int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);

		[PreserveSig]
		int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);

		[PreserveSig]
		int RegisterEndpointNotificationCallback(IntPtr pClient);

		[PreserveSig]
		int UnregisterEndpointNotificationCallback(IntPtr pClient);
	}

	[ComImport]
	[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDeviceCollection
	{
	}

	[ComImport]
	[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDevice
	{
		[PreserveSig]
		int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, out IAudioEndpointVolume ppInterface);

		[PreserveSig]
		int OpenPropertyStore(uint stgmAccess, IntPtr ppProperties);

		[PreserveSig]
		int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

		[PreserveSig]
		int GetState(out uint pdwState);
	}

	[ComImport]
	[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioEndpointVolume
	{
		[PreserveSig]
		int RegisterControlChangeNotify(IntPtr pNotify);

		[PreserveSig]
		int UnregisterControlChangeNotify(IntPtr pNotify);

		[PreserveSig]
		int GetChannelCount(out uint pnChannelCount);

		[PreserveSig]
		int SetMasterVolumeLevel(float fLevelDB, IntPtr pguidEventContext);

		[PreserveSig]
		int SetMasterVolumeLevelScalar(float fLevel, IntPtr pguidEventContext);

		[PreserveSig]
		int GetMasterVolumeLevel(out float pfLevelDB);

		[PreserveSig]
		int GetMasterVolumeLevelScalar(out float pfLevel);

		[PreserveSig]
		int SetChannelVolumeLevel(uint nChannel, float fLevelDB, IntPtr pguidEventContext);

		[PreserveSig]
		int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, IntPtr pguidEventContext);

		[PreserveSig]
		int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);

		[PreserveSig]
		int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

		[PreserveSig]
		int SetMute(int bMute, IntPtr pguidEventContext);

		[PreserveSig]
		int GetMute(out int pbMute);

		[PreserveSig]
		int SetVolumeStep(IntPtr pguidEventContext);

		[PreserveSig]
		int GetVolumeStep(out uint pnStep);

		[PreserveSig]
		int QueryHardwareSupport(out uint pdwHardwareSupportMask);

		[PreserveSig]
		int GetVolumeRange(out float pflMinVolumeDB, out float pflMaxVolumeDB, out float pflIncrementDB);
	}

	// Native volume flyout wake-up: inject a system volume key then correct to the exact target.
	private const uint KEYEVENTF_KEYUP = 2u;
	private const ushort VK_VOLUME_DOWN = 174;
	private const ushort VK_VOLUME_UP = 175;

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nint dwExtraInfo);

	private static readonly object _endpointLock = new object();

	private static IMMDevice? _device;

	private static IAudioEndpointVolume? _endpointVolume;

	// Set when the endpoint object is built; GetEndpointVolume rebuilds the
	// COM objects when the system default play device switches.
	private static string? _endpointId;

	/// <summary>读取系统主音量（0.0~1.0）。失败返回 false。</summary>
	public static bool TryGetVolume(out float percent)
	{
		percent = 0f;
		IAudioEndpointVolume? volume = GetEndpointVolume();
		if (volume == null)
		{
			return false;
		}
		try
		{
			if (volume.GetMasterVolumeLevelScalar(out percent) != 0)
			{
				ResetEndpoint();
				return false;
			}
			return true;
		}
		catch
		{
			ResetEndpoint();
			return false;
		}
	}

	/// <summary>设置系统主音量（0.0~1.0，越界自动夹取）。失败返回 false。</summary>
	public static bool SetVolume(float percent)
	{
		float clamped = Math.Clamp(percent, 0f, 1f);
		IAudioEndpointVolume? volume = GetEndpointVolume();
		if (volume == null)
		{
			return false;
		}
		try
		{
			if (volume.SetMasterVolumeLevelScalar(clamped, IntPtr.Zero) != 0)
			{
				ResetEndpoint();
				return false;
			}
			return true;
		}
		catch
		{
			ResetEndpoint();
			return false;
		}
	}

	/// <summary>获取系统是否处于静音状态。失败返回 false。</summary>
	public static bool GetMute(out bool isMuted)
	{
		isMuted = false;
		IAudioEndpointVolume? volume = GetEndpointVolume();
		if (volume == null)
		{
			return false;
		}
		try
		{
			if (volume.GetMute(out int muteVal) == 0)
			{
				isMuted = (muteVal != 0);
				return true;
			}
			ResetEndpoint();
			return false;
		}
		catch
		{
			ResetEndpoint();
			return false;
		}
	}

	/// <summary>设置系统静音状态。失败返回 false。</summary>
	public static bool SetMute(bool mute)
	{
		IAudioEndpointVolume? volume = GetEndpointVolume();
		if (volume == null)
		{
			return false;
		}
		try
		{
			if (volume.SetMute(mute ? 1 : 0, IntPtr.Zero) == 0)
			{
				return true;
			}
			ResetEndpoint();
			return false;
		}
		catch
		{
			ResetEndpoint();
			return false;
		}
	}

	/// <summary>Wake the native volume flyout and correct system volume to the exact target.</summary>
	/// <remarks>Injecting a system volume key makes the OS pop the volume OSD; the trailing
	/// SetVolume immediately re-aligns the real volume so the flyout reflects the target.</remarks>
	public static void ShowOsd(float targetPercent, bool directionUp)
	{
		try
		{
			ushort vk = directionUp ? VK_VOLUME_UP : VK_VOLUME_DOWN;
			keybd_event((byte)vk, 0, 0, 0);
			keybd_event((byte)vk, 0, KEYEVENTF_KEYUP, 0);
			SetVolume(targetPercent);
		}
		catch
		{
		}
	}

	/// <summary>Resolve current default render endpoint id; null on failure.</summary>
	private static string? ResolveDefaultEndpointId()
	{
		try
		{
			IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
			if (enumerator.GetDefaultAudioEndpoint(0, 0, out IMMDevice device) != 0)
			{
				Marshal.ReleaseComObject(enumerator);
				return null;
			}
			string? id = null;
			try
			{
				device.GetId(out string sid);
				id = sid;
			}
			catch
			{
			}
			Marshal.ReleaseComObject(device);
			Marshal.ReleaseComObject(enumerator);
			return id;
		}
		catch
		{
			return null;
		}
	}

	private static IAudioEndpointVolume? GetEndpointVolume()
	{
		lock (_endpointLock)
		{
			string? currentEndpointId = ResolveDefaultEndpointId();
			if (_endpointVolume != null && currentEndpointId != null && string.Equals(_endpointId, currentEndpointId, StringComparison.OrdinalIgnoreCase))
			{
				return _endpointVolume;
			}
			// Default play device switched (or first access): rebuild cached endpoint objects
			if (_endpointVolume != null)
			{
				ResetEndpoint();
			}
			try
			{
				IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
				// 默认播放设备 + 控制台角色（eRender/eConsole）
				if (enumerator.GetDefaultAudioEndpoint(0, 0, out IMMDevice device) != 0)
				{
					Marshal.ReleaseComObject(enumerator);
					return null;
				}
				Guid iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
				// CLSCTX_ALL = 23
				if (device.Activate(ref iid, 23u, IntPtr.Zero, out IAudioEndpointVolume volume) != 0)
				{
					Marshal.ReleaseComObject(device);
					Marshal.ReleaseComObject(enumerator);
					return null;
				}
				_device = device;
				_endpointVolume = volume;
				Marshal.ReleaseComObject(enumerator);
				string dbgEndpointId = "(unknown)";
				try
				{
					if (device.GetId(out string dbgSid) == 0)
					{
						dbgEndpointId = dbgSid;
					}
				}
				catch
				{
				}
				_endpointId = dbgEndpointId;
				return volume;
			}
			catch
			{
				return null;
			}
		}
	}

	private static void ResetEndpoint()
	{
		lock (_endpointLock)
		{
			_endpointId = null;
			if (_endpointVolume != null)
			{
				try
				{
					Marshal.ReleaseComObject(_endpointVolume);
				}
				catch
				{
				}
				_endpointVolume = null;
			}
			if (_device != null)
			{
				try
				{
					Marshal.ReleaseComObject(_device);
				}
				catch
				{
				}
				_device = null;
			}
		}
	}
}
