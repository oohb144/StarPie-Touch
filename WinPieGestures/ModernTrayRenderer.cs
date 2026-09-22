using System;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GdiColor = System.Drawing.Color;
using GdiPen = System.Drawing.Pen;
using GdiSolidBrush = System.Drawing.SolidBrush;
using GdiRectangle = System.Drawing.Rectangle;

namespace WinPieGestures;

/// <summary>
/// 现代化系统托盘配色方案表 (支持深色与浅色双版)
/// </summary>
public class ModernTrayColorTable : ProfessionalColorTable
{
	private readonly bool _isDark;

	public ModernTrayColorTable(bool isDark)
	{
		_isDark = isDark;
	}

	public override GdiColor ToolStripDropDownBackground =>
		_isDark ? GdiColor.FromArgb(24, 24, 27) : GdiColor.FromArgb(255, 255, 255);

	public override GdiColor ImageMarginGradientBegin => ToolStripDropDownBackground;
	public override GdiColor ImageMarginGradientMiddle => ToolStripDropDownBackground;
	public override GdiColor ImageMarginGradientEnd => ToolStripDropDownBackground;

	public override GdiColor MenuBorder =>
		_isDark ? GdiColor.FromArgb(46, 46, 51) : GdiColor.FromArgb(226, 232, 240);

	public override GdiColor MenuItemBorder => GdiColor.Transparent;

	public override GdiColor MenuItemSelected =>
		_isDark ? GdiColor.FromArgb(39, 39, 42) : GdiColor.FromArgb(241, 245, 249);

	public override GdiColor MenuItemSelectedGradientBegin => MenuItemSelected;
	public override GdiColor MenuItemSelectedGradientEnd => MenuItemSelected;

	public override GdiColor SeparatorDark =>
		_isDark ? GdiColor.FromArgb(46, 46, 51) : GdiColor.FromArgb(226, 232, 240);

	public override GdiColor SeparatorLight => GdiColor.Transparent;
}

/// <summary>
/// 现代化系统托盘右键菜单渲染器 (无复古左侧边距槽、平滑高亮微交互、纯净分割线)
/// </summary>
public class ModernTrayRenderer : ToolStripProfessionalRenderer
{
	private readonly bool _isDark;

	public ModernTrayRenderer(bool isDark) : base(new ModernTrayColorTable(isDark))
	{
		_isDark = isDark;
	}

	protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
	{
		if (e.Item.Selected && e.Item.Enabled)
		{
			GdiRectangle bounds = new GdiRectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
			GdiColor hoverColor = _isDark ? GdiColor.FromArgb(39, 39, 42) : GdiColor.FromArgb(241, 245, 249);
			using var brush = new GdiSolidBrush(hoverColor);
			using var path = CreateRoundedRectanglePath(bounds, 4);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.FillPath(brush, path);
		}
		else
		{
			base.OnRenderMenuItemBackground(e);
		}
	}

	protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
	{
		if (!e.Item.Enabled)
		{
			GdiColor disabledColor = _isDark ? GdiColor.FromArgb(148, 163, 184) : GdiColor.FromArgb(100, 116, 139);
			TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
			TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, disabledColor, flags);
			return;
		}

		base.OnRenderItemText(e);
	}

	protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
	{
		int y = e.Item.Height / 2;
		GdiColor sepColor = _isDark ? GdiColor.FromArgb(46, 46, 51) : GdiColor.FromArgb(226, 232, 240);
		using var pen = new GdiPen(sepColor, 1f);
		e.Graphics.SmoothingMode = SmoothingMode.None;
		e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
	}

	private static GraphicsPath CreateRoundedRectanglePath(GdiRectangle rect, int radius)
	{
		GraphicsPath path = new GraphicsPath();
		int d = radius * 2;
		path.AddArc(rect.X, rect.Y, d, d, 180, 90);
		path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
		path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
		path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
		path.CloseFigure();
		return path;
	}
}
