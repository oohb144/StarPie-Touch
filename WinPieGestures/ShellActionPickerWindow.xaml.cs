using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinPieGestures;

public class ShellToolItem
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Provider { get; set; } = "";
	public string Category { get; set; } = ""; // "Compress", "System", "Developer"
	public string Icon { get; set; } = "";
	public string Verb { get; set; } = "";
	public string TargetType { get; set; } = "";
	public string Requirement { get; set; } = "";
	public string Description { get; set; } = "";
	public string DefaultIconKey { get; set; } = "";

	public string Name => Title;
	public string IconKey => DefaultIconKey;
}

public partial class ShellActionPickerWindow : Window
{
	public ShellToolItem? SelectedTool { get; private set; }

	private string _activeCategory = "All";
	private ShellToolItem? _currentSelection;

	public static IReadOnlyList<ShellToolItem> ShellTools => PredefinedShellTools;

	private static readonly List<ShellToolItem> PredefinedShellTools = new()
	{
		// 1. 系统与常用增强
		new ShellToolItem
		{
			Id = "copy_path",
			Title = "复制文件/文件夹完整路径",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "📋",
			Verb = "Windows.CopyAsPath",
			TargetType = "任意文件 / 文件夹",
			Requirement = "前台选中文件或当前目录",
			Description = "将资源管理器中当前选中对象或当前打开目录的完整路径直接复制入系统剪贴板",
			DefaultIconKey = "Copy"
		},
		new ShellToolItem
		{
			Id = "builtin_ocr",
			Title = "StarPie 屏幕 OCR 快速识字",
			Provider = "Windows 10/11 WinRT 原生引擎",
			Category = "System",
			Icon = "📝",
			Verb = "StarPie.Builtin.ScreenOCR",
			TargetType = "全屏幕任意区域",
			Requirement = "全局可用 (无需选文件)",
			Description = "快速唤起本地离线 OCR 识别引擎，鼠标拉框截取屏幕任意文字直接存入剪贴板",
			DefaultIconKey = "Screenshot"
		},
		new ShellToolItem
		{
			Id = "run_as_admin",
			Title = "以管理员身份运行 (Run as Admin)",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "🛡️",
			Verb = "Windows.RunAs",
			TargetType = "可执行程序 / 脚本",
			Requirement = "选中 exe / bat / cmd / ps1",
			Description = "以特权 UAC 提示唤起选中的程序或命令脚本",
			DefaultIconKey = "Command"
		},
		new ShellToolItem
		{
			Id = "task_manager",
			Title = "打开 Windows 任务管理器",
			Provider = "Windows 系统工具",
			Category = "System",
			Icon = "📊",
			Verb = "Windows.TaskManager",
			TargetType = "系统级",
			Requirement = "全局可用",
			Description = "瞬间呼出 Windows 任务管理器查看 CPU、内存占用与后台进程状态",
			DefaultIconKey = "TaskManager"
		},
		new ShellToolItem
		{
			Id = "snipping_tool",
			Title = "Windows 原生截屏 (Win+Shift+S)",
			Provider = "Windows 系统工具",
			Category = "System",
			Icon = "✂️",
			Verb = "Windows.SnippingTool",
			TargetType = "系统级",
			Requirement = "全局可用",
			Description = "唤起 Windows 原生 Snipping Tool 进行自定义区域、窗口或全屏截屏",
			DefaultIconKey = "Screenshot"
		},
		new ShellToolItem
		{
			Id = "new_folder",
			Title = "新建文件夹 (Ctrl+Shift+N)",
			Provider = "资源管理器工具",
			Category = "System",
			Icon = "📁",
			Verb = "Windows.NewFolder",
			TargetType = "目录空白处",
			Requirement = "资源管理器窗口",
			Description = "在当前活跃的资源管理器窗口中就地创建新文件夹",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "file_properties",
			Title = "查看文件/文件夹属性 (Alt+Enter)",
			Provider = "资源管理器工具",
			Category = "System",
			Icon = "ℹ️",
			Verb = "Windows.Properties",
			TargetType = "文件或文件夹",
			Requirement = "选中文件或文件夹",
			Description = "直接打开当前选中对象的 Windows 系统属性对话框",
			DefaultIconKey = "Settings"
		},
		new ShellToolItem
		{
			Id = "lock_screen",
			Title = "快速锁定电脑屏幕 (Win+L)",
			Provider = "Windows 系统安全",
			Category = "System",
			Icon = "🔒",
			Verb = "Windows.Lock",
			TargetType = "系统级",
			Requirement = "全局可用",
			Description = "立即锁定当前 Windows 桌面会话，保护个人隐私",
			DefaultIconKey = "Lock"
		},
		new ShellToolItem
		{
			Id = "empty_recycle_bin",
			Title = "清空桌面回收站",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "🗑️",
			Verb = "Windows.EmptyRecycleBin",
			TargetType = "系统级",
			Requirement = "全局可用",
			Description = "一键彻底清空桌面回收站中所有已删除的项目，释放磁盘存储",
			DefaultIconKey = "Delete"
		},

		// 2. 压缩与解压缩扩展
		new ShellToolItem
		{
			Id = "7z_extract_here",
			Title = "7-Zip: 解压到当前位置 (Extract Here)",
			Provider = "7-Zip Shell Extension",
			Category = "Compress",
			Icon = "📦",
			Verb = "7-Zip.ExtractHere",
			TargetType = "压缩包 (*.zip, *.7z, *.rar, *.tar)",
			Requirement = "选中压缩包文件",
			Description = "在当前所在目录下就地提取解压选中的压缩包文件",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "7z_extract_folder",
			Title = "7-Zip: 解压到独立同名子文件夹",
			Provider = "7-Zip Shell Extension",
			Category = "Compress",
			Icon = "🗂️",
			Verb = "7-Zip.ExtractToFolder",
			TargetType = "压缩包文件",
			Requirement = "选中压缩包文件",
			Description = "以压缩包名称自动新建同名独立子目录并解压其全部文件",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "bandizip_extract",
			Title = "Bandizip: 智能自动解压",
			Provider = "Bandizip Shell Extension",
			Category = "Compress",
			Icon = "🗜️",
			Verb = "Bandizip.AutoExtract",
			TargetType = "压缩包文件",
			Requirement = "选中压缩包文件",
			Description = "智能判断结构：单文件包就地解压，多文件包自动新建子目录归类",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "winrar_extract",
			Title = "WinRAR: 解压到当前文件夹",
			Provider = "WinRAR Shell Extension",
			Category = "Compress",
			Icon = "📚",
			Verb = "WinRAR.ExtractHere",
			TargetType = "压缩包文件",
			Requirement = "选中压缩包文件",
			Description = "调用 WinRAR 将选中的压缩文件就地解压至当前所在目录",
			DefaultIconKey = "Folder"
		},

		// 3. 开发者与高效办公
		new ShellToolItem
		{
			Id = "vscode_open",
			Title = "通过 Visual Studio Code 打开",
			Provider = "VS Code Shell Extension",
			Category = "Developer",
			Icon = "💻",
			Verb = "VSCode.Open",
			TargetType = "文件 / 文件夹",
			Requirement = "选中对象或在目录内",
			Description = "将当前选中文件或当前打开的文件夹直接加载进 Visual Studio Code 编辑器",
			DefaultIconKey = "Code"
		},
		new ShellToolItem
		{
			Id = "git_bash_here",
			Title = "Git Bash Here (在此处打开 Git 终端)",
			Provider = "Git for Windows",
			Category = "Developer",
			Icon = "🐙",
			Verb = "Git.BashHere",
			TargetType = "目录 / 桌面空白处",
			Requirement = "在文件夹内或桌面",
			Description = "在当前所在的路径直接唤起 Git Bash 终端命令行环境",
			DefaultIconKey = "Terminal"
		},
		new ShellToolItem
		{
			Id = "windows_terminal",
			Title = "在当前目录打开 Windows Terminal",
			Provider = "Windows 终端",
			Category = "Developer",
			Icon = "🖥️",
			Verb = "Windows.Terminal",
			TargetType = "目录 / 桌面空白处",
			Requirement = "在文件夹内或桌面",
			Description = "在当前所在路径启动 Windows Terminal 现代化多标签终端",
			DefaultIconKey = "Terminal"
		},
		new ShellToolItem
		{
			Id = "cmd_here",
			Title = "在当前目录打开命令提示符 (CMD)",
			Provider = "Windows 原生终端",
			Category = "Developer",
			Icon = "📟",
			Verb = "Windows.CmdHere",
			TargetType = "目录 / 桌面空白处",
			Requirement = "在文件夹内或桌面",
			Description = "在当前打开的路径就地唤起 cmd.exe 命令行窗口",
			DefaultIconKey = "Command"
		},
		new ShellToolItem
		{
			Id = "powershell_here",
			Title = "在当前目录打开 PowerShell",
			Provider = "PowerShell",
			Category = "Developer",
			Icon = "⚡",
			Verb = "Windows.PowerShellHere",
			TargetType = "目录 / 桌面空白处",
			Requirement = "在文件夹内或桌面",
			Description = "在当前所在路径直接唤起 PowerShell 脚本终端环境",
			DefaultIconKey = "Command"
		}
	};

