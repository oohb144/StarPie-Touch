using System;
using System.Windows;
using System.Windows.Media;
using Pen = System.Windows.Media.Pen;

namespace WinPieGestures
{
    /// <summary>
    /// Compact radial position cue used by the sector-action mapping list.
    /// The selected sector is filled with the current accent color and carries
    /// a larger directional arrow so the fixed destination slot is obvious
    /// even when the action cards are reordered.
    /// </summary>
    public sealed class WheelPositionIndicator : FrameworkElement
    {
        public static readonly DependencyProperty PositionIndexProperty =
            DependencyProperty.Register(
                nameof(PositionIndex),
                typeof(int),
                typeof(WheelPositionIndicator),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SectorCountProperty =
            DependencyProperty.Register(
                nameof(SectorCount),
                typeof(int),
                typeof(WheelPositionIndicator),
                new FrameworkPropertyMetadata(8, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(
                nameof(AccentBrush),
                typeof(Brush),
                typeof(WheelPositionIndicator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AccentTextBrushProperty =
            DependencyProperty.Register(
                nameof(AccentTextBrush),
                typeof(Brush),
                typeof(WheelPositionIndicator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SectorBrushProperty =
            DependencyProperty.Register(
                nameof(SectorBrush),
                typeof(Brush),
                typeof(WheelPositionIndicator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BorderBrushProperty =
            DependencyProperty.Register(
                nameof(BorderBrush),
                typeof(Brush),
                typeof(WheelPositionIndicator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MutedBrushProperty =
            DependencyProperty.Register(
                nameof(MutedBrush),
                typeof(Brush),
                typeof(WheelPositionIndicator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CenterBrushProperty =
            DependencyProperty.Register(
                nameof(CenterBrush),
                typeof(Brush),
                typeof(WheelPositionIndicator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public int PositionIndex
        {
            get => (int)GetValue(PositionIndexProperty);
            set => SetValue(PositionIndexProperty, value);
        }

        public int SectorCount
        {
            get => (int)GetValue(SectorCountProperty);
            set => SetValue(SectorCountProperty, value);
        }

        public Brush? AccentBrush
        {
            get => (Brush?)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public Brush? AccentTextBrush
        {
            get => (Brush?)GetValue(AccentTextBrushProperty);
            set => SetValue(AccentTextBrushProperty, value);
        }

        public Brush? SectorBrush
        {
            get => (Brush?)GetValue(SectorBrushProperty);
            set => SetValue(SectorBrushProperty, value);
        }

        public Brush? BorderBrush
        {
            get => (Brush?)GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public Brush? MutedBrush
        {
            get => (Brush?)GetValue(MutedBrushProperty);
            set => SetValue(MutedBrushProperty, value);
        }

        public Brush? CenterBrush
        {
            get => (Brush?)GetValue(CenterBrushProperty);
            set => SetValue(CenterBrushProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            double width = ActualWidth;
            double height = ActualHeight;
            if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            {
                return;
            }

            try
            {
                int sectorCount = SectorCount is 4 or 8 or 12 ? SectorCount : 8;
                int selectedIndex = PositionIndex % sectorCount;
                if (selectedIndex < 0)
                {
                    selectedIndex += sectorCount;
                }

                double size = Math.Min(width, height);
                if (!double.IsFinite(size) || size <= 0) return;

                Point center = new(width / 2.0, height / 2.0);
                double outerRadius = Math.Max(8.0, size / 2.0 - 3.0);
                double innerRadius = Math.Max(3.0, outerRadius * 0.28);
                double sectorSize = 360.0 / sectorCount;
                double gap = Math.Min(2.6, sectorSize * 0.14);

                Brush accent = ResolveBrush(AccentBrush, "AccentPrimaryBrush", Color.FromRgb(37, 99, 235));
                Brush accentText = ResolveBrush(AccentTextBrush, "AccentTextBrush", Colors.White);
                Brush sector = ResolveBrush(SectorBrush, "SubtleCardBrush", Color.FromRgb(248, 250, 252));
                Brush border = ResolveBrush(BorderBrush, "CardBorderBrush", Color.FromRgb(226, 232, 240));
                Brush muted = ResolveBrush(MutedBrush, "TextMutedBrush", Color.FromRgb(100, 116, 139));
                Brush centerFill = ResolveBrush(CenterBrush, "CardBackgroundBrush", Colors.White);

                // Resolve and clone each brush once per render. The previous
                // implementation cloned brushes inside the sector loop, which
                // created a burst of Freezable objects whenever the list was
                // rebuilt. Freezing the render-only copies also keeps WPF's
                // retained drawing data stable while rows are replaced.
                Brush selectedFill = WithOpacity(accent, 0.32);
                Brush neutralFill = WithOpacity(sector, 0.62);
                Brush mutedMarker = WithOpacity(muted, 0.72);
                Brush outerRing = WithOpacity(border, 0.9);
                Brush accentDot = WithOpacity(accent, 0.9);
                Pen selectedPen = CreatePen(accent, 1.35);
                Pen neutralPen = CreatePen(border, 0.8);
                Pen outerRingPen = CreatePen(outerRing, 0.85);

                for (int i = 0; i < sectorCount; i++)
                {
                    bool isSelected = i == selectedIndex;
                    double middleAngle = i * sectorSize;
                    double startAngle = middleAngle - sectorSize / 2.0 + gap;
                    double endAngle = middleAngle + sectorSize / 2.0 - gap;
                    double sectorOuterRadius = isSelected ? outerRadius + 0.8 : outerRadius;
                    Geometry geometry = CreateDonutSectorGeometry(center, innerRadius, sectorOuterRadius, startAngle, endAngle);

                    drawingContext.DrawGeometry(isSelected ? selectedFill : neutralFill, isSelected ? selectedPen : neutralPen, geometry);

                    if (!isSelected)
                    {
                        Point marker = PointOnCircle(center, outerRadius * 0.68, middleAngle);
                        drawingContext.DrawEllipse(mutedMarker, null, marker, 1.15, 1.15);
                    }
                }

                // A quiet outer ring keeps the indicator legible against light and dark cards.
                drawingContext.DrawEllipse(null, outerRingPen, center, outerRadius, outerRadius);

                Point selectedDirectionStart = PointOnCircle(center, innerRadius * 0.45, selectedIndex * sectorSize);
                double arrowHeadLength = Math.Max(4.5, size * 0.14);
                double arrowHeadWidth = Math.Max(2.5, size * 0.085);
                double arrowTipRadius = outerRadius * 0.80;
                Point selectedTip = PointOnCircle(center, arrowTipRadius, selectedIndex * sectorSize);
                Point arrowBase = PointOnCircle(center, arrowTipRadius - arrowHeadLength, selectedIndex * sectorSize);
                double angleRadians = selectedIndex * sectorSize * Math.PI / 180.0;
                Vector perpendicular = new(-Math.Sin(angleRadians), Math.Cos(angleRadians));
                Point leftWing = arrowBase + perpendicular * arrowHeadWidth;
                Point rightWing = arrowBase - perpendicular * arrowHeadWidth;

                Pen arrowPen = CreatePen(accentText, Math.Max(1.8, size * 0.045), PenLineCap.Round);
                drawingContext.DrawLine(arrowPen, selectedDirectionStart, arrowBase);

                StreamGeometry arrowHead = new();
                using (StreamGeometryContext context = arrowHead.Open())
                {
                    context.BeginFigure(selectedTip, true, true);
                    context.LineTo(leftWing, true, false);
                    context.LineTo(rightWing, true, false);
                }
                arrowHead.Freeze();
                drawingContext.DrawGeometry(accentText, null, arrowHead);

                double centerRadius = Math.Max(3.5, innerRadius * 0.80);
                drawingContext.DrawEllipse(centerFill, CreatePen(accent, 1.0), center, centerRadius, centerRadius);
                drawingContext.DrawEllipse(accentDot, null, center, Math.Max(1.3, size * 0.035), Math.Max(1.3, size * 0.035));
            }
            catch (Exception ex)
            {
                // A malformed theme brush or an invalid visual-state value
                // must not take down the settings window during a list reset.
                System.Diagnostics.Debug.WriteLine($"[WheelPositionIndicator Render Error]: {ex.Message}");
            }
        }

        private Brush ResolveBrush(Brush? configured, string resourceKey, Color fallback)
        {
            try
            {
                if (configured != null)
                {
                    return configured;
                }

                if (TryFindResource(resourceKey) is Brush resourceBrush)
                {
                    return resourceBrush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WheelPositionIndicator Brush Error]: {ex.Message}");
            }

            var fallbackBrush = new SolidColorBrush(fallback);
            fallbackBrush.Freeze();
            return fallbackBrush;
        }

        private static Brush WithOpacity(Brush source, double opacity)
        {
            try
            {
                Brush clone = source.CloneCurrentValue();
                clone.Opacity = Math.Clamp(opacity, 0.0, 1.0);
                if (clone.CanFreeze) clone.Freeze();
                return clone;
            }
            catch
            {
                var fallback = new SolidColorBrush(Colors.Transparent);
                fallback.Freeze();
                return fallback;
            }
        }

        private static Pen CreatePen(Brush brush, double thickness, PenLineCap cap = PenLineCap.Flat)
        {
            var pen = new Pen(brush, Math.Max(0.1, double.IsFinite(thickness) ? thickness : 1.0))
            {
                StartLineCap = cap,
                EndLineCap = cap,
                LineJoin = PenLineJoin.Round
            };
            if (pen.CanFreeze) pen.Freeze();
            return pen;
        }

        private static Point PointOnCircle(Point center, double radius, double angleDegrees)
        {
            double radians = angleDegrees * Math.PI / 180.0;
            return new Point(
                center.X + Math.Cos(radians) * radius,
                center.Y + Math.Sin(radians) * radius);
        }

        private static Geometry CreateDonutSectorGeometry(
            Point center,
            double innerRadius,
            double outerRadius,
            double startAngle,
            double endAngle)
        {
            if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) ||
                !double.IsFinite(innerRadius) || !double.IsFinite(outerRadius) ||
                !double.IsFinite(startAngle) || !double.IsFinite(endAngle) ||
                outerRadius <= 0 || innerRadius <= 0 || outerRadius <= innerRadius)
            {
                return Geometry.Empty;
            }

            Point outerStart = PointOnCircle(center, outerRadius, startAngle);
            Point outerEnd = PointOnCircle(center, outerRadius, endAngle);
            Point innerEnd = PointOnCircle(center, innerRadius, endAngle);
            Point innerStart = PointOnCircle(center, innerRadius, startAngle);
            bool isLargeArc = Math.Abs(endAngle - startAngle) > 180.0;

            StreamGeometry geometry = new();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(outerStart, true, true);
                context.ArcTo(
                    outerEnd,
                    new Size(outerRadius, outerRadius),
                    0,
                    isLargeArc,
                    SweepDirection.Clockwise,
                    true,
                    false);
                context.LineTo(innerEnd, true, false);
                context.ArcTo(
                    innerStart,
                    new Size(innerRadius, innerRadius),
                    0,
                    isLargeArc,
                    SweepDirection.Counterclockwise,
                    true,
                    false);
            }
            geometry.Freeze();
            return geometry;
        }
    }
}
