using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Gdi = System.Drawing;
using Gdi2D = System.Drawing.Drawing2D;

namespace WinPieGestures;

// Native touch presenter. Input selection and action execution remain in GestureController.
internal sealed class NativeLayeredWheelWindow : IWheelPresenter
{
    private const uint WsPopup = 0x80000000;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExTopmost = 0x00000008;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private static readonly nint HwndTopmost = new(-1);
    private const uint UlwAlpha = 0x00000002;
    private const int WmNcHitTest = 0x0084;
    private static readonly WindowProcedure Callback = WndProc;

    private readonly string _className = $"StarPie.SoftwareWheel.{Guid.NewGuid():N}";
    private readonly nint _module;
    private nint _window;
    private nint _dc;
    private nint _dib;
    private nint _oldDib;
    private nint _bits;
    private Gdi.Bitmap? _frame;
    private NativeWheelScene? _scene;
    private WheelProfile? _sceneProfile;
    private long _configurationRevision = -1, _sceneRevision = -2;
    private int _sceneLayer = -1;
    private double _sceneScale;
    private readonly DispatcherTimer _idleRelease;
    private int _size;
    private double _scale = 1;
    private WheelProfile? _profile;
    private int _sector = -1;
    private int _subSector = -1;
    private bool _showSubTier;
    private bool _escaped;
    private int _volumePercent = -1;
    private bool _presented;
    private bool _disposed;

    internal long SurfaceBytes => _frame == null ? 0 : (long)_size * _size * 4;
    public Dispatcher Dispatcher => Application.Current.Dispatcher;
    public Point ActualPhysicalCenter { get; private set; }
    public long PresentationVersion { get; private set; }

