using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPieGestures;

public interface IRadialStyleRenderer
{
	Brush DefaultSectorBrush { get; }

	Brush HighlightSectorBrush { get; }

	Brush SectorBorderBrush { get; }

	Brush HighlightBorderBrush { get; }

	Brush TextColorBrush { get; }

	Brush CoreBgBrush { get; }

	Brush CoreBorderBrush { get; }

	double BorderThickness { get; }

	double HighlightBorderThickness { get; }

	void Initialize(string theme, AppConfig config);

	void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex);

	void ApplySectorHighlight(Path path, bool isHighlighted);

	void ApplyExitHighlight(Path exitIcon, bool isHighlighted);
}
