using System.Runtime.InteropServices;
using System.Windows;

namespace WinPieGestures.Input;

/// <summary>
/// Opt-in background digitizer diagnostic. It observes raw reports and runs
/// recognition without opening the wheel or suppressing ordinary input.
/// </summary>
internal sealed class RawDigitizerProbe : IDisposable
{
    public const int WmInput = 0x00FF;
    private const uint RidevRemove = 0x00000001;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceInfo = 0x2000000B;
    private const ushort DigitizerPage = 0x0D;

    private readonly HashSet<ushort> _registered = new();
    private long _lastLogAt;
    private long _lastHeartbeatAt;
    private long _lastErrorLogAt;
    private int _lastTouchContactCount = -1;
    private long _reportCount;
    private long _backgroundCount;
    private readonly Dictionary<nint, (ushort page, ushort usage)> _devices = new();
    private readonly Dictionary<nint, HidContactDecoder?> _decoders = new();
    private readonly Dictionary<nint, NativeRect> _displayRects = new();
    private readonly Dictionary<nint, HashSet<int>> _activeTouches = new();
    private readonly Dictionary<nint, long> _pensInRange = new();
    private readonly TouchGestureRecognizer _recognizer;
    private readonly bool _penGuardEnabled;
    private readonly HashSet<nint> _invalidTouchDevices = new();
    private readonly bool _detailedLog;
    private nint _trackingTouchDevice;

    public long ReportCount => _reportCount;
    public event EventHandler<TouchGestureEventArgs>? GestureDetected;
    public event Action<Point, int>? TouchFrameChanged;
    public event Action? TouchFrameInvalidated;
    public event Action<bool>? PenPresenceChanged;

    public RawDigitizerProbe(nint target, bool detailedLog = true, TouchGestureConfig? config = null)
    {
        _detailedLog = detailedLog;
        _recognizer = new TouchGestureRecognizer(config);
        _penGuardEnabled = config?.EnablePenGuard ?? true;
        _recognizer.GestureDetected += (_, gesture) =>
        {
            if (_detailedLog)
                AppLogger.LogInfo($"[raw-digitizer-probe] Global two-finger recognition: "
                    + $"{gesture.Direction} start=({gesture.StartPoint.X:0},{gesture.StartPoint.Y:0}) "
                    + $"current=({gesture.CurrentPoint.X:0},{gesture.CurrentPoint.Y:0}) "
                    + $"distance={gesture.Distance:0} px (diagnostic only)");
            else
                AppLogger.LogInfo($"[touch-wheel-experiment] Recognized {gesture.Direction} at "
                    + $"({gesture.StartPoint.X:0},{gesture.StartPoint.Y:0})");
            GestureDetected?.Invoke(this, gesture);
        };
        // HID digitizer usages: touchscreen, integrated pen, external pen.
        foreach (ushort usage in new ushort[] { 0x04, 0x02, 0x01 })
        {
            if (Register(usage, RidevInputSink, target))
            {
                _registered.Add(usage);
                if (_detailedLog)
                    AppLogger.LogInfo($"[raw-digitizer-probe] Registered HID 0x0D/0x{usage:X2} INPUTSINK");
            }
            else
            {
                AppLogger.LogWarn($"[raw-digitizer-probe] Register HID 0x0D/0x{usage:X2} failed: Win32 error {Marshal.GetLastWin32Error()}");
            }
        }
    }

