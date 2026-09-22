using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinPieGestures;

/// <summary>
/// 音效生成源类型
/// </summary>
public enum SoundSourceType
{
	/// <summary>程序化极微波形合成 (零延迟 &lt; 2ms，常驻内存 &lt; 20KB)</summary>
	ProceduralWave,
	/// <summary>本地外部音频文件 (.wav / .mp3)</summary>
	CustomFile,
	/// <summary>借用内置预设片段 (Mechanical / Crisp / Bubble / Minimalist)</summary>
	BuiltInPreset,
	/// <summary>静音 / 禁用此事件</summary>
	Mute
}

/// <summary>
/// 单个交互事件的音效参数配置
/// </summary>
public class SoundEventConfig : INotifyPropertyChanged
{
	private SoundSourceType _sourceType = SoundSourceType.ProceduralWave;
	private string _wavePreset = "Sine1200";
	private string _builtInTheme = "Crisp";
	private string _customFilePath = string.Empty;
	private int _pitchSemitones = 0;
	private int _durationMs = 25;
	private double _relativeVolume = 0.8;

	public SoundType EventType { get; set; }

	public string EventName => EventType switch
	{
		SoundType.WheelPopup => "轮盘唤出 (Popup)",
		SoundType.SectorHover => "扇区划过 (Hover)",
		SoundType.SubmenuExpand => "二级展开 (Expand)",
		SoundType.ActionExecute => "动作触发 (Execute)",
		SoundType.GestureCancel => "脱离取消 (Cancel)",
		_ => EventType.ToString()
	};

	public string EventIcon => EventType switch
	{
		SoundType.WheelPopup => "🌟",
		SoundType.SectorHover => "🎯",
		SoundType.SubmenuExpand => "🌿",
		SoundType.ActionExecute => "⚡",
		SoundType.GestureCancel => "↩️",
		_ => "🔔"
	};

	public string EventDescription => EventType switch
	{
		SoundType.WheelPopup => "按下唤醒按键呼出轮盘瞬间的轻微唤醒声",
		SoundType.SectorHover => "光标划入不同扇区、切换高亮时的微动步进刻度感",
		SoundType.SubmenuExpand => "触碰级联扇区并展开二级子轮盘时的滑动音",
		SoundType.ActionExecute => "在目标扇区松开按键执行动作时的清脆敲击音",
		SoundType.GestureCancel => "外甩脱离或缩回中心放弃手势时的柔和消退音",
		_ => string.Empty
	};

	public SoundSourceType SourceType
	{
		get => _sourceType;
		set
		{
			if (_sourceType != value)
			{
				_sourceType = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsProcedural));
				OnPropertyChanged(nameof(IsCustomFile));
				OnPropertyChanged(nameof(IsBuiltIn));
			}
		}
	}

	public bool IsProcedural => _sourceType == SoundSourceType.ProceduralWave;
	public bool IsCustomFile => _sourceType == SoundSourceType.CustomFile;
	public bool IsBuiltIn => _sourceType == SoundSourceType.BuiltInPreset;

	public string WavePreset
	{
		get => _wavePreset;
		set { if (_wavePreset != value) { _wavePreset = value; OnPropertyChanged(); } }
	}

	public string BuiltInTheme
	{
		get => _builtInTheme;
		set { if (_builtInTheme != value) { _builtInTheme = value; OnPropertyChanged(); } }
	}

	public string CustomFilePath
	{
		get => _customFilePath;
		set { if (_customFilePath != value) { _customFilePath = value; OnPropertyChanged(); } }
	}

	public int PitchSemitones
	{
		get => _pitchSemitones;
		set
		{
			if (_pitchSemitones != value)
			{
				_pitchSemitones = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(PitchText));
			}
		}
	}

	public string PitchText => _pitchSemitones switch
	{
		> 0 => $"+{_pitchSemitones} st (高亢)",
		< 0 => $"{_pitchSemitones} st (沉稳)",
		_ => "0 st (标准基频)"
	};

	public int DurationMs
	{
		get => _durationMs;
		set
		{
			if (_durationMs != value)
			{
				_durationMs = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(DurationText));
			}
		}
	}

	public string DurationText => $"{_durationMs} ms";

	public double RelativeVolume
	{
		get => _relativeVolume;
		set
		{
			if (Math.Abs(_relativeVolume - value) > 0.001)
			{
				_relativeVolume = Math.Clamp(value, 0.0, 1.0);
				OnPropertyChanged();
				OnPropertyChanged(nameof(RelativeVolumePercent));
			}
		}
	}

	public string RelativeVolumePercent => $"{(int)Math.Round(_relativeVolume * 100)}%";

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? propName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
	}

	public SoundEventConfig Clone()
	{
		return new SoundEventConfig
		{
			EventType = this.EventType,
			SourceType = this.SourceType,
			WavePreset = this.WavePreset,
			BuiltInTheme = this.BuiltInTheme,
			CustomFilePath = this.CustomFilePath,
			PitchSemitones = this.PitchSemitones,
			DurationMs = this.DurationMs,
			RelativeVolume = this.RelativeVolume
		};
	}
}

