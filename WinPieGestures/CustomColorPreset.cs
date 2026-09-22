using System;

namespace WinPieGestures;

public class CustomColorPreset
{
	public string Id { get; set; } = Guid.NewGuid().ToString();

	public string Name { get; set; } = "我的配色";

	public string SectorBg { get; set; } = "#9016161A";

	public string SectorBorder { get; set; } = "#35FFFFFF";

	public string HighlightBg { get; set; } = "#E06C4DFF";

	public string HighlightBorder { get; set; } = "#A0FFFFFF";

	public string TextColor { get; set; } = "#E0FFFFFF";

	public override string ToString()
	{
		return Name;
	}
}
