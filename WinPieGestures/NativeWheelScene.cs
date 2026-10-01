using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gdi = System.Drawing;
using Gdi2D = System.Drawing.Drawing2D;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace WinPieGestures;

// A disposable, configuration-sized scene. Geometry and icon lookup reuse the
// original wheel's definitions without constructing WPF visuals or using D3D.
// Only selection/state changes repaint; no animation or background render loop.
internal sealed class NativeWheelScene : IDisposable
{
    private readonly List<IDisposable> _resources = new();
    private readonly Dictionary<(string, float, bool), Gdi.Font> _fonts = new();
    private readonly List<Slot> _slots = new();
    private readonly Palette _main, _sub;
    private readonly AppConfig _config;
    private readonly WheelProfile _profile;
    private readonly float _center, _scale;
    private readonly Gdi.StringFormat _format;
    private readonly Gdi.Font _coreFont, _subtitleFont;
    private readonly Gdi.SolidBrush _white, _coreText;
    private readonly Gdi.Pen _orbit;
    private readonly Gdi2D.GraphicsPath? _coreIcon;
    private readonly Gdi.Bitmap? _coreImage;
    private readonly Gdi.RectangleF _coreRect;
    private readonly Gdi.RectangleF _coreImageRect;
    private readonly Gdi2D.GraphicsPath _coreClip;
    private readonly string _cancelText;
    private readonly string _subtitle;
    private readonly float _surfaceExtent;
    private bool _hasDrawn, _lastShowSub, _lastEscaped;
    private int _lastSelected = -1, _lastSubIndex = -1;

    private sealed record Palette(Gdi.SolidBrush Normal, Gdi.SolidBrush Highlight,
        Gdi.SolidBrush Text, Gdi.SolidBrush Core, Gdi.Pen Border, Gdi.Pen SelectedBorder,
        Gdi.Pen CoreBorder, Gdi.Pen Halo);
    private sealed record Slot(int Parent, int SubIndex, Gdi2D.GraphicsPath Shape,
        Gdi2D.GraphicsPath? Icon, Gdi.Bitmap? Image, Gdi.RectangleF IconRect,
        string Name, string Label, Gdi.RectangleF TextRect, Gdi.Font Font, Gdi.SolidBrush Text, Palette Palette,
        Gdi.RectangleF Bounds);

    public NativeWheelScene(AppConfig config, WheelProfile profile, int size, double scale)
    {
        _config = config; _profile = profile; _scale = (float)scale;
        _center = size / (2 * _scale);
        _surfaceExtent = size / _scale;
        try
        {
            // Normalize root properties before reading the sector count. A
            // freshly created layered profile may still carry default roots.
            _profile.EnsureLayers();
            _main = CreatePalette(config.UiStyle, config.Theme, false);
            string subStyle = Follow(config.SubWheelUiStyle, config.UiStyle);
            string subTheme = Follow(config.SubWheelTheme, config.Theme);
            _sub = !config.UseIndependentSubWheelTheme && subStyle == config.UiStyle && subTheme == config.Theme
                ? _main : CreatePalette(subStyle, subTheme, true);
            _white = Own(new Gdi.SolidBrush(Gdi.Color.White));
            _coreText = Own(new Gdi.SolidBrush(config.CoreTextColorAuto ? _main.Text.Color : ParseColor(config.CoreTextColor, _main.Text.Color)));
            _format = Own(new Gdi.StringFormat { Alignment = Gdi.StringAlignment.Center,
                LineAlignment = Gdi.StringAlignment.Center, Trimming = Gdi.StringTrimming.EllipsisCharacter });
            _coreFont = Font(config.CoreFontFamily, config.CoreFontSize, true);
            _subtitleFont = Font(config.CoreFontFamily, Math.Max(7, config.CoreFontSize * .65), false);
            _orbit = Own(new Gdi.Pen(Gdi.Color.FromArgb(45, _main.Text.Color), .7f) { DashPattern = [2, 5] });
            float cr = (float)config.CoreRadius;
            _coreRect = new Gdi.RectangleF(_center - cr, _center - cr, cr * 2, cr * 2);
            _coreClip = Own(new Gdi2D.GraphicsPath()); _coreClip.AddEllipse(_coreRect);
            _cancelText = I18n.T("BtnCancel");
            _subtitle = config.CoreSubtitle == "RMB Drag" ? I18n.T("TouchWheelSubtitle") : config.CoreSubtitle;
            if (config.ShowCoreIcon)
            {
                _coreImage = Raster(IconHelper.GetCachedCustomImage(config.CoreCustomImagePath), 256);
                float iconScale = (float)Math.Clamp(config.CoreIconScale, .1, 4);
                if (_coreImage != null)
                {
                    float s = config.CoreCustomImageStretch == "Uniform"
                        ? Math.Min(_coreRect.Width / _coreImage.Width, _coreRect.Height / _coreImage.Height)
                        : Math.Max(_coreRect.Width / _coreImage.Width, _coreRect.Height / _coreImage.Height);
                    float width = _coreImage.Width * s * iconScale, height = _coreImage.Height * s * iconScale;
                    _coreImageRect = new Gdi.RectangleF(_center - width / 2 + (float)config.CoreImageOffsetX,
                        _center - height / 2 + (float)config.CoreImageOffsetY, width, height);
                }
                if (_coreImage == null)
                {
                    string? data = !string.IsNullOrWhiteSpace(config.CoreCustomIconSvg) ? config.CoreCustomIconSvg
                        : IconHelper.GetCachedSvgPathByKey(config.CoreCustomIconKey);
                    Geometry geometry;
                    try { geometry = !string.IsNullOrWhiteSpace(data) ? Geometry.Parse(data)
                        : IconHelper.GetCoreIconGeometry(config.CoreIconType); }
                    catch (FormatException) { geometry = IconHelper.GetCoreIconGeometry("Exit"); }
                    _coreIcon = Fit(geometry, new Gdi.RectangleF(_center - cr * .55f * iconScale,
                        _center - cr * .55f * iconScale, cr * 1.1f * iconScale, cr * 1.1f * iconScale));
                }
            }
            BuildSlots();
        }
        catch { Dispose(); throw; }
    }

