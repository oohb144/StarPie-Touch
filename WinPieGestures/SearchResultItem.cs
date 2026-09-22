using System;

namespace WinPieGestures;

/// <summary>
/// StarPie 原生检索引擎搜索结果条目模型
/// </summary>
public class SearchResultItem
{
	public string FullPath { get; set; } = "";
	public string FileName { get; set; } = "";
	public string Extension { get; set; } = "";
	public long Size { get; set; }
	public string SizeFormatted { get; set; } = "";
	public DateTime DateModified { get; set; }
	public string DateFormatted { get; set; } = "";
	public bool IsFolder { get; set; }
	public string Category { get; set; } = "Other"; // App, CAD, Doc, Folder, System, Video, Other
	public string CategoryDisplay { get; set; } = "文件";
	public string BadgeBg { get; set; } = "#183B82F6";
	public string BadgeFg { get; set; } = "#3B82F6";
	public string IconEmoji { get; set; } = "📄";
	public string EngineSource { get; set; } = "Native";
	public string Details => IsFolder ? "文件夹" : $"{SizeFormatted} · {DateFormatted}";
}
