using System;

namespace WinPieGestures;

public static class FuzzyMatcher
{
	public static int ComputeLevenshtein(string s, string t)
	{
		if (string.IsNullOrEmpty(s))
		{
			return t?.Length ?? 0;
		}
		if (string.IsNullOrEmpty(t))
		{
			return s.Length;
		}
		int length = s.Length;
		int length2 = t.Length;
		int[,] array = new int[length + 1, length2 + 1];
		int num = 0;
		while (num <= length)
		{
			array[num, 0] = num++;
		}
		int num2 = 0;
		while (num2 <= length2)
		{
			array[0, num2] = num2++;
		}
		for (int i = 1; i <= length; i++)
		{
			for (int j = 1; j <= length2; j++)
			{
				int num3 = ((s[i - 1] != t[j - 1]) ? 1 : 0);
				array[i, j] = Math.Min(Math.Min(array[i - 1, j] + 1, array[i, j - 1] + 1), array[i - 1, j - 1] + num3);
			}
		}
		return array[length, length2];
	}

	public static double Score(ProgramPickerWindow.ProgramItem item, string query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return 1.0;
		}
		string[] array = query.Trim().Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0)
		{
			return 1.0;
		}
		double num = 0.0;
		string[] array2 = array;
		foreach (string token in array2)
		{
			double num2 = ScoreSingleToken(item, token);
			if (num2 <= 0.0)
			{
				return 0.0;
			}
			num += num2;
		}
		if (item.UsageCount > 0)
		{
			num += (double)Math.Min(300, item.UsageCount * 25);
		}
		if (item.LastUsed > DateTime.MinValue)
		{
			TimeSpan timeSpan = DateTime.Now - item.LastUsed;
			if (timeSpan.TotalDays < 1.0)
			{
				num += 150.0;
			}
			else if (timeSpan.TotalDays < 7.0)
			{
				num += 80.0;
			}
		}
		return num;
	}

	private static double ScoreSingleToken(ProgramPickerWindow.ProgramItem item, string token)
	{
		string text = token.ToLowerInvariant();
		string text2 = item.Name.ToLowerInvariant();
		string text3 = item.ExeName.ToLowerInvariant();
		string text4 = item.PinyinInitials.ToLowerInvariant();
		string text5 = item.Pinyin.ToLowerInvariant();
		string text6 = item.FriendlyPath.ToLowerInvariant();
		string text7 = item.AppType.ToLowerInvariant();
		double num = 0.0;
		if (text2 == text || text3 == text)
		{
			num += 1000.0;
		}
		else if (text2.StartsWith(text) || text3.StartsWith(text))
		{
			num += (double)(500 + Math.Max(0, 50 - text2.Length));
		}
		else if (text2.Contains(text) || text3.Contains(text))
		{
			num += 300.0;
		}
		if (!string.IsNullOrEmpty(text4))
		{
			if (text4 == text)
			{
				num += 450.0;
			}
			else if (text4.StartsWith(text))
			{
				num += 350.0;
			}
			else if (text4.Contains(text))
			{
				num += 200.0;
			}
		}
		if (!string.IsNullOrEmpty(text5))
		{
			if (text5.StartsWith(text))
			{
				num += 280.0;
			}
			else if (text5.Contains(text))
			{
				num += 180.0;
			}
		}
		if (text6.Contains(text))
		{
			num += 100.0;
		}
		if (text7.Contains(text))
		{
			num += 80.0;
		}
		if (num == 0.0 && text.Length >= 3)
		{
			string[] array = text2.Split(new char[8] { ' ', '-', '_', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text8 in array)
			{
				if (text8.Length >= 3)
				{
					int num2 = ComputeLevenshtein(text, text8);
					if (num2 <= 2)
					{
						num = Math.Max(num, 120 - num2 * 35);
					}
				}
			}
			if (text3.Length >= 3)
			{
				int num3 = ComputeLevenshtein(text, text3);
				if (num3 <= 2)
				{
					num = Math.Max(num, 110 - num3 * 35);
				}
			}
		}
		return num;
	}
}
