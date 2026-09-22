using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPieGestures;

public partial class ColorPickerWindow : Window
{
	private double _hue;

	private double _saturation = 1.0;

	private double _value = 1.0;

	private byte _alpha = byte.MaxValue;

	private bool _isUpdating;

	private static readonly string[] PresetColors = new string[35]
	{
		"#EB18181B", "#F0F8FAFC", "#FF2563EB", "#FF3B82F6", "#FF60A5FA", "#FF06B6D4", "#FF0EA5E9", "#FF10B981", "#FF22C55E", "#FF84CC16",
		"#FFEAB308", "#FFF97316", "#FFEF4444", "#FFF43F5E", "#FFEC4899", "#FFD946EF", "#FFA855F7", "#FF8B5CF6", "#FF6366F1", "#FF475569",
		"#FF64748B", "#FF94A3B8", "#FFCBD5E1", "#FFF1F5F9", "#FFFFFF", "#FF000000", "#9016161A", "#35FFFFFF", "#E06C4DFF", "#A0FFFFFF",
		"#E0FFFFFF", "#E60F172A", "#E61E1B4B", "#E6142E1F", "#E6181111"
	};

	public string SelectedHexColor { get; private set; } = "#FF2563EB";

	public Action<string>? ColorChangedCallback { get; set; }

	public ColorPickerWindow(string initialHex = "#FF2563EB")
	{
		InitializeComponent();
		AppThemeManager.ApplyTheme(this, AppThemeManager.CurrentEffectiveTheme);
		PopulateSwatches();
		SetColorFromHex(string.IsNullOrWhiteSpace(initialHex) ? "#FF2563EB" : initialHex);
		ApplyLocalization();
	}

	private void ApplyLocalization()
	{
		base.Title = I18n.T("ColorPickerTitle") + " - StarPie";
		if (HueLabelText != null)
		{
			HueLabelText.Text = I18n.T("ColorPickerHue");
		}
		if (AlphaLabelText != null)
		{
			AlphaLabelText.Text = I18n.T("ColorPickerAlpha");
		}
		if (EyedropperTitleText != null)
		{
			EyedropperTitleText.Text = I18n.T("ColorPickerEyedropperTitle");
		}
		if (EyedropperDescText != null)
		{
			EyedropperDescText.Text = I18n.T("ColorPickerEyedropperDesc");
		}
		if (EyedropperButton != null)
		{
			EyedropperButton.Content = I18n.T("ColorPickerEyedropperBtn");
		}
		if (SwatchesTitleText != null)
		{
			SwatchesTitleText.Text = I18n.T("ColorPickerSwatches");
		}
		if (CancelButton != null)
		{
			CancelButton.Content = I18n.T("BtnCancel");
		}
		if (OkButton != null)
		{
			OkButton.Content = I18n.T("ColorPickerApply");
		}
	}

	private void PopulateSwatches()
	{
		SwatchesPanel.Children.Clear();
		string[] presetColors = PresetColors;
		foreach (string hex in presetColors)
		{
			try
			{
				Color color = (Color)ColorConverter.ConvertFromString(hex);
				Button button = new Button
				{
					Style = (Style)FindResource("ColorSwatchButtonStyle"),
					Background = new SolidColorBrush(color),
					ToolTip = hex
				};
				button.Click += delegate
				{
					SetColorFromHex(hex);
				};
				SwatchesPanel.Children.Add(button);
			}
			catch
			{
			}
		}
	}

	public void SetColorFromHex(string hex)
	{
		if (string.IsNullOrWhiteSpace(hex))
		{
			return;
		}
		try
		{
			if (!hex.StartsWith("#"))
			{
				hex = "#" + hex;
			}
			Color color = (Color)ColorConverter.ConvertFromString(hex);
			_alpha = color.A;
			ColorToHsv(color, out _hue, out _saturation, out _value);
			_isUpdating = true;
			HueSlider.Value = _hue;
			AlphaSlider.Value = (int)_alpha;
			HexInputBox.Text = hex.ToUpper();
			_isUpdating = false;
			UpdateSpectrumCanvasColor();
			UpdateSpectrumThumbPosition();
			UpdatePreview();
		}
		catch
		{
		}
	}

	private void UpdateSpectrumCanvasColor()
	{
		Color color = HsvToRgb(_hue, 1.0, 1.0);
		SpectrumCanvas.Background = new SolidColorBrush(color);
	}

	private void UpdateSpectrumThumbPosition()
	{
		double num = ((SpectrumCanvas.ActualWidth > 0.0) ? SpectrumCanvas.ActualWidth : 440.0);
		double num2 = ((SpectrumCanvas.ActualHeight > 0.0) ? SpectrumCanvas.ActualHeight : 180.0);
		double length = _saturation * num;
		double length2 = (1.0 - _value) * num2;
		Canvas.SetLeft(SpectrumThumb, length);
		Canvas.SetTop(SpectrumThumb, length2);
	}

