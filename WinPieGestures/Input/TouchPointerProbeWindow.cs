using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace WinPieGestures.Input;

/// <summary>
/// Phase 1 diagnostic only. WM_POINTER is delivered to this window when it is the
/// hit-test target; this is deliberately not advertised as a global input hook.
/// </summary>
internal sealed class TouchPointerProbeWindow : Window
{
    private const int WmPointerUpdate = 0x0245;
    private const int WmPointerDown = 0x0246;
    private const int WmPointerUp = 0x0247;
    private const int WmPointerEnter = 0x0249;
    private const int WmPointerLeave = 0x024A;
    private const uint PointerFlagInContact = 0x00000004;
    private const uint PointerFlagDown = 0x00010000;
    private const uint PointerFlagUp = 0x00040000;

    private readonly TextBlock _status;
    private readonly TextBlock _motionStatus;
    private readonly TextBlock _gestureStatus;
    private readonly TouchGestureRecognizer _recognizer = new();
    private RawDigitizerProbe? _rawDigitizerProbe;
    private readonly Dictionary<uint, PixelPoint> _activeTouches = new();
    private HwndSource? _source;
    private long _lastUpdateLogTicks;
    private long _lastTouchMoveLogTicks;

    public TouchPointerProbeWindow(bool enableRawDigitizerProbe = false)
    {
        Title = "StarPie Touch/Pen Pointer Probe";
        Width = 650;
        Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "在此窗口空白区域触摸、双指触摸或使用手写笔。\n"
                 + "验证双指：一指按住不放，再落下第二指；观察下方是否显示触点数=2和两个不同 ID。\n"
                 + "本阶段只验证本窗口的 Pointer 数据，不会触发轮盘，也不会拦截其他应用。",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            Margin = new Thickness(0, 0, 0, 20)
        });
        _status = new TextBlock
        {
            Text = "等待 Touch / Pen Pointer…",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 18
        };
        panel.Children.Add(_status);
        _motionStatus = new TextBlock
        {
            Text = "等待 WPF Touch Down / Move / Up…",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            Margin = new Thickness(0, 20, 0, 0)
        };
        panel.Children.Add(_motionStatus);
        _gestureStatus = new TextBlock
        {
            Text = "双指识别：等待 150ms 保持后同向滑动（仅诊断，不打开轮盘）",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            Margin = new Thickness(0, 20, 0, 0)
        };
        panel.Children.Add(_gestureStatus);
        Content = new Grid { Background = System.Windows.Media.Brushes.White, Children = { panel } };

        PreviewTouchDown += OnTouchDown;
        PreviewTouchMove += OnTouchMove;
        PreviewTouchUp += OnTouchUp;
        PreviewStylusDown += OnStylusDown;
        PreviewStylusUp += OnStylusUp;
        _recognizer.GestureDetected += (_, gesture) =>
        {
            _gestureStatus.Text = $"双指识别：{gesture.Direction}，滑动 {gesture.Distance:0} px";
            AppLogger.LogInfo($"[touch-probe] Recognized two-finger slide: {gesture.Direction}, distance={gesture.Distance:0} px");
        };

        SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source?.AddHook(WndProc);
            if (enableRawDigitizerProbe)
                _rawDigitizerProbe = new RawDigitizerProbe(new WindowInteropHelper(this).Handle);
            AppLogger.LogInfo("[touch-probe] Diagnostic window ready; touch/pen input is local to its HWND.");
        };
        Closed += (_, _) =>
        {
            _source?.RemoveHook(WndProc);
            _source = null;
            _rawDigitizerProbe?.Dispose();
            _rawDigitizerProbe = null;
        };
    }

    private void OnTouchDown(object? sender, TouchEventArgs e) => ShowWpfTouch("Down", e);
    private void OnTouchMove(object? sender, TouchEventArgs e) => ShowWpfTouch("Move", e);
    private void OnTouchUp(object? sender, TouchEventArgs e) => ShowWpfTouch("Up", e);

    private void OnStylusDown(object? sender, StylusDownEventArgs e)
    {
        if (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) return;
        _recognizer.PenDown(Environment.TickCount64);
        _gestureStatus.Text = "双指识别：手写笔按下，触摸已屏蔽";
    }

    private void OnStylusUp(object? sender, StylusEventArgs e)
    {
        if (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) return;
        _recognizer.PenUp(Environment.TickCount64);
        _gestureStatus.Text = "双指识别：手写笔抬起，进入 500ms 冷却";
    }

    private void ShowWpfTouch(string phase, TouchEventArgs e)
    {
        Point pixel = PointToScreen(e.GetTouchPoint(this).Position);
        long now = Environment.TickCount64;
        if (phase == "Down") _recognizer.TouchDown(e.TouchDevice.Id, pixel, now);
        else if (phase == "Move") _recognizer.TouchMove(e.TouchDevice.Id, pixel, now);
        else _recognizer.TouchUp(e.TouchDevice.Id);
        string line = $"WPF Touch ID={e.TouchDevice.Id} Phase={phase} "
                    + $"Position=({pixel.X:0}, {pixel.Y:0})";
        _motionStatus.Text = "轨迹事件：" + line;
        if (phase != "Move" || now - _lastTouchMoveLogTicks >= 100)
        {
            AppLogger.LogInfo("[touch-probe] " + line);
            if (phase == "Move") _lastTouchMoveLogTicks = now;
        }
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == RawDigitizerProbe.WmInput)
        {
            _rawDigitizerProbe?.OnRawInput(lParam, wParam);
            return 0;
        }
        if (message is not (WmPointerDown or WmPointerUpdate or WmPointerUp or WmPointerEnter or WmPointerLeave))
            return 0;

        uint id = (uint)((nuint)wParam & 0xffff);
        if (!GetPointerInfo(id, out PointerInfo info))
            return 0;

        if (info.PointerType is not (PointerInputType.Touch or PointerInputType.Pen))
            return 0;

        string phase = message switch
        {
            WmPointerDown => "Down",
            WmPointerUp => "Up",
            WmPointerEnter => "Enter",
            WmPointerLeave => "Leave",
            _ => "Update"
        };
        string line = $"Type={info.PointerType} ID={info.PointerId} Phase={phase} "
                    + $"Position=({info.PixelLocation.X}, {info.PixelLocation.Y}) "
                    + $"SourceDevice=0x{info.SourceDevice:X} Flags=0x{info.PointerFlags:X}";
        if (info.PointerType == PointerInputType.Touch)
        {
            // WPF can consume WM_POINTERDOWN/UPDATE before this HwndSource hook,
            // while ENTER/LEAVE still reach us. POINTER_INFO flags retain the
            // actual contact transition in those observed messages.
            if ((info.PointerFlags & PointerFlagUp) != 0 ||
                (info.PointerFlags & PointerFlagInContact) == 0)
                _activeTouches.Remove(info.PointerId);
            else if ((info.PointerFlags & (PointerFlagDown | PointerFlagInContact)) != 0)
                _activeTouches[info.PointerId] = info.PixelLocation;
        }

        string contacts = _activeTouches.Count == 0
            ? "无"
            : string.Join("，", _activeTouches.Select(pair => $"ID={pair.Key} ({pair.Value.X}, {pair.Value.Y})"));
        _status.Text = $"当前按下的 Touch 触点数={_activeTouches.Count}\n{contacts}\n\n最新事件：\n{line}";

        // Coalesced updates can be frequent. Keep the diagnostic log useful without
        // turning pointer movement into heavy synchronous disk activity.
        long now = Environment.TickCount64;
        if (message != WmPointerUpdate || now - _lastUpdateLogTicks >= 100)
        {
            AppLogger.LogInfo("[touch-probe] " + line);
            if (message == WmPointerUpdate) _lastUpdateLogTicks = now;
        }
        return 0; // Preserve normal WPF/Windows touch and pen processing.
    }

    private enum PointerInputType : uint { Pointer = 1, Touch = 2, Pen = 3, Mouse = 4, Touchpad = 5 }

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelPoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointerInfo
    {
        public PointerInputType PointerType;
        public uint PointerId;
        public uint FrameId;
        public uint PointerFlags;
        public nint SourceDevice;
        public nint HwndTarget;
        public PixelPoint PixelLocation;
        public PixelPoint HimetricLocation;
        public PixelPoint PixelLocationRaw;
        public PixelPoint HimetricLocationRaw;
        public uint Time;
        public uint HistoryCount;
        public int InputData;
        public uint KeyStates;
        public ulong PerformanceCount;
        public uint ButtonChangeType;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPointerInfo(uint pointerId, out PointerInfo pointerInfo);
}
