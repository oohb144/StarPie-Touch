using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WinPieGestures;

public partial class HotkeyBuilderDialog : Window
{
	public string ResultHotkey { get; private set; } = string.Empty;
	private bool _isInternalUpdating = true;
	private readonly List<ComboBoxItem> _allKeyItems = new List<ComboBoxItem>();

	public HotkeyBuilderDialog(string initialHotkey)
	{
		_isInternalUpdating = true;
		InitializeComponent();
		foreach (var item in MainKeyComboBox.Items)
		{
			if (item is ComboBoxItem cbi)
			{
				_allKeyItems.Add(cbi);
			}
		}
		_isInternalUpdating = false;
		AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig?.AppTheme ?? "System");
		InitializeFromHotkey(initialHotkey);
	}

	private void InitializeFromHotkey(string hotkey)
	{
		_isInternalUpdating = true;
		try
		{
			ResultHotkey = (hotkey ?? "").Trim();
			if (CustomInputTextBox != null)
			{
				CustomInputTextBox.Text = ResultHotkey;
			}
			if (PreviewResultText != null)
			{
				PreviewResultText.Text = string.IsNullOrEmpty(ResultHotkey) ? "(空)" : ResultHotkey;
			}

			if (CtrlCheckBox != null) CtrlCheckBox.IsChecked = false;
			if (ShiftCheckBox != null) ShiftCheckBox.IsChecked = false;
			if (AltCheckBox != null) AltCheckBox.IsChecked = false;
			if (WinCheckBox != null) WinCheckBox.IsChecked = false;

			if (string.IsNullOrWhiteSpace(hotkey))
			{
				if (MainKeyComboBox != null && MainKeyComboBox.Items.Count > 0)
				{
					MainKeyComboBox.SelectedIndex = 0;
				}
				return;
			}

			string[] parts = hotkey.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			string mainKey = "";
			foreach (string p in parts)
			{
				string t = p.Trim();
				if (t.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || t.Equals("Control", StringComparison.OrdinalIgnoreCase) || t.Equals("LCtrl", StringComparison.OrdinalIgnoreCase) || t.Equals("RCtrl", StringComparison.OrdinalIgnoreCase))
				{
					if (CtrlCheckBox != null) CtrlCheckBox.IsChecked = true;
				}
				else if (t.Equals("Shift", StringComparison.OrdinalIgnoreCase) || t.Equals("LShift", StringComparison.OrdinalIgnoreCase) || t.Equals("RShift", StringComparison.OrdinalIgnoreCase))
				{
					if (ShiftCheckBox != null) ShiftCheckBox.IsChecked = true;
				}
				else if (t.Equals("Alt", StringComparison.OrdinalIgnoreCase) || t.Equals("Menu", StringComparison.OrdinalIgnoreCase) || t.Equals("LAlt", StringComparison.OrdinalIgnoreCase) || t.Equals("RAlt", StringComparison.OrdinalIgnoreCase))
				{
					if (AltCheckBox != null) AltCheckBox.IsChecked = true;
				}
				else if (t.Equals("Win", StringComparison.OrdinalIgnoreCase) || t.Equals("Windows", StringComparison.OrdinalIgnoreCase) || t.Equals("LWin", StringComparison.OrdinalIgnoreCase) || t.Equals("RWin", StringComparison.OrdinalIgnoreCase))
				{
					if (WinCheckBox != null) WinCheckBox.IsChecked = true;
				}
				else
				{
					mainKey = t;
				}
			}

			if (MainKeyComboBox != null)
			{
				bool matched = false;
				if (!string.IsNullOrEmpty(mainKey))
				{
					foreach (ComboBoxItem item in MainKeyComboBox.Items)
					{
						if (string.Equals(item.Tag?.ToString(), mainKey, StringComparison.OrdinalIgnoreCase))
						{
							MainKeyComboBox.SelectedItem = item;
							matched = true;
							break;
						}
					}
				}
				if (!matched && MainKeyComboBox.Items.Count > 0)
				{
					MainKeyComboBox.SelectedIndex = 0;
				}
			}

			UpdateSwitcherTip();
		}
		finally
		{
			_isInternalUpdating = false;
		}
	}

	private void QuickPreset_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is string preset)
		{
			InitializeFromHotkey(preset);
		}
	}

	private void OnComboChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdating) return;
		BuildFromControls();
	}

	private void BuildFromControls()
	{
		if (_isInternalUpdating) return;
		if (CtrlCheckBox == null || ShiftCheckBox == null || AltCheckBox == null || WinCheckBox == null || MainKeyComboBox == null || CustomInputTextBox == null || PreviewResultText == null) return;

		List<string> list = new List<string>();
		if (CtrlCheckBox.IsChecked == true) list.Add("Ctrl");
		if (ShiftCheckBox.IsChecked == true) list.Add("Shift");
		if (AltCheckBox.IsChecked == true) list.Add("Alt");
		if (WinCheckBox.IsChecked == true) list.Add("Win");

		string tag = (MainKeyComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
		if (!string.IsNullOrEmpty(tag))
		{
			list.Add(tag);
		}

		ResultHotkey = string.Join(" + ", list);
		_isInternalUpdating = true;
		try
		{
			CustomInputTextBox.Text = ResultHotkey;
			PreviewResultText.Text = string.IsNullOrEmpty(ResultHotkey) ? "(空)" : ResultHotkey;
			UpdateSwitcherTip();
		}
		finally
		{
			_isInternalUpdating = false;
		}
	}

	private void CustomInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isInternalUpdating) return;
		ResultHotkey = CustomInputTextBox?.Text?.Trim() ?? "";
		if (PreviewResultText != null)
		{
			PreviewResultText.Text = string.IsNullOrEmpty(ResultHotkey) ? "(空)" : ResultHotkey;
		}
		UpdateSwitcherTipFromText(ResultHotkey);
	}

	private void UpdateSwitcherTip()
	{
		if (WindowSwitcherTipBorder == null) return;
		bool isAlt = AltCheckBox?.IsChecked == true;
		bool isCtrl = CtrlCheckBox?.IsChecked == true;
		string tag = (MainKeyComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
		bool isTab = string.Equals(tag, "Tab", StringComparison.OrdinalIgnoreCase);

		if (isAlt && isTab && !isCtrl)
		{
			WindowSwitcherTipBorder.Visibility = Visibility.Visible;
		}
		else
		{
			WindowSwitcherTipBorder.Visibility = Visibility.Collapsed;
		}
	}

	private void UpdateSwitcherTipFromText(string text)
	{
		if (WindowSwitcherTipBorder == null) return;
		string t = (text ?? "").ToLowerInvariant();
		if (t.Contains("alt") && t.Contains("tab") && !t.Contains("ctrl"))
		{
			WindowSwitcherTipBorder.Visibility = Visibility.Visible;
		}
		else
		{
			WindowSwitcherTipBorder.Visibility = Visibility.Collapsed;
		}
	}

	private void ConvertToStickyAltTab_Click(object sender, RoutedEventArgs e)
	{
		if (CtrlCheckBox != null)
		{
			CtrlCheckBox.IsChecked = true;
		}
		else
		{
			InitializeFromHotkey("Ctrl + Alt + Tab");
		}
	}

	private void OkButton_Click(object sender, RoutedEventArgs e)
	{
		if (CustomInputTextBox != null)
		{
			ResultHotkey = CustomInputTextBox.Text.Trim();
		}
		DialogResult = true;
		Close();
	}

	private void MainKeySearchBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		string query = MainKeySearchBox?.Text?.Trim() ?? "";
		if (MainKeySearchPlaceholder != null)
		{
			MainKeySearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
		}
		if (MainKeyClearSearchBtn != null)
		{
			MainKeyClearSearchBtn.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
		}

		if (MainKeyComboBox == null || _allKeyItems.Count == 0) return;

		var currentSelected = MainKeyComboBox.SelectedItem as ComboBoxItem;
		_isInternalUpdating = true;
		try
		{
			MainKeyComboBox.Items.Clear();
			ComboBoxItem? firstMatch = null;
			foreach (var item in _allKeyItems)
			{
				string content = item.Content?.ToString() ?? "";
				string tag = item.Tag?.ToString() ?? "";
				if (string.IsNullOrEmpty(query) ||
				    content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
				    tag.Contains(query, StringComparison.OrdinalIgnoreCase))
				{
					MainKeyComboBox.Items.Add(item);
					if (firstMatch == null && !string.IsNullOrEmpty(tag))
					{
						firstMatch = item;
					}
				}
			}

			if (currentSelected != null && MainKeyComboBox.Items.Contains(currentSelected))
			{
				MainKeyComboBox.SelectedItem = currentSelected;
			}
			else if (firstMatch != null && !string.IsNullOrEmpty(query))
			{
				MainKeyComboBox.SelectedItem = firstMatch;
			}
			else if (MainKeyComboBox.Items.Count > 0)
			{
				MainKeyComboBox.SelectedIndex = 0;
			}
		}
		finally
		{
			_isInternalUpdating = false;
		}
		BuildFromControls();
	}

	private void MainKeyClearSearchBtn_Click(object sender, RoutedEventArgs e)
	{
		if (MainKeySearchBox != null)
		{
			MainKeySearchBox.Text = string.Empty;
		}
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}
}
