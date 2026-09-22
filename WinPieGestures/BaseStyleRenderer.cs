using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace WinPieGestures;

public abstract class BaseStyleRenderer : IRadialStyleRenderer
{
	protected AppConfig? _config;

	// 高亮光晕颜色在 Initialize 阶段解析一次；高亮切换是最高频路径，不再重复走字符串解析
	private Color? _glowColorOverride;

	public Brush DefaultSectorBrush { get; protected set; }

	public Brush HighlightSectorBrush { get; protected set; }

	public Brush SectorBorderBrush { get; protected set; }

	public Brush HighlightBorderBrush { get; protected set; }

	public Brush TextColorBrush { get; protected set; }

	public Brush CoreBgBrush { get; protected set; }

	public Brush CoreBorderBrush { get; protected set; }

	public double BorderThickness { get; protected set; } = 1.0;

	public double HighlightBorderThickness { get; protected set; } = 1.5;

	public bool IsLightTheme { get; protected set; }

	public virtual void Initialize(string theme, AppConfig config)
	{
		_config = config;
		BorderThickness = 1.0;
		HighlightBorderThickness = 1.5;
		string text = theme;
		if (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(theme))
		{
			text = (AppThemeManager.IsWindowsInDarkTheme() ? "Dark" : "Light");
		}
		IsLightTheme = string.Equals(text, "Light", StringComparison.OrdinalIgnoreCase);
		GetDefaultColors(text, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex);
		string text2 = sectorBgHex;
		string text3 = sectorBorderHex;
		if (theme == "Light" && UseStandardLightThemeFallback())
		{
			sectorBgHex = "#F0F8FAFC";
			sectorBorderHex = "#3064748B";
			highlightBgHex = "#FF2563EB";
			highlightBorderHex = "#FF60A5FA";
			textHex = "#FF0F172A";
			text2 = "#FFF8FAFC";
			text3 = "#3064748B";
		}
		else if (theme == "MatchaForest")
		{
			sectorBgHex = "#E6142E1F";
			sectorBorderHex = "#4034D399";
			highlightBgHex = "#FF10B981";
			highlightBorderHex = "#FF6EE7B7";
			textHex = "#FFF0FDF4";
			text2 = "#F0142E1F";
			text3 = "#4034D399";
		}
		else if (theme == "GlacialIce")
		{
			sectorBgHex = "#E0E0F2FE";
			sectorBorderHex = "#6038BDF8";
			highlightBgHex = "#FF0284C7";
			highlightBorderHex = "#FFBAE6FD";
			textHex = "#FF0C4A6E";
			text2 = "#F0E0F2FE";
			text3 = "#6038BDF8";
		}
		else if (theme == "MorandiMuted")
		{
			sectorBgHex = "#E62C302E";
			sectorBorderHex = "#409CA3AF";
			highlightBgHex = "#FF78716C";
			highlightBorderHex = "#FFD6D3D1";
			textHex = "#FFF5F5F4";
			text2 = "#F02C302E";
			text3 = "#409CA3AF";
		}
		else if (theme.StartsWith("CustomPreset_") || (config.CustomColorPresets != null && config.CustomColorPresets.Exists((CustomColorPreset p) => p.Id == theme || p.Name == theme)))
		{
			CustomColorPreset customColorPreset = config.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == theme || p.Name == theme || "CustomPreset_" + p.Id == theme);
			if (customColorPreset != null)
			{
				sectorBgHex = customColorPreset.SectorBg;
				sectorBorderHex = customColorPreset.SectorBorder;
				highlightBgHex = customColorPreset.HighlightBg;
				highlightBorderHex = customColorPreset.HighlightBorder;
				textHex = customColorPreset.TextColor;
			}
		}
		else if (theme == "Custom")
		{
			sectorBgHex = config.CustomSectorBg ?? sectorBgHex;
			sectorBorderHex = config.CustomSectorBorder ?? sectorBorderHex;
			highlightBgHex = config.CustomHighlightBg ?? highlightBgHex;
			highlightBorderHex = config.CustomHighlightBorder ?? highlightBorderHex;
			textHex = config.CustomText ?? textHex;
			text2 = sectorBgHex;
			text3 = sectorBorderHex;
		}
		text2 = sectorBgHex;
		text3 = sectorBorderHex;
		try
		{
			DefaultSectorBrush = CreateSolidBrush(sectorBgHex);
			HighlightSectorBrush = CreateSolidBrush(highlightBgHex);
			SectorBorderBrush = CreateSolidBrush(sectorBorderHex);
			HighlightBorderBrush = CreateSolidBrush(highlightBorderHex);
			TextColorBrush = CreateSolidBrush(textHex);
			CoreBgBrush = CreateSolidBrush(text2);
			CoreBorderBrush = CreateSolidBrush(text3);
		}
		catch
		{
			DefaultSectorBrush = CreateSolidBrush("#E618181B");
			HighlightSectorBrush = CreateSolidBrush("#FF3B82F6");
			SectorBorderBrush = CreateSolidBrush("#35FFFFFF");
			HighlightBorderBrush = CreateSolidBrush("#A0FFFFFF");
			TextColorBrush = CreateSolidBrush("#F8FAFC");
			CoreBgBrush = CreateSolidBrush("#F018181B");
			CoreBorderBrush = CreateSolidBrush("#30FFFFFF");
		}
		_glowColorOverride = null;
		if (_config != null && !string.IsNullOrEmpty(_config.HighlightGlowColor))
		{
			try
			{
				_glowColorOverride = (Color)ColorConverter.ConvertFromString(_config.HighlightGlowColor);
			}
			catch
			{
			}
		}
		PostInitialize();
	}

	protected SolidColorBrush CreateSolidBrush(string hex)
	{
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
	}

	protected virtual void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
	{
		sectorBgHex = "#EB18181B";
		sectorBorderHex = "#30FFFFFF";
		highlightBgHex = "#FF2563EB";
		highlightBorderHex = "#FF60A5FA";
		textHex = "#FFF8FAFC";
	}

	protected virtual bool UseStandardLightThemeFallback()
	{
		return true;
	}

	protected virtual void PostInitialize()
	{
	}

	/// <summary>
	/// 构建一次并冻结的 DropShadowEffect，供扇区高亮反复复用。
	/// 冻结后的 Freezable 只读且线程安全，可被多个扇区 Path 安全共享，
	/// 高亮切换因此从「实例化 Effect 并重建渲染资源」降为一次引用赋值。
	/// </summary>
	protected static DropShadowEffect CreateFrozenDropShadow(
		Color color, double blurRadius, double shadowDepth, double opacity, double? direction = null)
	{
		DropShadowEffect effect = new DropShadowEffect
		{
			Color = color,
			BlurRadius = blurRadius,
			ShadowDepth = shadowDepth,
			Opacity = opacity
		};
		if (direction.HasValue)
		{
			effect.Direction = direction.Value;
		}
		effect.Freeze();
		return effect;
	}

	public virtual Color GetEffectiveGlowColor()
	{
		if (_glowColorOverride.HasValue)
		{
			return _glowColorOverride.Value;
		}
		if (HighlightBorderBrush is SolidColorBrush { Color: { A: >0 } } solidColorBrush)
		{
			return solidColorBrush.Color;
		}
		if (HighlightSectorBrush is SolidColorBrush { Color: { A: >0 } } solidColorBrush2)
		{
			return solidColorBrush2.Color;
		}
		return Color.FromRgb(168, 85, 247);
	}

	public virtual double GetEffectiveGlowRadius(double defaultRadius = 24.0)
	{
		if (_config != null && _config.HighlightGlowRadius > 0.0)
		{
			return _config.HighlightGlowRadius;
		}
		return defaultRadius;
	}

	public virtual double GetEffectiveGlowOpacity(double defaultOpacity = 0.85)
	{
		if (_config != null && _config.HighlightGlowOpacity >= 0.0)
		{
			return _config.HighlightGlowOpacity;
		}
		return defaultOpacity;
	}

	public abstract void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex);

	public virtual void ApplySectorHighlight(Path path, bool isHighlighted)
	{
	}

	public virtual void ApplyExitHighlight(Path exitIcon, bool isHighlighted)
	{
	}
}
