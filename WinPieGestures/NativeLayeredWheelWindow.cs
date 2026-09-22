using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Gdi = System.Drawing;
using Gdi2D = System.Drawing.Drawing2D;

namespace WinPieGestures;

// Trial renderer for touch. Input selection and action execution remain in GestureController.
internal sealed class NativeLayeredWheelWindow : IWheelPresenter
{
    private const uint WsPopup = 0x80000000;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExTopmost = 0x00000008;
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
    private byte[] _pixels = [];
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

    public Dispatcher Dispatcher => Application.Current.Dispatcher;
    public Point ActualPhysicalCenter { get; private set; }
    public long PresentationVersion { get; private set; }

    public NativeLayeredWheelWindow()
    {
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
        double extent = Math.Max(cfg.WheelRadius + 100, cfg.SubWheelOuterRadius + 35);
        extent = Math.Max(extent, RadialWindow.GetFanExtentRadius(cfg.WheelRadius, cfg.InnerRadius) + 35);
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
    }

    public void Dismiss(long expectedPresentationVersion)
    {
        if (_disposed || expectedPresentationVersion != PresentationVersion) return;
        _presented = false;
        ShowWindow(_window, 0);
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
        _frame?.Dispose();
        if (_dib != 0)
        {
            SelectObject(_dc, _oldDib);
            DeleteObject(_dib);
            _dib = 0;
        }
        _size = size;
        _frame = new Gdi.Bitmap(size, size, PixelFormat.Format32bppPArgb);
        _pixels = new byte[size * size * 4];
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
    }