	public ShellActionPickerWindow(string? currentVerb = null)
	{
		InitializeComponent();
		AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig?.AppTheme ?? "System");

		if (!string.IsNullOrEmpty(currentVerb))
		{
			_currentSelection = PredefinedShellTools.FirstOrDefault(t => string.Equals(t.Verb, currentVerb, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Id, currentVerb, StringComparison.OrdinalIgnoreCase));
		}

		UpdateCategoryButtonsUi();
		RefreshActionItemsList();
	}

	private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		string query = SearchTextBox.Text.Trim();
		ClearSearchBtn.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
		RefreshActionItemsList();
	}

	private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
	{
		SearchTextBox.Text = "";
		SearchTextBox.Focus();
	}

	private void CatBtn_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is string cat)
		{
			_activeCategory = cat;
			UpdateCategoryButtonsUi();
			RefreshActionItemsList();
		}
	}

	private void UpdateCategoryButtonsUi()
	{
		Button[] buttons = new[] { CatAllBtn, CatCompressBtn, CatSystemBtn, CatDevBtn };
		foreach (Button b in buttons)
		{
			if (b == null) continue;
			bool isActive = string.Equals(b.Tag?.ToString(), _activeCategory, StringComparison.OrdinalIgnoreCase);
			if (isActive)
			{
				b.Background = (Brush)FindResource("AccentPrimaryBrush");
				b.Foreground = (Brush)FindResource("AccentTextBrush");
				b.BorderBrush = (Brush)FindResource("AccentHoverBrush");
			}
			else
			{
				b.Background = (Brush)FindResource("SubtleCardBrush");
				b.Foreground = (Brush)FindResource("TextSecondaryBrush");
				b.BorderBrush = (Brush)FindResource("CardBorderBrush");
			}
		}
	}

	private void RefreshActionItemsList()
	{
		ActionItemsPanel.Children.Clear();
		string keyword = SearchTextBox.Text.Trim().ToLowerInvariant();

		var filtered = PredefinedShellTools.Where(item =>
		{
			bool matchCat = _activeCategory == "All" || string.Equals(item.Category, _activeCategory, StringComparison.OrdinalIgnoreCase);
			bool matchKey = string.IsNullOrEmpty(keyword) ||
							item.Title.ToLowerInvariant().Contains(keyword) ||
							item.Provider.ToLowerInvariant().Contains(keyword) ||
							item.Verb.ToLowerInvariant().Contains(keyword) ||
							item.Description.ToLowerInvariant().Contains(keyword);
			return matchCat && matchKey;
		}).ToList();

		if (filtered.Count == 0)
		{
			TextBlock emptyLabel = new TextBlock
			{
				Text = "未找到匹配的右键或系统扩展功能，试试搜索 7-Zip、路径、OCR 或 终端",
				Foreground = (Brush)FindResource("TextSecondaryBrush"),
				FontSize = 12,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0, 40, 0, 40)
			};
			ActionItemsPanel.Children.Add(emptyLabel);
			return;
		}

		foreach (ShellToolItem tool in filtered)
		{
			ActionItemsPanel.Children.Add(CreateActionCard(tool));
		}
	}

	private FrameworkElement CreateActionCard(ShellToolItem item)
	{
		bool isSelected = _currentSelection != null && string.Equals(_currentSelection.Id, item.Id, StringComparison.OrdinalIgnoreCase);

		Border card = new Border
		{
			CornerRadius = new CornerRadius(8),
			BorderThickness = new Thickness(isSelected ? 1.5 : 1),
			BorderBrush = isSelected ? (Brush)FindResource("AccentPrimaryBrush") : (Brush)FindResource("CardBorderBrush"),
			Background = isSelected ? (Brush)FindResource("SubtleCardBrush") : Brushes.Transparent,
			Padding = new Thickness(10, 8, 10, 8),
			Margin = new Thickness(0, 0, 0, 6),
			Cursor = Cursors.Hand,
			Tag = item
		};

		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		// Icon
		Border iconBorder = new Border
		{
			Width = 34,
			Height = 34,
			CornerRadius = new CornerRadius(8),
			Background = (Brush)FindResource("SubtleCardBrush"),
			BorderBrush = (Brush)FindResource("CardBorderBrush"),
			BorderThickness = new Thickness(1),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center
		};

		string svg = !string.IsNullOrEmpty(item.DefaultIconKey) ? IconHelper.GetSvgPathByKey(item.DefaultIconKey) : "";
		if (!string.IsNullOrEmpty(svg))
		{
			try
			{
				var path = new System.Windows.Shapes.Path
				{
					Data = Geometry.Parse(svg),
					Fill = (Brush)FindResource("AccentPrimaryBrush"),
					Width = 16,
					Height = 16,
					Stretch = Stretch.Uniform,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				iconBorder.Child = path;
			}
			catch
			{
				svg = "";
			}
		}

		if (string.IsNullOrEmpty(svg))
		{
			TextBlock iconText = new TextBlock
			{
				Text = item.Icon,
				FontSize = 18,
				FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI"),
				Foreground = (Brush)FindResource("TextPrimaryBrush"),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			iconBorder.Child = iconText;
		}

		Grid.SetColumn(iconBorder, 0);
		grid.Children.Add(iconBorder);

		// Middle info
		StackPanel infoPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(4, 0, 8, 0)
		};

		StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal };
		TextBlock titleText = new TextBlock
		{
			Text = item.Title,
			FontSize = 12.5,
			FontWeight = FontWeights.SemiBold,
			Foreground = (Brush)FindResource("TextPrimaryBrush"),
			VerticalAlignment = VerticalAlignment.Center
		};
		titleRow.Children.Add(titleText);

		Border providerBadge = new Border
		{
			CornerRadius = new CornerRadius(4),
			Background = (Brush)FindResource("SubtleCardBrush"),
			BorderBrush = (Brush)FindResource("CardBorderBrush"),
			BorderThickness = new Thickness(1),
			Padding = new Thickness(6, 1, 6, 1),
			Margin = new Thickness(8, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock providerText = new TextBlock
		{
			Text = item.Provider,
			FontSize = 10,
			Foreground = (Brush)FindResource("TextSecondaryBrush")
		};
		providerBadge.Child = providerText;
		titleRow.Children.Add(providerBadge);

		infoPanel.Children.Add(titleRow);

		TextBlock descText = new TextBlock
		{
			Text = item.Description,
			FontSize = 11,
			Foreground = (Brush)FindResource("TextSecondaryBrush"),
			Margin = new Thickness(0, 3, 0, 0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		infoPanel.Children.Add(descText);

		Grid.SetColumn(infoPanel, 1);
		grid.Children.Add(infoPanel);

		// Right requirement + select button
		StackPanel rightPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};

		TextBlock reqText = new TextBlock
		{
			Text = item.Requirement,
			FontSize = 10.5,
			Foreground = (Brush)FindResource("TextMutedBrush"),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 10, 0)
		};
		rightPanel.Children.Add(reqText);

		Button selectBtn = new Button
		{
			Content = isSelected ? "✓ 已选" : "选择",
			Style = isSelected ? (Style)FindResource("PrimaryButtonStyle") : (Style)FindResource("ModernButtonStyle"),
			Height = 26,
			Padding = new Thickness(10, 0, 10, 0),
			FontSize = 11,
			Tag = item
		};
		selectBtn.Click += (s, e) =>
		{
			e.Handled = true;
			SelectTool(item);
		};
		rightPanel.Children.Add(selectBtn);

		Grid.SetColumn(rightPanel, 2);
		grid.Children.Add(rightPanel);

		card.Child = grid;

		// Mouse interactions
		card.MouseEnter += (s, e) =>
		{
			if (_currentSelection?.Id != item.Id)
			{
				card.Background = (Brush)FindResource("ButtonHoverBgBrush");
			}
		};
		card.MouseLeave += (s, e) =>
		{
			if (_currentSelection?.Id != item.Id)
			{
				card.Background = Brushes.Transparent;
			}
		};
		card.MouseLeftButtonDown += (s, e) =>
		{
			SelectTool(item);
			if (e.ClickCount == 2)
			{
				ConfirmBtn_Click(this, new RoutedEventArgs());
			}
		};

		return card;
	}

	private void SelectTool(ShellToolItem tool)
	{
		_currentSelection = tool;
		SelectedItemLabel.Text = $"{tool.Icon} {tool.Title} ({tool.Provider})";
		ConfirmBtn.IsEnabled = true;
		RefreshActionItemsList();
	}

	private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_currentSelection == null) return;
		SelectedTool = _currentSelection;
		DialogResult = true;
		Close();
	}

	private void CancelBtn_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}
}
