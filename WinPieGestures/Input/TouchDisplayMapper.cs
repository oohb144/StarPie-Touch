using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace WinPieGestures.Input;

/// <summary>Maps raw HID axes using the digitizer's current Windows rotation.</summary>
internal static class TouchDisplayMapper
{
    private const int CurrentSettings = -1;
    private const int DevModeSize = 220;
    private const int SizeOffset = 68;
    private const int OrientationOffset = 84;

    // POINTER_DEVICE_INFO has a 520-WCHAR product string (1080 bytes on x64).
    // Leave room for either process bitness; only its first DWORD is needed.
    private const int PointerDeviceInfoBytes = 2048;

    internal static Point Map(double rawX, double rawY, double maxX, double maxY,
        double left, double top, double width, double height, int orientation)
    {
        double x = Math.Clamp(rawX / maxX, 0, 1);
        double y = Math.Clamp(rawY / maxY, 0, 1);
        // DISPLAYCONFIG_ROTATION is clockwise in screen coordinates. Unlike
        // DEVMODE.dmDisplayOrientation, its values are 1..4 and belong to the
        // input digitizer. This also covers the upside-down landscape case.
        (x, y) = orientation switch
        {
            2 => (1 - y, x),
            3 => (1 - x, 1 - y),
            4 => (y, 1 - x),
            _ => (x, y)
        };
        return new Point(left + x * width, top + y * height);
    }

    internal static int GetOrientation(nint device, double left, double top, double width, double height)
    {
        nint info = Marshal.AllocHGlobal(PointerDeviceInfoBytes);
        try
        {
            if (GetPointerDevice(device, info))
            {
                int rotation = Marshal.ReadInt32(info);
                if (rotation is >= 1 and <= 4) return rotation;
            }
        }
        finally { Marshal.FreeHGlobal(info); }

        // Older/virtual devices may not expose pointer metadata. DEVMODE uses
        // counter-clockwise 0..3, so convert its enum before mapping.
        return GetDisplayOrientation(left, top, width, height) switch
        {
            1 => 4,
            2 => 3,
            3 => 2,
            _ => 1
        };
    }

    private static int GetDisplayOrientation(double left, double top, double width, double height)
    {
        Screen? screen = Screen.AllScreens.FirstOrDefault(candidate =>
            candidate.Bounds.Left == (int)left && candidate.Bounds.Top == (int)top &&
            candidate.Bounds.Width == (int)width && candidate.Bounds.Height == (int)height);
        if (screen == null) return 0;
        nint mode = Marshal.AllocHGlobal(DevModeSize);
        try
        {
            for (int offset = 0; offset < DevModeSize; offset += sizeof(int))
                Marshal.WriteInt32(mode, offset, 0);
            Marshal.WriteInt16(mode, SizeOffset, DevModeSize);
            if (!EnumDisplaySettingsEx(screen.DeviceName, CurrentSettings, mode, 0)) return 0;
            return Marshal.ReadInt32(mode, OrientationOffset);
        }
        finally { Marshal.FreeHGlobal(mode); }
    }

    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsExW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettingsEx(string deviceName, int modeNumber, nint devMode, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPointerDevice(nint device, nint info);
}