    private void DrawAndUpdate()
    {
        if (_frame == null || _profile == null) return;
        Draw(_frame);
        var rect = new Gdi.Rectangle(0, 0, _size, _size);
        BitmapData locked = _frame.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            Marshal.Copy(locked.Scan0, _pixels, 0, _pixels.Length);
            Marshal.Copy(_pixels, 0, _bits, _pixels.Length);
        }
        finally { _frame.UnlockBits(locked); }
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
        g.CompositingMode = Gdi2D.CompositingMode.SourceCopy;
        g.Clear(Gdi.Color.Transparent);
        g.CompositingMode = Gdi2D.CompositingMode.SourceOver;
        g.SmoothingMode = Gdi2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var cfg = ConfigManager.CurrentConfig;
        int count = Math.Clamp(_profile!.SectorCount, 4, 12);
        float cx = _size / 2f, cy = _size / 2f;
        float outer = (float)(cfg.WheelRadius * _scale);
        float inner = (float)(cfg.InnerRadius * _scale);
        float core = (float)(cfg.CoreRadius * _scale);
        using var normal = new Gdi.SolidBrush(Gdi.Color.FromArgb(221, 29, 43, 67));
        using var highlight = new Gdi.SolidBrush(Gdi.Color.FromArgb(245, 43, 117, 218));
        using var line = new Gdi.Pen(Gdi.Color.FromArgb(210, 160, 203, 250), Math.Max(1.2f, (float)_scale));
        using var center = new Gdi.SolidBrush(Gdi.Color.FromArgb(238, 14, 25, 45));
        using var white = new Gdi.SolidBrush(Gdi.Color.White);
        using var muted = new Gdi.SolidBrush(Gdi.Color.FromArgb(210, 201, 222, 246));
        using var font = new Gdi.Font("Microsoft YaHei UI", Math.Max(11, (float)(12 * _scale)), Gdi.FontStyle.Bold, Gdi.GraphicsUnit.Pixel);
        using var centerFont = new Gdi.Font("Microsoft YaHei UI", Math.Max(12, (float)(15 * _scale)), Gdi.FontStyle.Bold, Gdi.GraphicsUnit.Pixel);
        using var format = new Gdi.StringFormat { Alignment = Gdi.StringAlignment.Center, LineAlignment = Gdi.StringAlignment.Center,
            Trimming = Gdi.StringTrimming.EllipsisCharacter };
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float start = i * step - step / 2f + 1.5f;
            float sweep = step - 3f;
            using var path = new Gdi2D.GraphicsPath();
            path.AddArc(cx - outer, cy - outer, outer * 2, outer * 2, start, sweep);
            path.AddArc(cx - inner, cy - inner, inner * 2, inner * 2, start + sweep, -sweep);
            path.CloseFigure();
            g.FillPath(!_escaped && i == _sector ? highlight : normal, path);
            g.DrawPath(line, path);
            ActionItem? action = _profile.GetEffectiveAction(i);
            string label = action?.Name ?? string.Empty;
            float angle = i * step * MathF.PI / 180f;
            float textRadius = (inner + outer) / 2f;
            var textRect = new Gdi.RectangleF(cx + MathF.Cos(angle) * textRadius - 40 * (float)_scale,
                cy + MathF.Sin(angle) * textRadius - 18 * (float)_scale,
                80 * (float)_scale, 36 * (float)_scale);
            g.DrawString(label, font, white, textRect, format);
        }
        g.FillEllipse(center, cx - core, cy - core, core * 2, core * 2);
        g.DrawEllipse(line, cx - core, cy - core, core * 2, core * 2);
        string centerText = _volumePercent >= 0 ? $"{_volumePercent}%"
            : _escaped ? "取消" : _sector >= 0 ? (_profile.GetEffectiveAction(_sector, _subSector)?.Name ?? cfg.CoreTitle)
            : cfg.CoreTitle;
        g.DrawString(centerText, centerFont, white,
            new Gdi.RectangleF(cx - core + 5, cy - core + 5, 2 * core - 10, 2 * core - 10), format);
        if (cfg.EnableMultiTier && cfg.SubmenuStyle == "Wheel")
        {
            float subInner = outer + (float)((cfg.SubWheelInnerGap + 2) * _scale);
            float subOuter = (float)(cfg.SubWheelOuterRadius * _scale);
            if (subOuter > subInner + 8)
            {
                for (int parentIndex = 0; parentIndex < count; parentIndex++)
                {
                    if (!(_showSubTier && parentIndex == _sector) && !cfg.AutoExpandSubRingsOnPopup) continue;
                    ActionItem? parent = _profile.GetEffectiveAction(parentIndex);
                    int subCount = parent?.SubActions?.Count ?? 0;
                    if (subCount == 0) continue;
                    float subStep = step / subCount;
                    for (int i = 0; i < subCount; i++)
                    {
                        float start = parentIndex * step - step / 2 + i * subStep + 0.8f;
                        float sweep = subStep - 1.6f;
                        using var path = new Gdi2D.GraphicsPath();
                        path.AddArc(cx - subOuter, cy - subOuter, subOuter * 2, subOuter * 2, start, sweep);
                        path.AddArc(cx - subInner, cy - subInner, subInner * 2, subInner * 2, start + sweep, -sweep);
                        path.CloseFigure();
                        g.FillPath(!_escaped && parentIndex == _sector && i == _subSector ? highlight : normal, path);
                        g.DrawPath(line, path);
                        float angle = (start + sweep / 2) * MathF.PI / 180f;
                        float radius = (subInner + subOuter) / 2;
                        string label = _profile.GetEffectiveAction(parentIndex, i)?.Name ?? string.Empty;
                        g.DrawString(label, font, muted,
                            new Gdi.RectangleF(cx + MathF.Cos(angle) * radius - 35 * (float)_scale,
                                cy + MathF.Sin(angle) * radius - 14 * (float)_scale,
                                70 * (float)_scale, 28 * (float)_scale), format);
                    }
                }
            }
        }
        else if (cfg.EnableMultiTier && cfg.SubmenuStyle == "Fan" && _showSubTier && _sector >= 0)
        {
            ActionItem? parent = _profile.GetEffectiveAction(_sector);
            int subCount = Math.Min(parent?.SubActions?.Count ?? 0, RadialWindow.FanSubmenuSlotCount);
            float angle = _sector * step * MathF.PI / 180f;
            float ux = MathF.Cos(angle), uy = MathF.Sin(angle);
            float vx = -uy, vy = ux;
            float radius = (inner + outer) / 2;
            for (int i = 0; i < subCount; i++)
            {
                int slot = RadialWindow.GetFanSlotIndex(i, subCount);
                var (du, dv) = RadialWindow.GetFanSubOffsetForShape(cfg.Shape, slot);
                float x = cx + (float)(ux * du + vx * dv) * radius;
                float y = cy + (float)(uy * du + vy * dv) * radius;
                float subRadius = Math.Max(22, (outer - inner) * 0.43f);
                g.FillEllipse(!_escaped && i == _subSector ? highlight : normal,
                    x - subRadius, y - subRadius, 2 * subRadius, 2 * subRadius);
                g.DrawEllipse(line, x - subRadius, y - subRadius, 2 * subRadius, 2 * subRadius);
                g.DrawString(_profile.GetEffectiveAction(_sector, i)?.Name ?? string.Empty, font, muted,
                    new Gdi.RectangleF(x - subRadius + 2, y - subRadius + 2, 2 * subRadius - 4, 2 * subRadius - 4), format);
            }
        }
    }

    public void CloseFast()
    {
        if (_disposed) return;
        _disposed = true;
        _presented = false;
        _frame?.Dispose();
        _frame = null;
        if (_dib != 0)
        {
            SelectObject(_dc, _oldDib);
            DeleteObject(_dib);
            _dib = 0;
        }
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
    [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)] private static extern bool UpdateLayeredWindow(nint hwnd, nint destDc, ref NativePoint dest, ref NativeSize size, nint srcDc, ref NativePoint src, uint key, ref BlendFunction blend, uint flags);
    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll", EntryPoint = "SelectObject")] private static extern nint SelectObject(nint dc, nint bitmap);
    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll", EntryPoint = "DeleteDC")] private static extern bool DeleteDC(nint dc);
}