    private static string Follow(string? value, string primary) => string.IsNullOrEmpty(value) || value == "FollowPrimary" ? primary : value;
    private T Own<T>(T item) where T : IDisposable { _resources.Add(item); return item; }

    private Palette CreatePalette(string style, string theme, bool sub)
    {
        var renderer = StyleRendererFactory.CreateRenderer(style);
        renderer.Initialize(theme, _config);
        Gdi.Color Color(Brush brush) => brush is SolidColorBrush b
            ? Gdi.Color.FromArgb(b.Color.A, b.Color.R, b.Color.G, b.Color.B) : Gdi.Color.White;
        Gdi.Color normal = Color(renderer.DefaultSectorBrush), border = Color(renderer.SectorBorderBrush);
        Gdi.Color highlight = Color(renderer.HighlightSectorBrush), selectedBorder = Color(renderer.HighlightBorderBrush), text = Color(renderer.TextColorBrush);
        if (sub && theme == "Custom")
        {
            normal = ParseColor(_config.SubWheelCustomSectorBg, normal);
            border = ParseColor(_config.SubWheelCustomSectorBorder, border);
            highlight = ParseColor(_config.SubWheelCustomHighlightBg, highlight);
            selectedBorder = ParseColor(_config.SubWheelCustomHighlightBorder, selectedBorder);
            text = ParseColor(_config.SubWheelCustomText, text);
        }
        return new Palette(Own(new Gdi.SolidBrush(normal)), Own(new Gdi.SolidBrush(highlight)),
            Own(new Gdi.SolidBrush(text)), Own(new Gdi.SolidBrush(Color(renderer.CoreBgBrush))),
            Own(new Gdi.Pen(border, (float)renderer.BorderThickness)),
            Own(new Gdi.Pen(selectedBorder, (float)renderer.HighlightBorderThickness)),
            Own(new Gdi.Pen(Color(renderer.CoreBorderBrush), 1)),
            Own(new Gdi.Pen(Gdi.Color.FromArgb(28, selectedBorder), 4) { LineJoin = Gdi2D.LineJoin.Round }));
    }

