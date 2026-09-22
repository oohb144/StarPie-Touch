using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures.Input;

/// <summary>Diagnostic description of HID input fields; no report offsets are assumed.</summary>
internal static class HidCapsInspector
{
    private const uint RidiPreparsedData = 0x20000005;
    private const int HidpValueCapsSize = 72;

    public static string Describe(nint device)
    {
        uint size = 0;
        GetRawInputDeviceInfo(device, RidiPreparsedData, 0, ref size);
        if (size == 0 || size > 65536) return "preparsed data unavailable";
        nint preparsed = Marshal.AllocHGlobal((int)size);
        nint caps = Marshal.AllocHGlobal(64);
        try
        {
            if (GetRawInputDeviceInfo(device, RidiPreparsedData, preparsed, ref size) == uint.MaxValue)
                return $"preparsed data failed: {Marshal.GetLastWin32Error()}";
            int status = HidP_GetCaps(preparsed, caps);
            if (status < 0) return $"HidP_GetCaps failed: 0x{status:X8}";

            ushort reportBytes = (ushort)Marshal.ReadInt16(caps, 4);
            ushort valueCount = (ushort)Marshal.ReadInt16(caps, 48);
            ushort buttonCount = (ushort)Marshal.ReadInt16(caps, 46);
            var result = new StringBuilder($"inputReportBytes={reportBytes} buttonCaps={buttonCount} valueCaps={valueCount}");
            if (valueCount is 0 or > 128) return result.ToString();

            nint values = Marshal.AllocHGlobal(valueCount * HidpValueCapsSize);
            try
            {
                ushort length = valueCount;
                status = HidP_GetValueCaps(0, values, ref length, preparsed);
                if (status < 0) return result.Append($" HidP_GetValueCaps failed: 0x{status:X8}").ToString();
                for (int i = 0; i < length; i++)
                {
                    nint cap = values + i * HidpValueCapsSize;
                    ushort page = (ushort)Marshal.ReadInt16(cap, 0);
                    byte reportId = Marshal.ReadByte(cap, 2);
                    ushort linkCollection = (ushort)Marshal.ReadInt16(cap, 6);
                    bool range = Marshal.ReadByte(cap, 12) != 0;
                    ushort bits = (ushort)Marshal.ReadInt16(cap, 18);
                    ushort count = (ushort)Marshal.ReadInt16(cap, 20);
                    ushort usageMin = (ushort)Marshal.ReadInt16(cap, 56);
                    ushort usageMax = range ? (ushort)Marshal.ReadInt16(cap, 58) : usageMin;
                    ushort dataMin = (ushort)Marshal.ReadInt16(cap, 68);
                    ushort dataMax = range ? (ushort)Marshal.ReadInt16(cap, 70) : dataMin;
                    result.Append($" | {i}:page={page:X2} usage={usageMin:X2}-{usageMax:X2} "
                        + $"link={linkCollection} id={reportId} bits={bits} count={count} data={dataMin}-{dataMax}");
                }
                return result.ToString();
            }
            finally
            {
                Marshal.FreeHGlobal(values);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(caps);
            Marshal.FreeHGlobal(preparsed);
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(nint device, uint command, nint data, ref uint size);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(nint preparsedData, nint capabilities);

    [DllImport("hid.dll")]
    private static extern int HidP_GetValueCaps(int reportType, nint valueCaps,
        ref ushort valueCapsLength, nint preparsedData);
}
