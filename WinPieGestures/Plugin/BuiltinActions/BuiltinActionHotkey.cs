using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「快捷热键」的插件模型实现。
/// <para>
/// 参数只有一个（热键串），但手写面板上挂着两枚<b>编辑辅助</b>按钮
/// （⏸️ 暂停全局热键、⚙️ 拼装组合）。那两枚按钮是「帮你把值填进去」的辅助，
/// 不是参数本身 —— 所以参数模型里没有它们的位置，本轮也不动 UI，
/// 面板照旧、辅助按钮照旧，只有描述 / 校验 / 执行入口收敛到统一形状。
/// </para>
/// </summary>
internal sealed class BuiltinActionHotkey : IActionContribution
{
	private const string KeyHotkey = "hotkey";

	public ActionDescriptor Descriptor => new()
	{
		Id = "hotkey",
		DisplayName = I18n.T("ActionTypeHotkeyShort"),
		Description = null,
		Category = "",
		IconKey = null,

		// 【必须与现状一致】原 switch 分支在动作线程上同步执行。
		Kind = ActionKind.Sequential,
		TimeoutSeconds = 0,
	};

	public IReadOnlyList<ParameterField> Parameters => new ParameterField[]
	{
		new()
		{
			Key = KeyHotkey,
			Label = "快捷键",
			Type = ParameterFieldType.Hotkey,
			Required = true,
			Placeholder = "Ctrl+Alt+S",
		},
	};

	/// <summary>
	/// <b>比原行为更严，是刻意的</b>：原 <see cref="ActionExecutor.ExecuteHotkey"/>
	/// 拿到空串会静默返回 —— 用户按下去什么也没发生。这里在执行前拦下并说明原因。
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string hotkey = parameters != null && parameters.TryGetValue(KeyHotkey, out string? value)
			? value ?? ""
			: "";

		return string.IsNullOrWhiteSpace(hotkey)
			? "未设置快捷键，请在动作设置里录制一个组合键。"
			: null;
	}

	/// <summary>列表副标题。热键串本身就是最好的说明，直接回显。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string hotkey = parameters != null && parameters.TryGetValue(KeyHotkey, out string? value)
			? (value ?? "").Trim()
			: "";

		return hotkey.Length <= 48 ? hotkey : hotkey.Substring(0, 47) + "…";
	}

	/// <summary>
	/// 执行。异常照常冒泡到 <see cref="ActionExecutor.Execute"/> 的 <c>catch</c>（弹 MessageBox）——
	/// 内建动作失败必须让用户立刻知道，与插件动作「失败可忽略」相反。
	/// </summary>
	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string hotkey = input?.Parameter(KeyHotkey) ?? "";

		if (string.IsNullOrWhiteSpace(hotkey))
		{
			return Task.FromResult(ActionResult.Fail("未设置快捷键。"));
		}

		ActionExecutor.ExecuteHotkey(hotkey);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "Hotkey",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".hotkey",
		Contribution = new BuiltinActionHotkey(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyHotkey)) map[KeyHotkey] = action.Parameter ?? "";

			return map;
		},
	};
}
