using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace WinPieGestures.Input;

/// <summary>
/// Global touchscreen input source. A native hidden HWND receives raw digitizer
/// reports without creating a WPF HwndSource or intercepting foreground input.
/// </summary>
internal sealed class TouchGestureProvider : IDisposable
{
    private const uint WsPopup = 0x80000000;
    private const uint WsExToolWindow = 0x00000080;
    private readonly WindowProcedure _windowProcedure;
    private readonly string _className;
    private readonly nint _module;
    private readonly int _ownerThreadId;
    private readonly RawDigitizerProbe _input;
    private nint _window;
    private ushort _classAtom;
    private bool _disposed;

    public event EventHandler<TouchGestureEventArgs>? GestureDetected;
    public event Action<Point, int>? TouchFrameChanged;
    public event Action? TouchFrameInvalidated;
    public event Action<bool>? PenPresenceChanged;

    public TouchGestureProvider(TouchGestureConfig? config = null)
    {
        _ownerThreadId = Environment.CurrentManagedThreadId;
        _windowProcedure = WndProc; // Keep the unmanaged callback alive through DestroyWindow.
        _className = $"StarPie.TouchInput.{Guid.NewGuid():N}";
        _module = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(_windowProcedure),
            Instance = _module,
            ClassName = _className
        };
        _classAtom = RegisterClassEx(ref windowClass);
        if (_classAtom == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Touch window class registration failed");

        _window = CreateWindowEx(WsExToolWindow, _className, "StarPie Touch Input",
            WsPopup, -32000, -32000, 1, 1, 0, 0, _module, 0);
        if (_window == 0)
        {
            int error = Marshal.GetLastWin32Error();
            UnregisterClass(_className, _module);
            _classAtom = 0;
            throw new Win32Exception(error, "Touch window creation failed");
        }

        try
        {
            _input = new RawDigitizerProbe(_window, detailedLog: false, config);
            _input.GestureDetected += ForwardGesture;
            _input.TouchFrameChanged += ForwardFrame;
            _input.TouchFrameInvalidated += ForwardInvalidFrame;
            _input.PenPresenceChanged += ForwardPen;
        }
        catch
        {
            DestroyWindow(_window);
            _window = 0;
            UnregisterClass(_className, _module);
            _classAtom = 0;
            throw;
        }
    }

    private void ForwardGesture(object? sender, TouchGestureEventArgs gesture) =>
        GestureDetected?.Invoke(this, gesture);
    private void ForwardFrame(Point center, int count) => TouchFrameChanged?.Invoke(center, count);
    private void ForwardInvalidFrame() => TouchFrameInvalidated?.Invoke();
    private void ForwardPen(bool present) => PenPresenceChanged?.Invoke(present);

    private nint WndProc(nint window, uint message, nint wParam, nint lParam)
    {
        if (message == RawDigitizerProbe.WmInput)
            _input?.OnRawInput(lParam, wParam);
        // DefWindowProc performs the required WM_INPUT cleanup for foreground reports.
        return DefWindowProc(window, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
            throw new InvalidOperationException("Touch input window must be disposed on its owner thread.");
        _disposed = true;
        _input.GestureDetected -= ForwardGesture;
        _input.TouchFrameChanged -= ForwardFrame;
        _input.TouchFrameInvalidated -= ForwardInvalidFrame;
        _input.PenPresenceChanged -= ForwardPen;
        try
        {
            _input.Dispose();
        }
        finally
        {
            if (_window != 0)
            {
                DestroyWindow(_window);
                _window = 0;
            }
            if (_classAtom != 0)
            {
                UnregisterClass(_className, _module);
                _classAtom = 0;
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint window, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public nint WindowProcedure;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        [MarshalAs(UnmanagedType.LPWStr)] public string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string ClassName;
        public nint SmallIcon;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);
    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string className, nint instance);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", SetLastError = true)]
    private static extern nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);
}
