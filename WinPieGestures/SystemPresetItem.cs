namespace WinPieGestures;

public class SystemPresetItem
{
	private string _key = "";
	private string _categoryKey = "";
	private string _presetKey = "";
	private string _defaultNameKey = "";

	public string Key
	{
		get => _key;
		set
		{
			_key = value ?? string.Empty;
			_categoryKey = "SysCategory_" + _key;
			_presetKey = "SysPreset_" + _key;
			_defaultNameKey = "SysPresetName_" + _key;
		}
	}

	private string _category = "";
	public string Category
	{
		get
		{
			string loc = I18n.T(_categoryKey);
			return (!string.IsNullOrEmpty(loc) && !object.ReferenceEquals(loc, _categoryKey) && loc != _categoryKey) ? loc : _category;
		}
		set => _category = value ?? string.Empty;
	}

	private string _displayName = "";
	public string DisplayName
	{
		get
		{
			string loc = I18n.T(_presetKey);
			return (!string.IsNullOrEmpty(loc) && !object.ReferenceEquals(loc, _presetKey) && loc != _presetKey) ? loc : _displayName;
		}
		set => _displayName = value ?? string.Empty;
	}

	private string _defaultName = "";
	public string DefaultName
	{
		get
		{
			string loc = I18n.T(_defaultNameKey);
			return (!string.IsNullOrEmpty(loc) && !object.ReferenceEquals(loc, _defaultNameKey) && loc != _defaultNameKey) ? loc : _defaultName;
		}
		set => _defaultName = value ?? string.Empty;
	}

	public string DefaultIconKey { get; set; } = "";

	public string FormattedDisplay => "[" + Category + "] " + DisplayName;
}