    public NativeLayeredWheelWindow()
    {
        _idleRelease = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher)
        { Interval = TimeSpan.FromSeconds(30) };
        _idleRelease.Tick += IdleRelease_Tick;
        _module = GetModuleHandle(null);
        var cls = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Procedure = Marshal.GetFunctionPointerForDelegate(Callback),
            Instance = _module,
            ClassName = _className
        };
        if (RegisterClassEx(ref cls) == 0) ThrowLastError();
        try
        {
            _window = CreateWindowEx(WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExTopmost,
                _className, "StarPie Software Wheel", WsPopup, 0, 0, 1, 1, 0, 0, _module, 0);
            if (_window == 0) ThrowLastError();
            _dc = CreateCompatibleDC(0);
            if (_dc == 0) ThrowLastError();
        }
        catch
        {
            CloseFast();
            throw;
        }
    }

    public void Present(Point center, WheelProfile profile, long configurationRevision, long presentationVersion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _idleRelease.Stop();
        _configurationRevision = configurationRevision;
        _profile = profile;
        _sector = -1;
        _subSector = -1;
        _showSubTier = false;
        _escaped = false;
        _volumePercent = -1;
        PresentationVersion = presentationVersion;
        var cfg = ConfigManager.CurrentConfig;
        var (sx, sy) = RadialWindow.GetMonitorDpiScale(center);
        _scale = Math.Clamp(Math.Max(sx, sy), 0.5, 4);
        Gdi.Rectangle bounds = System.Windows.Forms.Screen.FromPoint(
            new Gdi.Point((int)Math.Round(center.X), (int)Math.Round(center.Y))).Bounds;
        double extent = cfg.WheelRadius + 18;
        bool hasSubActions = cfg.EnableMultiTier && Enumerable.Range(0, profile.SectorCount)
            .Any(i => profile.GetEffectiveAction(i)?.SubActions?.Count > 0);
        if (hasSubActions)
            extent = Math.Max(extent, cfg.SubmenuStyle == "Fan"
                ? RadialWindow.GetFanExtentRadius(cfg.WheelRadius, cfg.InnerRadius) + 12
                : cfg.SubWheelOuterRadius + 12);
        int size = (int)Math.Clamp(Math.Ceiling(extent * _scale * 2), 320, 1600);
        int available = Math.Min(bounds.Width, bounds.Height);
        if (size > available)
        {
            _scale *= (double)available / size;
            size = available;
        }
        EnsureSurface(size);
        double x = Math.Clamp(center.X, bounds.Left + size / 2.0, bounds.Right - size / 2.0);
        double y = Math.Clamp(center.Y, bounds.Top + size / 2.0, bounds.Bottom - size / 2.0);
        ActualPhysicalCenter = new Point(x, y);
        _presented = true;
        DrawAndUpdate();
        ShowWindow(_window, 4); // SW_SHOWNOACTIVATE
        // This HWND is reused after being hidden. WS_EX_TOPMOST at creation does
        // not bring a reused window back above newer topmost windows. Raise it
        // on every presentation without taking focus from the foreground app.
        if (!SetWindowPos(_window, HwndTopmost, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate))
            ThrowLastError();
    }

    public void Dismiss(long expectedPresentationVersion)
    {
        if (_disposed || expectedPresentationVersion != PresentationVersion) return;
        _presented = false;
        ShowWindow(_window, 0);
        _idleRelease.Stop();
        _idleRelease.Start();
    }

    public void HighlightSector(int mainIndex, int subIndex, bool showSubTier)
    {
        if (_sector == mainIndex && _subSector == subIndex && _showSubTier == showSubTier) return;
        _sector = mainIndex;
        _subSector = subIndex;
        _showSubTier = showSubTier;
        if (_presented) DrawAndUpdate();
    }

    public void SetOuterEscapeState(bool escaped)
    {
        if (_escaped == escaped) return;
        _escaped = escaped;
        if (_presented) DrawAndUpdate();
    }

    public void SetVolumePreview(int percent, bool isActive)
    {
        int next = isActive ? percent : -1;
        if (_volumePercent == next) return;
        _volumePercent = next;
        if (_presented) DrawAndUpdate();
    }

    public void SwitchToLayer(int layerIndex)
    {
        if (_profile?.Layers == null || layerIndex < 0 || layerIndex >= _profile.Layers.Count) return;
        _profile.ActiveLayerIndex = layerIndex;
        _profile.SyncRootPropertiesFromActiveLayer();
        if (_presented) DrawAndUpdate();
    }

    private void EnsureSurface(int size)
    {
        if (_size == size && _frame != null) return;
        ReleaseSurface();
        _size = size;

        var info = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = size,
                Height = -size,
                Planes = 1,
                BitCount = 32
            }
        };
        _dib = CreateDIBSection(_dc, ref info, 0, out _bits, 0, 0);
        if (_dib == 0 || _bits == 0) ThrowLastError();
        _oldDib = SelectObject(_dc, _dib);
        // The bitmap wraps the DIB itself: one premultiplied surface, zero
        // managed pixel array and zero full-frame copies before presentation.
        _frame = new Gdi.Bitmap(size, size, size * 4, PixelFormat.Format32bppPArgb, _bits);
    }

    private void DrawAndUpdate()
    {
        if (_frame == null || _profile == null) return;
        Draw(_frame);
        var position = new NativePoint((int)Math.Round(ActualPhysicalCenter.X) - _size / 2,
            (int)Math.Round(ActualPhysicalCenter.Y) - _size / 2);
        var origin = new NativePoint(0, 0);
        var size = new NativeSize(_size, _size);
        var blend = new BlendFunction(0, 0, 255, 1);
        if (!UpdateLayeredWindow(_window, 0, ref position, ref size, _dc, ref origin,
            0, ref blend, UlwAlpha)) ThrowLastError();
    }

    private void Draw(Gdi.Bitmap bitmap)
    {
        using Gdi.Graphics g = Gdi.Graphics.FromImage(bitmap);
        g.SmoothingMode = Gdi2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        if (_scene == null || !ReferenceEquals(_sceneProfile, _profile)
            || _sceneRevision != _configurationRevision || _sceneLayer != _profile!.ActiveLayerIndex
            || _sceneScale != _scale)
        {
            _scene?.Dispose();
            _scene = null;
            _scene = new NativeWheelScene(ConfigManager.CurrentConfig, _profile!, _size, _scale);
            _sceneProfile = _profile;
            _sceneRevision = _configurationRevision;
            _sceneLayer = _profile!.ActiveLayerIndex;
            _sceneScale = _scale;
        }
        _scene.Draw(g, _sector, _subSector, _showSubTier, _escaped, _volumePercent);
    }

    private void IdleRelease_Tick(object? sender, EventArgs e)
    {
        _idleRelease.Stop();
        if (!_presented && !_disposed) ReleaseSurface();
    }

    private void ReleaseSurface()
    {
        _scene?.Dispose();
        _scene = null;
        _sceneProfile = null;
        _frame?.Dispose(); // Release wrapper before freeing its native pixels.
        _frame = null;
        if (_dib != 0)
        {
            SelectObject(_dc, _oldDib);
            DeleteObject(_dib);
            _dib = 0;
        }
        _bits = 0;
        _size = 0;
    }

    public void CloseFast()
    {
        if (_disposed) return;
        _disposed = true;
        _presented = false;
        _idleRelease.Stop();
        _idleRelease.Tick -= IdleRelease_Tick;
        ReleaseSurface();
        if (_dc != 0) DeleteDC(_dc);
        if (_window != 0) DestroyWindow(_window);
        UnregisterClass(_className, _module);
    }

    private static void ThrowLastError() => throw new Win32Exception(Marshal.GetLastWin32Error());
    private static nint WndProc(nint hwnd, uint message, nint wParam, nint lParam) =>
        message == WmNcHitTest ? -1 : DefWindowProc(hwnd, message, wParam, lParam);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    {
        public uint Size, Style;
        public nint Procedure;
        public int ClassExtraBytes, WindowExtraBytes;
        public nint Instance, Icon, Cursor, Background;
        [MarshalAs(UnmanagedType.LPWStr)] public string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string ClassName;
        public nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private record struct NativeSize(int Width, int Height);
    [StructLayout(LayoutKind.Sequential)] private record struct BlendFunction(byte BlendOp, byte BlendFlags, byte SourceConstantAlpha, byte AlphaFormat);
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, ImageSize;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ColorsUsed, ColorsImportant;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public BitmapInfoHeader Header; public uint Colors; }
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? module);
    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass cls);
    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string cls, nint module);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint module, nint param);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "DestroyWindow")] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "ShowWindow")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)] private static extern bool UpdateLayeredWindow(nint hwnd, nint destDc, ref NativePoint dest, ref NativeSize size, nint srcDc, ref NativePoint src, uint key, ref BlendFunction blend, uint flags);
    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll", EntryPoint = "SelectObject")] private static extern nint SelectObject(nint dc, nint bitmap);
    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll", EntryPoint = "DeleteDC")] private static extern bool DeleteDC(nint dc);
}
