using System;
using System.Linq;

namespace WinPieGestures;

/// <summary>
/// 轮盘扇区文本智能排版与自适应两行格式化工具
/// </summary>
public static class SectorTextFormatter
{
	/// <summary>
	/// 计算文本在轮盘扇区中渲染的视觉权重宽度（中日韩汉字及全角标点计2.0，宽大写字符计1.2，普通ASCII字母数字计1.0，空格标点计0.8）
	/// </summary>
	public static double MeasureVisualWeight(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0.0;
		}

		double weight = 0.0;
		foreach (char c in text)
		{
			if (c >= 0x2E80 || c >= 0xFF00)
			{
				weight += 2.0; // 中日韩汉字、全角标点、Emoji等
			}
			else if (char.IsUpper(c) || c == '@' || c == '#' || c == '%' || c == 'W' || c == 'M')
			{
				weight += 1.2; // 宽英文字母与特殊宽符号
			}
			else if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
			{
				weight += 0.8; // 空格与半角标点
			}
			else
			{
				weight += 1.0; // 常规半角小写字母与数字
			}
		}
		return weight;
	}

	/// <summary>
	/// 当扇区文本过长时，智能转换为两行显示。
	/// 优先在括号、中英文副标题、连字符或词间空格处断行；纯中文长文本则在中点平衡断行。
	/// </summary>
	/// <param name="rawText">原始文本</param>
	/// <param name="sectorCount">扇区数（4/8/12）</param>
	/// <param name="isSubWheel">是否为二级外环或蜂窝扇（空间更紧凑）</param>
	/// <returns>格式化后的文本（如需折行则中间包含 '\n'）</returns>
	public static string FormatSectorText(string? rawText, int sectorCount = 8, bool isSubWheel = false)
	{
		if (string.IsNullOrWhiteSpace(rawText))
		{
			return rawText ?? string.Empty;
		}

		string text = rawText.Trim();

		// 若用户在配置中已显式输入换行，尊重用户既有排版
		if (text.Contains('\n'))
		{
			return text;
		}

		double totalWeight = MeasureVisualWeight(text);

		// 判定是否“过长”的分态阈值：
		// - 12 键扇区（30° 狭窄扇面）：主轮盘 8.5，子轮盘 7.5
		// - 8 键扇区（45° 黄金扇面）：主轮盘 12.5，子轮盘 9.5
		//     "复制 (Copy)"(10.6)、"粘贴 (Paste)"(11.6) <= 12.5 保持单行优雅；
		//     "音量增 (Vol Up)"(14.6)、"锁定电脑 (Lock)"(14.6)、"音量减 (Vol Down)"(16.6) > 12.5 自动转换为两行！
		// - 4 键扇区（90° 宽广扇面）：主轮盘 18.0，子轮盘 13.0
		double threshold = sectorCount switch
		{
			4 => isSubWheel ? 13.0 : 18.0,
			12 => isSubWheel ? 7.5 : 8.5,
			_ => isSubWheel ? 9.5 : 12.5
		};

		if (totalWeight <= threshold)
		{
			return text;
		}

		// === 策略 1：括号优先断行（中英文主副标题 / 快捷键提示）===
		// 如 "音量增 (Vol Up)" -> "音量增\n(Vol Up)"
		// 如 "锁定电脑 (Lock)" -> "锁定电脑\n(Lock)"
		// 如 "系统工具 (Tools)" -> "系统工具\n(Tools)"
		int parenIdx = text.IndexOfAny(new[] { '(', '（', '[', '【' });
		if (parenIdx > 0 && parenIdx < text.Length - 1)
		{
			string prefix = text.Substring(0, parenIdx).Trim();
			string suffix = text.Substring(parenIdx).Trim();
			if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(suffix))
			{
				return prefix + "\n" + suffix;
			}
		}

		// === 策略 2：分隔符断行（" - ", " / ", " | ", "--", "·", "：" 等）===
		string[] separators = new[] { " - ", " / ", " | ", "--", "·", "：" };
		foreach (string sep in separators)
		{
			int sepIdx = text.IndexOf(sep, StringComparison.Ordinal);
			if (sepIdx > 0 && sepIdx < text.Length - sep.Length)
			{
				string prefix = text.Substring(0, sepIdx).Trim();
				string suffix = text.Substring(sepIdx + sep.Length).Trim();
				if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(suffix))
				{
					return prefix + "\n" + suffix;
				}
			}
		}

		// === 策略 3：单词与空格平衡断行（英文或带空格的多词名称）===
		// 寻找最靠近视觉中点的空格位置断开
		if (text.Contains(' '))
		{
			double halfWeight = totalWeight / 2.0;
			int bestSpaceIdx = -1;
			double bestDiff = double.MaxValue;
			double currentWeight = 0.0;

			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c == ' ' && i > 0 && i < text.Length - 1)
				{
					double diff = Math.Abs(currentWeight - halfWeight);
					if (diff < bestDiff)
					{
						bestDiff = diff;
						bestSpaceIdx = i;
					}
				}
				currentWeight += (c >= 0x2E80 || c >= 0xFF00) ? 2.0 : ((char.IsUpper(c) || c == '@' || c == '#') ? 1.2 : 1.0);
			}

			if (bestSpaceIdx > 0)
			{
				string prefix = text.Substring(0, bestSpaceIdx).Trim();
				string suffix = text.Substring(bestSpaceIdx + 1).Trim();
				if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(suffix))
				{
					return prefix + "\n" + suffix;
				}
			}
		}

		// === 策略 4：纯中文或连续长文本中点平衡断行 ===
		// 寻找最平分前后视觉权重的字符分界点
		{
			double halfWeight = totalWeight / 2.0;
			double runningWeight = 0.0;
			int splitIdx = text.Length / 2;
			double bestDiff = double.MaxValue;

			for (int i = 1; i < text.Length; i++)
			{
				char c = text[i - 1];
				runningWeight += (c >= 0x2E80 || c >= 0xFF00) ? 2.0 : 1.0;
				double diff = Math.Abs(runningWeight - halfWeight);
				if (diff <= bestDiff)
				{
					bestDiff = diff;
					splitIdx = i;
				}
			}

			if (splitIdx > 0 && splitIdx < text.Length)
			{
				string prefix = text.Substring(0, splitIdx).Trim();
				string suffix = text.Substring(splitIdx).Trim();
				if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(suffix))
				{
					return prefix + "\n" + suffix;
				}
			}
		}

		return text;
	}
}