	private void UpdatePreview()
	{
		Color color = HsvToRgb(_hue, _saturation, _value);
		Color color2 = Color.FromArgb(_alpha, color.R, color.G, color.B);
		SelectedHexColor = $"#{color2.A:X2}{color2.R:X2}{color2.G:X2}{color2.B:X2}";
		ColorPreviewBorder.Background = new SolidColorBrush(color2);
		if (!_isUpdating && HexInputBox != null)
		{
			_isUpdating = true;
			HexInputBox.Text = SelectedHexColor;
			_isUpdating = false;
		}
		ColorChangedCallback?.Invoke(SelectedHexColor);
	}

	private void SpectrumCanvas_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed)
		{
			SpectrumCanvas.CaptureMouse();
			UpdateFromSpectrumMouse(e.GetPosition(SpectrumCanvas));
		}
	}

	private void SpectrumCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed && SpectrumCanvas.IsMouseCaptured)
		{
			UpdateFromSpectrumMouse(e.GetPosition(SpectrumCanvas));
		}
	}

	protected override void OnMouseUp(MouseButtonEventArgs e)
	{
		base.OnMouseUp(e);
		if (SpectrumCanvas.IsMouseCaptured)
		{
			SpectrumCanvas.ReleaseMouseCapture();
		}
	}

	private void UpdateFromSpectrumMouse(Point pos)
	{
		double num = ((SpectrumCanvas.ActualWidth > 0.0) ? SpectrumCanvas.ActualWidth : 440.0);
		double num2 = ((SpectrumCanvas.ActualHeight > 0.0) ? SpectrumCanvas.ActualHeight : 180.0);
		double num3 = Math.Max(0.0, Math.Min(num, pos.X));
		double num4 = Math.Max(0.0, Math.Min(num2, pos.Y));
		_saturation = num3 / num;
		_value = 1.0 - num4 / num2;
		UpdateSpectrumThumbPosition();
		UpdatePreview();
	}

	private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdating)
		{
			_hue = HueSlider.Value;
			UpdateSpectrumCanvasColor();
			UpdatePreview();
		}
	}

	private void AlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdating)
		{
			_alpha = (byte)AlphaSlider.Value;
			UpdatePreview();
		}
	}

	private void HexInputBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdating)
		{
			return;
		}
		string text = HexInputBox.Text.Trim();
		if (text.Length != 7 && text.Length != 9)
		{
			return;
		}
		try
		{
			if (!text.StartsWith("#"))
			{
				text = "#" + text;
			}
			Color color = (Color)ColorConverter.ConvertFromString(text);
			_alpha = color.A;
			ColorToHsv(color, out _hue, out _saturation, out _value);
			_isUpdating = true;
			HueSlider.Value = _hue;
			AlphaSlider.Value = (int)_alpha;
			_isUpdating = false;
			UpdateSpectrumCanvasColor();
			UpdateSpectrumThumbPosition();
			UpdatePreview();
		}
		catch
		{
		}
	}

	private void Eyedropper_Click(object sender, RoutedEventArgs e)
	{
		ScreenEyedropperOverlay screenEyedropperOverlay = new ScreenEyedropperOverlay();
		if (screenEyedropperOverlay.ShowDialog() == true && !string.IsNullOrEmpty(screenEyedropperOverlay.CapturedHexColor))
		{
			SetColorFromHex(screenEyedropperOverlay.CapturedHexColor);
		}
	}

	private void SwatchesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (double)e.Delta / 3.0);
			e.Handled = true;
		}
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	private static Color HsvToRgb(double h, double s, double v)
	{
		int num = (int)Math.Floor(h / 60.0) % 6;
		double num2 = h / 60.0 - Math.Floor(h / 60.0);
		v *= 255.0;
		byte b = (byte)Math.Max(0.0, Math.Min(255.0, v));
		byte b2 = (byte)Math.Max(0.0, Math.Min(255.0, v * (1.0 - s)));
		byte b3 = (byte)Math.Max(0.0, Math.Min(255.0, v * (1.0 - num2 * s)));
		byte b4 = (byte)Math.Max(0.0, Math.Min(255.0, v * (1.0 - (1.0 - num2) * s)));
		return num switch
		{
			0 => Color.FromRgb(b, b4, b2), 
			1 => Color.FromRgb(b3, b, b2), 
			2 => Color.FromRgb(b2, b, b4), 
			3 => Color.FromRgb(b2, b3, b), 
			4 => Color.FromRgb(b4, b2, b), 
			_ => Color.FromRgb(b, b2, b3), 
		};
	}

	private static void ColorToHsv(Color color, out double h, out double s, out double v)
	{
		double num = (double)(int)color.R / 255.0;
		double num2 = (double)(int)color.G / 255.0;
		double num3 = (double)(int)color.B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		double num6 = num4 - num5;
		v = num4;
		if (num4 <= 0.0)
		{
			s = 0.0;
		}
		else
		{
			s = num6 / num4;
		}
		if (num6 <= 0.0)
		{
			h = 0.0;
			return;
		}
		if (Math.Abs(num - num4) < 0.0001)
		{
			h = (num2 - num3) / num6;
		}
		else if (Math.Abs(num2 - num4) < 0.0001)
		{
			h = 2.0 + (num3 - num) / num6;
		}
		else
		{
			h = 4.0 + (num - num2) / num6;
		}
		h *= 60.0;
		if (h < 0.0)
		{
			h += 360.0;
		}
	}
}