namespace WinPieGestures;

public static class StyleRendererFactory
{
	public static IRadialStyleRenderer CreateRenderer(string style)
	{
		if (string.IsNullOrEmpty(style))
		{
			return new ClassicRingRenderer();
		}
		return style.Trim() switch
		{
			"Glassmorphism" => new GlassmorphismRenderer(), 
			"CleanSectors" => new CleanSectorsRenderer(), 
			_ => new ClassicRingRenderer(), 
		};
	}
}