    public void OnRawInput(nint rawHandle, nint messageWParam)
    {
        _reportCount++;
        if (((nuint)messageWParam & 0xff) == 1) _backgroundCount++;
        long now = Environment.TickCount64;
        if (!_detailedLog && now - _lastHeartbeatAt >= 5000)
        {
            _lastHeartbeatAt = now;
            AppLogger.LogInfo($"[touch-wheel-experiment] Raw reports={_reportCount} background={_backgroundCount}");
        }
        bool shouldLog = _detailedLog && now - _lastLogAt >= 250;
        if (shouldLog) _lastLogAt = now;
        try
        {
            string detail = DescribeRawInput(rawHandle, now, shouldLog);
            if (shouldLog)
                AppLogger.LogInfo($"[raw-digitizer-probe] WM_INPUT count={_reportCount} background={_backgroundCount} {detail}");
        }
        catch (Exception ex)
        {
            if (shouldLog || now - _lastErrorLogAt >= 5000)
            {
                _lastErrorLogAt = now;
                AppLogger.LogWarn($"[touch-wheel] decode-error={ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private string DescribeRawInput(nint rawHandle, long now, bool shouldLog)
    {
        uint byteCount = 0;
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        if (GetRawInputData(rawHandle, RidInput, 0, ref byteCount, headerSize) == uint.MaxValue ||
            byteCount < headerSize + 8 || byteCount > 65536)
            return "raw-data=unavailable";

        nint buffer = Marshal.AllocHGlobal((int)byteCount);
        try
        {
            if (GetRawInputData(rawHandle, RidInput, buffer, ref byteCount, headerSize) == uint.MaxValue)
                return "raw-data=unavailable";
            RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (!_devices.TryGetValue(header.Device, out var usage))
            {
                usage = ReadDeviceUsage(header.Device);
                _devices[header.Device] = usage;
                if (_detailedLog)
                    AppLogger.LogInfo($"[raw-digitizer-probe] Device handle=0x{header.Device:X} HID=0x{usage.page:X2}/0x{usage.usage:X2} "
                        + $"mapping: {DescribeDeviceRects(header.Device)} caps: {HidCapsInspector.Describe(header.Device)}");
                _decoders[header.Device] = HidContactDecoder.Create(header.Device);
                if (GetPointerDeviceRects(header.Device, out _, out NativeRect display))
                    _displayRects[header.Device] = display;
            }
            if (header.Type != 2) return $"type={header.Type} device=0x{header.Device:X}";

            int reportSize = Marshal.ReadInt32(buffer, (int)headerSize);
            int reportCount = Marshal.ReadInt32(buffer, (int)headerSize + 4);
            int available = (int)byteCount - (int)headerSize - 8;
            if (reportSize <= 0 || reportCount <= 0 || (long)reportSize * reportCount > available ||
                !_decoders.TryGetValue(header.Device, out HidContactDecoder? decoder) || decoder == null)
                return "report-layout-or-decoder=unavailable";

            string lastDecoded = "";
            for (int i = 0; i < reportCount; i++)
            {
                nint report = buffer + (int)headerSize + 8 + i * reportSize;
                HidContactDecoder.ReportSnapshot snapshot = decoder.Decode(report, (uint)reportSize);
                TrackSnapshot(header.Device, snapshot, now);
                if (shouldLog) lastDecoded = snapshot.ToString();
            }
            if (!shouldLog) return "";
            int sampleSize = Math.Min(reportSize, 16);
            var sample = new byte[sampleSize];
            Marshal.Copy(buffer + (int)headerSize + 8, sample, 0, sampleSize);
            return $"HID=0x{usage.page:X2}/0x{usage.usage:X2} reportBytes={reportSize} "
                + $"reports={reportCount} sample={Convert.ToHexString(sample)} {lastDecoded}";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void TrackSnapshot(nint device, HidContactDecoder.ReportSnapshot snapshot, long now)
    {
        if (snapshot.Error != null) return;
        if (snapshot.IsPen)
        {
            if (!_penGuardEnabled) return;
            bool wasBlocked = _pensInRange.Count != 0;
            if (snapshot.PenInRange || snapshot.PenTip) _pensInRange[device] = now;
            else _pensInRange.Remove(device);
            if (_pensInRange.Count != 0) _recognizer.PenDown(now);
            else if (wasBlocked) _recognizer.PenUp(now);
            if (wasBlocked != (_pensInRange.Count != 0))
                PenPresenceChanged?.Invoke(_pensInRange.Count != 0);
            return;
        }
        if (!snapshot.IsTouch || !_displayRects.TryGetValue(device, out NativeRect display)) return;
        // Some devices stop reporting when the pen leaves proximity instead of
        // sending an explicit out-of-range frame. Release a stale pen lease.
        bool hadPen = _pensInRange.Count != 0;
        foreach (nint stale in _pensInRange.Where(pair => now - pair.Value > 1000)
                     .Select(pair => pair.Key).ToArray())
            _pensInRange.Remove(stale);
        if (hadPen && _pensInRange.Count == 0) _recognizer.PenUp(now);
        if (hadPen && _pensInRange.Count == 0) PenPresenceChanged?.Invoke(false);
        if (!_activeTouches.TryGetValue(device, out HashSet<int>? previous))
            _activeTouches[device] = previous = new HashSet<int>();

        // Accept only complete, confident frames. An incomplete frame cannot
        // safely update the contact lifetime tracked by the recognizer.
        // On this HID device ContactCount can remain 2 as one or both Tip bits
        // clear during release. Compare it as an upper bound for active contacts.
        bool valid = snapshot.ContactCount is uint reported &&
            reported >= snapshot.Contacts.Count && reported <= 10 &&
            snapshot.Contacts.Count <= 10 &&
            snapshot.Contacts.All(contact => contact.Confidence && contact.XMax > 0 &&
                contact.YMax > 0 && contact.Id <= int.MaxValue &&
                contact.X <= contact.XMax && contact.Y <= contact.YMax) &&
            display.Right > display.Left && display.Bottom > display.Top;
        if (!valid)
        {
            if (!_invalidTouchDevices.Contains(device))
                AppLogger.LogWarn($"[touch-wheel] Invalid frame: reported={snapshot.ContactCount} "
                    + $"decoded={snapshot.Contacts.Count}; canceling contact sequence");
            _invalidTouchDevices.Add(device);
            foreach (int oldId in previous) _recognizer.TouchUp(oldId);
            previous.Clear();
            if (_trackingTouchDevice == device) _trackingTouchDevice = 0;
            TouchFrameInvalidated?.Invoke();
            return;
        }

        // A malformed frame invalidates the entire contact sequence. Resume
        // only after the device reports all fingers lifted.
        if (_invalidTouchDevices.Contains(device))
        {
            if (snapshot.Contacts.Count == 0) _invalidTouchDevices.Remove(device);
            return;
        }

        // This diagnostic tracks only one touchscreen at a time. Devices have
        // independent contact ID spaces; mixing them would invent a pair.
        if (_trackingTouchDevice != 0 && _trackingTouchDevice != device && previous.Count == 0)
            return;
        if (snapshot.Contacts.Count != 0) _trackingTouchDevice = device;

        var current = new HashSet<int>(snapshot.Contacts.Select(contact => (int)contact.Id));
        if (current.Count != _lastTouchContactCount)
        {
            _lastTouchContactCount = current.Count;
            AppLogger.LogInfo($"[touch-wheel] Contacts={current.Count}");
        }
        foreach (int oldId in previous.Where(id => !current.Contains(id)).ToArray())
            _recognizer.TouchUp(oldId);
        foreach (HidContactDecoder.ContactSample contact in snapshot.Contacts)
        {
            int id = (int)contact.Id;
            var point = new Point(
                display.Left + contact.X * (double)(display.Right - display.Left) / contact.XMax,
                display.Top + contact.Y * (double)(display.Bottom - display.Top) / contact.YMax);
            if (previous.Contains(id)) _recognizer.TouchMove(id, point, now);
            else _recognizer.TouchDown(id, point, now);
        }
        previous.Clear();
        previous.UnionWith(current);
        if (current.Count == 0) _trackingTouchDevice = 0;
        if (snapshot.Contacts.Count > 0)
        {
            double x = snapshot.Contacts.Average(contact =>
                display.Left + contact.X * (double)(display.Right - display.Left) / contact.XMax);
            double y = snapshot.Contacts.Average(contact =>
                display.Top + contact.Y * (double)(display.Bottom - display.Top) / contact.YMax);
            TouchFrameChanged?.Invoke(new Point(x, y), current.Count);
        }
        else TouchFrameChanged?.Invoke(default, 0);
    }

    private static (ushort page, ushort usage) ReadDeviceUsage(nint device)
    {
        uint size = 0;
        GetRawInputDeviceInfo(device, RidiDeviceInfo, 0, ref size);
        if (size < 24 || size > 1024) return (0, 0);
        nint buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.WriteInt32(buffer, (int)size);
            if (GetRawInputDeviceInfo(device, RidiDeviceInfo, buffer, ref size) == uint.MaxValue)
                return (0, 0);
            return ((ushort)Marshal.ReadInt16(buffer, 20), (ushort)Marshal.ReadInt16(buffer, 22));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string DescribeDeviceRects(nint device)
    {
        if (!GetPointerDeviceRects(device, out NativeRect digitizer, out NativeRect display))
            return $"GetPointerDeviceRects failed={Marshal.GetLastWin32Error()}";
        return $"digitizerHimetric=({digitizer.Left},{digitizer.Top})-({digitizer.Right},{digitizer.Bottom}) "
            + $"displayPixels=({display.Left},{display.Top})-({display.Right},{display.Bottom})";
    }

    public void Dispose()
    {
        foreach (HidContactDecoder? decoder in _decoders.Values) decoder?.Dispose();
        _decoders.Clear();
        foreach (ushort usage in _registered)
        {
            if (!Register(usage, RidevRemove, 0))
                AppLogger.LogWarn($"[raw-digitizer-probe] Unregister HID 0x0D/0x{usage:X2} failed: Win32 error {Marshal.GetLastWin32Error()}");
        }
        _registered.Clear();
    }

    private static bool Register(ushort usage, uint flags, nint target)
    {
        var devices = new[] { new RawInputDevice(DigitizerPage, usage, flags, target) };
        return RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RawInputDevice>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RawInputDevice
    {
        public readonly ushort UsagePage;
        public readonly ushort Usage;
        public readonly uint Flags;
        public readonly nint Target;

        public RawInputDevice(ushort usagePage, ushort usage, uint flags, nint target)
            => (UsagePage, Usage, Flags, Target) = (usagePage, usage, flags, target);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public nint Device;
        public nint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices([In] RawInputDevice[] devices,
        uint deviceCount, uint deviceSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(nint rawInput, uint command, nint data,
        ref uint size, uint headerSize);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(nint device, uint command, nint data,
        ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPointerDeviceRects(nint device, out NativeRect digitizerRect,
        out NativeRect displayRect);
}