    private void BuildSlots()
    {
        int count = Math.Clamp(_profile.SectorCount, 4, 12);
        double step = 360d / count, radius = (_config.WheelRadius + _config.InnerRadius) / 2;
        for (int i = 0; i < count; i++)
        {
            double angle = i * step * Math.PI / 180;
            var shape = IconHelper.CreateAdvancedSectorGeometry(_center, _center, i * step - step / 2, i * step + step / 2,
                _config.InnerRadius, _config.WheelRadius, _config.Shape, _config.SectorGap, _config.SectorCornerRadius);
            AddSlot(i, -1, shape, _center + Math.Cos(angle) * radius, _center + Math.Sin(angle) * radius, _main);
            if (!_config.EnableMultiTier) continue;
            int subCount = _profile.GetEffectiveAction(i)?.SubActions?.Count ?? 0;
            if (_config.SubmenuStyle == "Fan") subCount = Math.Min(subCount, RadialWindow.FanSubmenuSlotCount);
            for (int j = 0; j < subCount; j++)
            {
                double x, y; Geometry subShape;
                if (_config.SubmenuStyle == "Fan")
                {
                    var (du, dv) = RadialWindow.GetFanSubOffsetForShape(_config.Shape, RadialWindow.GetFanSlotIndex(j, subCount));
                    x = _center + (Math.Cos(angle) * du - Math.Sin(angle) * dv) * radius;
                    y = _center + (Math.Sin(angle) * du + Math.Cos(angle) * dv) * radius;
                    subShape = RadialWindow.CreateSubMenuGeometry(_config.Shape, x, y,
                        (_config.WheelRadius - _config.InnerRadius) * .40, angle, _center, _center, _config.SubWheelCornerRadius);
                }
                else
                {
                    double start = i * step - step / 2 + j * step / subCount, end = start + step / subCount;
                    double inner = _config.WheelRadius + _config.SubWheelInnerGap + 2, outer = _config.SubWheelOuterRadius;
                    if (outer <= inner + 8) break;
                    double a = (start + end) / 2 * Math.PI / 180;
                    x = _center + Math.Cos(a) * (inner + outer) / 2; y = _center + Math.Sin(a) * (inner + outer) / 2;
                    subShape = IconHelper.CreateAdvancedSectorGeometry(_center, _center, start, end, inner, outer,
                        _config.Shape, _config.SectorGap, _config.SubWheelCornerRadius);
                }
                AddSlot(i, j, subShape, x, y, _sub);
            }
        }
    }

    private void AddSlot(int parent, int sub, Geometry shape, double x, double y, Palette palette)
    {
        var action = _profile.GetEffectiveAction(parent, sub);
        bool secondary = sub >= 0;
        string layout = action?.LayoutMode is not (null or "" or "Inherit") ? action.LayoutMode : _config.IconLayoutMode;
        bool showIcon = layout != "TextOnly", showText = layout != "IconOnly";
        double factor = secondary ? 1 : _profile.SectorCount == 4 ? 1.2 : _profile.SectorCount == 12 ? .82 : 1;
        double iconSize = (action?.CustomIconSize is > 0 ? action.CustomIconSize.Value
            : secondary ? _config.SubWheelIconSize : _config.SectorIconSize) * factor * (layout == "IconOnly" ? 1.35 : 1);
        float fs = (float)(action?.CustomFontSize is > 0 ? action.CustomFontSize.Value : secondary ? _config.SubWheelFontSize : _config.SectorFontSize);
        if (!secondary && _profile.SectorCount == 12) fs = Math.Min(fs, 10);
        fs = Math.Clamp(fs, 6, 64);
        float iw = (float)Math.Clamp(iconSize, 8, 128), textHeight = fs * 2.6f;
        bool above = (action?.CustomTextPlacement is not (null or "" or "Inherit") ? action.CustomTextPlacement : _config.SectorTextPlacement) == "Above";
        float iconY = (float)y - iw / 2 + (showText ? (above ? fs * .6f : -fs * .6f) : 0);
        var iconRect = new Gdi.RectangleF((float)x - iw / 2, iconY, iw, iw);
        float textY = showIcon ? above ? iconY - textHeight - 2 : iconY + iw + 2 : (float)y - textHeight / 2;
        float tw = secondary ? 70 : _profile.SectorCount == 12 ? 66 : 92;
        var textRect = new Gdi.RectangleF((float)x - tw / 2 + (float)(action?.CustomTextOffsetX ?? _config.SectorTextOffsetX),
            textY + (float)(action?.CustomTextOffsetY ?? _config.SectorTextOffsetY), tw, textHeight);
        Gdi2D.GraphicsPath? icon = null; Gdi.Bitmap? image = null;
        if (showIcon)
        {
            string? data = !string.IsNullOrWhiteSpace(action?.CustomIconSvg) ? action.CustomIconSvg : null;
            if (data == null) image = Raster(IconHelper.GetCachedIcon(action?.InheritAppIconPath), 128);
            if (image == null && data == null)
            {
                data = IconHelper.GetCachedSvgPathByKey(action?.IconKey);
                if (data == null) image = Raster(IconHelper.GetCachedCustomImage(action?.IconKey), 128);
            }
            if (image == null && data == null && action?.Type is "Launch" or "App") image = Raster(IconHelper.GetCachedIcon(action.Parameter), 128);
            data ??= IconHelper.GetCachedSvgPathByKey(action?.Type)
                ?? "M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z";
            if (image == null && !string.IsNullOrWhiteSpace(data))
            {
                try { icon = Fit(Geometry.Parse(data), iconRect); }
                catch (FormatException) { /* Invalid user/plugin SVG never breaks presentation. */ }
            }
        }
        var textBrush = string.IsNullOrWhiteSpace(action?.CustomTextColor) ? palette.Text
            : Own(new Gdi.SolidBrush(ParseColor(action.CustomTextColor, palette.Text.Color)));
        string label = showText ? SectorTextFormatter.FormatSectorText(action?.Name ?? "", _profile.SectorCount) : "";
        var path = Path(shape);
        var bounds = path.GetBounds(); bounds.Inflate(4, 4);
        bounds = Gdi.RectangleF.Union(bounds, iconRect);
        if (label.Length != 0) bounds = Gdi.RectangleF.Union(bounds, textRect);
        _slots.Add(new Slot(parent, sub, path, icon, image, iconRect, action?.Name ?? "", label, textRect,
            Font(action?.CustomFontFamily ?? _config.WheelFontFamily, fs, false), textBrush, palette, bounds));
    }

