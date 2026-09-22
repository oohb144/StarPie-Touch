using System.Collections.Generic;

namespace WinPieGestures;

/// <summary>
/// 鼠标手势映射：图样（如 "D" 下、"DR" 下-右、"DRD" 下-右-下）→ 动作。
/// 图样编码：方向码拼接，方向码 = U/D/L/R/UL/UR/DL/DR。
/// </summary>
public class GestureMapping
{
	public string Pattern { get; set; } = "D";

	public ActionItem Action { get; set; } = new ActionItem();
}