/// <summary>
/// 自定义交互音效方案模型
/// </summary>
public class CustomSoundProfile : INotifyPropertyChanged
{
	private string _name = "未命名方案";
	private string _description = string.Empty;

	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string Name
	{
		get => _name;
		set { if (_name != value) { _name = value; OnPropertyChanged(); } }
	}

	public string Description
	{
		get => _description;
		set { if (_description != value) { _description = value; OnPropertyChanged(); } }
	}

	public bool IsBuiltIn { get; set; } = false;

	public List<SoundEventConfig> Events { get; set; } = new();

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? propName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
	}

	public CustomSoundProfile Clone()
	{
		var copy = new CustomSoundProfile
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = this.Name + " (副本)",
			Description = this.Description,
			IsBuiltIn = false
		};
		foreach (var ev in this.Events)
		{
			copy.Events.Add(ev.Clone());
		}
		return copy;
	}

	/// <summary>
	/// 获取预置的高保真交互音效演示方案集
	/// </summary>
	public static List<CustomSoundProfile> CreateDefaultDemoProfiles()
	{
		var list = new List<CustomSoundProfile>();

		// 1. 赛博电竞
		var cyber = new CustomSoundProfile
		{
			Id = "cyber_haptic",
			Name = "🎮 赛博电竞 (Cyber Haptic)",
			Description = "利落紧凑的数码极微脉冲波，零延迟高频响应，专为高敏操作与竞技盲操调校。",
			IsBuiltIn = true,
			Events = new List<SoundEventConfig>
			{
				new() { EventType = SoundType.WheelPopup, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Square850", PitchSemitones = 3, DurationMs = 20, RelativeVolume = 0.7 },
				new() { EventType = SoundType.SectorHover, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Pulse2ms", PitchSemitones = 5, DurationMs = 12, RelativeVolume = 0.65 },
				new() { EventType = SoundType.SubmenuExpand, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Square850", PitchSemitones = 8, DurationMs = 30, RelativeVolume = 0.75 },
				new() { EventType = SoundType.ActionExecute, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Pulse2ms", PitchSemitones = 10, DurationMs = 35, RelativeVolume = 0.95 },
				new() { EventType = SoundType.GestureCancel, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Sine1200", PitchSemitones = -4, DurationMs = 40, RelativeVolume = 0.5 }
			}
		};
		list.Add(cyber);

		// 2. 经典机械轴
		var blueSwitch = new CustomSoundProfile
		{
			Id = "blue_switch",
			Name = "⌨️ 经典机械轴 (Blue Switch)",
			Description = "段落轴下压与清脆回弹微动声，逼真模拟机械键盘刻度感与打击反馈。",
			IsBuiltIn = true,
			Events = new List<SoundEventConfig>
			{
				new() { EventType = SoundType.WheelPopup, SourceType = SoundSourceType.BuiltInPreset, BuiltInTheme = "Mechanical", WavePreset = "NoiseSnap", PitchSemitones = 0, DurationMs = 28, RelativeVolume = 0.8 },
				new() { EventType = SoundType.SectorHover, SourceType = SoundSourceType.ProceduralWave, WavePreset = "NoiseSnap", PitchSemitones = 1, DurationMs = 14, RelativeVolume = 0.7 },
				new() { EventType = SoundType.SubmenuExpand, SourceType = SoundSourceType.BuiltInPreset, BuiltInTheme = "Mechanical", WavePreset = "NoiseSnap", PitchSemitones = 4, DurationMs = 32, RelativeVolume = 0.85 },
				new() { EventType = SoundType.ActionExecute, SourceType = SoundSourceType.BuiltInPreset, BuiltInTheme = "Mechanical", WavePreset = "NoiseSnap", PitchSemitones = 6, DurationMs = 42, RelativeVolume = 1.0 },
				new() { EventType = SoundType.GestureCancel, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Sine1200", PitchSemitones = -3, DurationMs = 25, RelativeVolume = 0.45 }
			}
		};
		list.Add(blueSwitch);

		// 3. 水滴轻语
		var dewDrop = new CustomSoundProfile
		{
			Id = "dew_drop",
			Name = "🫧 水滴轻语 (Dew Drop)",
			Description = "温润柔和的水珠微破与泛音，极简空灵，营造沉浸专注且不打扰的安静体验。",
			IsBuiltIn = true,
			Events = new List<SoundEventConfig>
			{
				new() { EventType = SoundType.WheelPopup, SourceType = SoundSourceType.BuiltInPreset, BuiltInTheme = "Bubble", WavePreset = "BubbleWater", PitchSemitones = 0, DurationMs = 35, RelativeVolume = 0.75 },
				new() { EventType = SoundType.SectorHover, SourceType = SoundSourceType.ProceduralWave, WavePreset = "BubbleWater", PitchSemitones = 2, DurationMs = 18, RelativeVolume = 0.6 },
				new() { EventType = SoundType.SubmenuExpand, SourceType = SoundSourceType.BuiltInPreset, BuiltInTheme = "Bubble", WavePreset = "BubbleWater", PitchSemitones = 5, DurationMs = 40, RelativeVolume = 0.8 },
				new() { EventType = SoundType.ActionExecute, SourceType = SoundSourceType.BuiltInPreset, BuiltInTheme = "Bubble", WavePreset = "BubbleWater", PitchSemitones = 7, DurationMs = 45, RelativeVolume = 0.9 },
				new() { EventType = SoundType.GestureCancel, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Sine1200", PitchSemitones = -5, DurationMs = 30, RelativeVolume = 0.4 }
			}
		};
		list.Add(dewDrop);

		// 4. 用户专属方案
		var userCustom = new CustomSoundProfile
		{
			Id = "user_custom_1",
			Name = "🎨 我的专属方案 1 (Custom 1)",
			Description = "可自由混搭程序波形、系统预设与本地音频样本的高级个性化调校方案。",
			IsBuiltIn = false,
			Events = new List<SoundEventConfig>
			{
				new() { EventType = SoundType.WheelPopup, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Sine1200", PitchSemitones = 2, DurationMs = 25, RelativeVolume = 0.8 },
				new() { EventType = SoundType.SectorHover, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Square850", PitchSemitones = 4, DurationMs = 15, RelativeVolume = 0.7 },
				new() { EventType = SoundType.SubmenuExpand, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Pulse2ms", PitchSemitones = 6, DurationMs = 30, RelativeVolume = 0.85 },
				new() { EventType = SoundType.ActionExecute, SourceType = SoundSourceType.CustomFile, CustomFilePath = "C:\\Windows\\Media\\Speech On.wav", PitchSemitones = 0, DurationMs = 50, RelativeVolume = 0.9 },
				new() { EventType = SoundType.GestureCancel, SourceType = SoundSourceType.ProceduralWave, WavePreset = "Sine1200", PitchSemitones = -6, DurationMs = 35, RelativeVolume = 0.5 }
			}
		};
		list.Add(userCustom);

		return list;
	}
}