    private Gdi.Font Font(string? families, double size, bool bold)
    {
        string family = string.IsNullOrWhiteSpace(families) ? _config.WheelFontFamily : families;
        float px = (float)Math.Clamp(size, 6, 64);
        var key = (family, px, bold);
        if (_fonts.TryGetValue(key, out var cached)) return cached;
        var font = Own(new Gdi.Font(family.Split(',')[0].Trim(), px, bold ? Gdi.FontStyle.Bold : Gdi.FontStyle.Regular, Gdi.GraphicsUnit.Pixel));
        _fonts[key] = font; return font;
    }

    private Gdi2D.GraphicsPath Path(Geometry geometry)
    {
        if (geometry.CanFreeze && !geometry.IsFrozen) geometry.Freeze();
        var flat = geometry.GetFlattenedPathGeometry(.20 / _scale, ToleranceType.Absolute);
        var result = Own(new Gdi2D.GraphicsPath(flat.FillRule == FillRule.Nonzero ? Gdi2D.FillMode.Winding : Gdi2D.FillMode.Alternate));
        var transform = flat.Transform?.Value ?? Matrix.Identity;
        foreach (var figure in flat.Figures)
        {
            var points = new List<Gdi.PointF>();
            void Add(System.Windows.Point p) { p = transform.Transform(p); points.Add(new Gdi.PointF((float)p.X, (float)p.Y)); }
            Add(figure.StartPoint);
            foreach (var segment in figure.Segments)
            {
                if (segment is PolyLineSegment line) foreach (var p in line.Points) Add(p);
                else if (segment is LineSegment single) Add(single.Point);
            }
            if (points.Count < 2) continue;
            result.StartFigure(); result.AddLines(points.ToArray());
            if (figure.IsClosed) result.CloseFigure();
        }
        return result;
    }

    private Gdi2D.GraphicsPath Fit(Geometry geometry, Gdi.RectangleF target)
    {
        var path = Path(geometry); var bounds = path.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        float s = Math.Min(target.Width / bounds.Width, target.Height / bounds.Height);
        using var matrix = new Gdi2D.Matrix(s, 0, 0, s,
            target.X + (target.Width - bounds.Width * s) / 2 - bounds.X * s,
            target.Y + (target.Height - bounds.Height * s) / 2 - bounds.Y * s);
        path.Transform(matrix); return path;
    }

    private Gdi.Bitmap? Raster(BitmapSource? source, int maximum)
    {
        if (source == null) return null;
        double scale = Math.Min(1, (double)maximum / Math.Max(source.PixelWidth, source.PixelHeight));
        if (scale < 1) source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        source = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var bitmap = Own(new Gdi.Bitmap(source.PixelWidth, source.PixelHeight, PixelFormat.Format32bppPArgb));
        var data = bitmap.LockBits(new Gdi.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        try { source.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Stride * bitmap.Height, data.Stride); }
        finally { bitmap.UnlockBits(data); }
        return bitmap;
    }

