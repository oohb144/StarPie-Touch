using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WinPieGestures;

public partial class ScreenEyedropperOverlay : Window
{
	private struct POINT
	{
		public int X;

		public int Y;
	}

	private readonly Canvas _loupeCanvas;

	private readonly Border _loupeBorder;

	private readonly TextBlock _hexLabel;

	private readonly Border _swatchBorder;

	public string CapturedHexColor { get; private set; } = "";

	[DllImport("user32.dll")]
	private static extern nint GetDC(nint hwnd);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(nint hwnd, nint hdc);

	[DllImport("gdi32.dll")]
	private static extern uint GetPixel(nint hdc, int nXPos, int nYPos);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	private const int SM_XVIRTUALSCREEN = 76;
	private const int SM_YVIRTUALSCREEN = 77;
	private const int SM_CXVIRTUALSCREEN = 78;
	private const int SM_CYVIRTUALSCREEN = 79;
	private const uint SWP_NOACTIVATE = 0x0010;
	private const uint SWP_NOZORDER = 0x0004;

	public ScreenEyedropperOverlay()
	{
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
		base.Topmost = true;
		base.ShowInTaskbar = false;
		base.Cursor = Cursors.Cross;
		base.Left = SystemParameters.VirtualScreenLeft;
		base.Top = SystemParameters.VirtualScreenTop;
		base.Width = SystemParameters.VirtualScreenWidth;
		base.Height = SystemParameters.VirtualScreenHeight;
		base.SourceInitialized += delegate
		{
			// 混合 DPI 多显示器下,SystemParameters 的 DIP 值与物理像素不一致,
			// 用 Win32 物理像素强制铺满整个虚拟屏幕。
			nint handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
			if (handle != IntPtr.Zero)
			{
				SetWindowPos(handle, IntPtr.Zero,
					GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
					GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN),
					SWP_NOACTIVATE | SWP_NOZORDER);
			}
		};
		_loupeCanvas = new Canvas
		{
			IsHitTestVisible = false
		};
		base.Content = _loupeCanvas;
		_loupeBorder = new Border
		{
			Width = 110.0,
			Height = 60.0,
			CornerRadius = new CornerRadius(8.0),
			Background = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(180, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.5),
			Padding = new Thickness(6.0),
			Effect = new DropShadowEffect
			{
				BlurRadius = 10.0,
				ShadowDepth = 2.0,
				Opacity = 0.4
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_swatchBorder = new Border
		{
			Width = 18.0,
			Height = 18.0,
			CornerRadius = new CornerRadius(4.0),
			BorderBrush = Brushes.White,
			BorderThickness = new Thickness(1.0),
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		};
		_hexLabel = new TextBlock
		{
			Text = "#FFFFFF",
			FontSize = 12.0,
			FontWeight = FontWeights.Bold,
			Foreground = Brushes.White,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel2.Children.Add(_swatchBorder);
		stackPanel2.Children.Add(_hexLabel);
		stackPanel.Children.Add(stackPanel2);
		TextBlock element = new TextBlock
		{
			Text = "单击取色 / Esc取消",
			FontSize = 9.0,
			Foreground = new SolidColorBrush(Color.FromArgb(200, 203, 213, 225)),
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(element);
		_loupeBorder.Child = stackPanel;
		_loupeCanvas.Children.Add(_loupeBorder);
		base.MouseMove += ScreenEyedropperOverlay_MouseMove;
		base.MouseDown += ScreenEyedropperOverlay_MouseDown;
		base.KeyDown += ScreenEyedropperOverlay_KeyDown;
	}

	private void ScreenEyedropperOverlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (GetCursorPos(out var lpPoint))
		{
			Color pixelColor = GetPixelColor(lpPoint.X, lpPoint.Y);
			string text = $"#FF{pixelColor.R:X2}{pixelColor.G:X2}{pixelColor.B:X2}";
			_hexLabel.Text = text;
			_swatchBorder.Background = new SolidColorBrush(pixelColor);
			Point position = e.GetPosition(this);
			double num = position.X + 20.0;
			double num2 = position.Y + 20.0;
			if (num + _loupeBorder.Width > base.ActualWidth)
			{
				num = position.X - _loupeBorder.Width - 10.0;
			}
			if (num2 + _loupeBorder.Height > base.ActualHeight)
			{
				num2 = position.Y - _loupeBorder.Height - 10.0;
			}
			Canvas.SetLeft(_loupeBorder, num);
			Canvas.SetTop(_loupeBorder, num2);
		}
	}

	private void ScreenEyedropperOverlay_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			if (GetCursorPos(out var lpPoint))
			{
				Color pixelColor = GetPixelColor(lpPoint.X, lpPoint.Y);
				CapturedHexColor = $"#FF{pixelColor.R:X2}{pixelColor.G:X2}{pixelColor.B:X2}";
				base.DialogResult = true;
				Close();
			}
		}
		else if (e.ChangedButton == MouseButton.Right)
		{
			base.DialogResult = false;
			Close();
		}
	}

	private void ScreenEyedropperOverlay_KeyDown(object sender, KeyEventArgs e)
	{
		if ((int)e.Key == 13)
		{
			base.DialogResult = false;
			Close();
		}
	}

	private static Color GetPixelColor(int x, int y)
	{
		nint dC = GetDC(IntPtr.Zero);
		uint pixel = GetPixel(dC, x, y);
		ReleaseDC(IntPtr.Zero, dC);
		byte r = (byte)(pixel & 0xFF);
		byte g = (byte)((pixel & 0xFF00) >> 8);
		byte b = (byte)((pixel & 0xFF0000) >> 16);
		return Color.FromRgb(r, g, b);
	}
}