    internal void Draw(Gdi.Graphics g, int selected, int subIndex, bool showSub, bool escaped, int volume)
    {
        g.ScaleTransform(_scale, _scale);
        // Keep the one DIB as the retained frame. Ordinary selection changes
        // erase/repaint only the old/new slots and core, including their halo.
        // Tier visibility changes require a full repaint, but no second bitmap.
        bool full = !_hasDrawn || showSub != _lastShowSub || (showSub && selected != _lastSelected);
        using var dirty = new Gdi.Region(new Gdi.RectangleF(0, 0, _surfaceExtent, _surfaceExtent));
        if (!full)
        {
            var core = _coreRect; core.Inflate(3, 3);
            dirty.MakeEmpty(); dirty.Union(core);
            if (selected != _lastSelected || subIndex != _lastSubIndex || escaped != _lastEscaped)
                foreach (var slot in _slots)
                    if (slot.Parent == selected || slot.Parent == _lastSelected) dirty.Union(slot.Bounds);
        }
        g.SetClip(dirty, Gdi2D.CombineMode.Replace);
        g.CompositingMode = Gdi2D.CompositingMode.SourceCopy;
        g.FillRectangle(Gdi.Brushes.Transparent, 0, 0, _surfaceExtent, _surfaceExtent);
        g.CompositingMode = Gdi2D.CompositingMode.SourceOver;
        if (_config.UiStyle == "ClassicRing")
        {
            float radius = (float)_config.WheelRadius + 8;
            g.DrawEllipse(_orbit, _center - radius, _center - radius, radius * 2, radius * 2);
        }
        foreach (var slot in _slots)
        {
            if (slot.SubIndex >= 0 && !(showSub && slot.Parent == selected)
                && !(_config.SubmenuStyle == "Wheel" && _config.AutoExpandSubRingsOnPopup)) continue;
            if (!full && !g.IsVisible(slot.Bounds)) continue;
            bool active = !escaped && slot.Parent == selected && (slot.SubIndex < 0 || slot.SubIndex == subIndex);
            var p = slot.Palette;
            if (active) g.DrawPath(p.Halo, slot.Shape);
            g.FillPath(active ? p.Highlight : p.Normal, slot.Shape);
            g.DrawPath(active ? p.SelectedBorder : p.Border, slot.Shape);
            var foreground = active ? _white : slot.Text;
            if (slot.Image != null) g.DrawImage(slot.Image, slot.IconRect);
            else if (slot.Icon != null) g.FillPath(foreground, slot.Icon);
            if (slot.Label.Length != 0) g.DrawString(slot.Label, slot.Font, foreground, slot.TextRect, _format);
        }
        g.FillEllipse(_main.Core, _coreRect); g.DrawEllipse(_main.CoreBorder, _coreRect);
        bool preview = volume >= 0 || escaped || selected >= 0;
        if (!preview && (_coreImage != null || _coreIcon != null))
        {
            var state = g.Save();
            g.SetClip(_coreClip, Gdi2D.CombineMode.Intersect);
            if (_coreImage != null) g.DrawImage(_coreImage, _coreImageRect);
            else g.FillPath(_coreText, _coreIcon!);
            g.Restore(state);
        }
        else
        {
            string title = _config.CoreTitle;
            if (selected >= 0)
                foreach (var slot in _slots)
                    if (slot.Parent == selected && slot.SubIndex == subIndex) { title = slot.Name; break; }
            if (volume >= 0) title = $"{volume}%";
            else if (escaped) title = _cancelText;
            var rect = _coreRect; rect.Inflate(-4, -4);
            if (!preview && !string.IsNullOrEmpty(_subtitle))
            {
                rect.Height *= .6f;
                g.DrawString(_subtitle, _subtitleFont, _coreText,
                    new Gdi.RectangleF(rect.X, rect.Bottom, rect.Width, _coreRect.Height * .25f), _format);
            }
            g.DrawString(title, _coreFont, _coreText, rect, _format);
        }
        _hasDrawn = true; _lastSelected = selected; _lastSubIndex = subIndex;
        _lastShowSub = showSub; _lastEscaped = escaped;
    }

    private static Gdi.Color ParseColor(string? text, Gdi.Color fallback)
    {
        try { var c = (Color)ColorConverter.ConvertFromString(text!); return Gdi.Color.FromArgb(c.A, c.R, c.G, c.B); }
        catch { return fallback; }
    }

    public void Dispose()
    {
        for (int i = _resources.Count - 1; i >= 0; i--) _resources[i].Dispose();
        _resources.Clear(); _fonts.Clear(); _slots.Clear();
    }
}
