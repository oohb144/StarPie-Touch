using System;
using System.Collections.Generic;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>
/// 把 <see cref="ActionItem"/> 上的<b>动作参数</b>投影成参数字典。
/// <para>
/// <b>为什么需要这一层</b>：动作参数长期存在 <see cref="ActionItem"/> 的若干裸字段上
/// （<c>Parameter</c> / <c>Arguments</c> / <c>CommandTerminal</c> …），而全项目有近两百处引用它们。
/// 整体搬进 <see cref="ActionItem.ExtensionData"/> 是一次高风险大改动，而认领了顶层类型的随包插件
/// 又<b>读不到宿主的 ActionItem</b>（那会把插件绑死在宿主内部类型签名上）。
/// 于是宿主在调用这类插件之前，先把这几个字段现读现装成字典 —— 持久化侧一行不动，
/// 执行侧却统一了。
/// </para>
/// <para>
/// <b>只投影「动作参数」，不投影外观</b>。<see cref="ActionItem"/> 上还有一大批字段
/// （<c>Name</c> / <c>IconKey</c> / <c>CustomTextColor</c> / <c>CustomFontSize</c> …），
/// 它们描述的是这个动作<b>长什么样</b>，与动作执行毫无关系。把外观字段也铺进字典，
/// 会让插件误以为它们是参数，进而在表单里列出来 —— 这个错误一旦进入插件生态就很难收回。
/// 所以这里是<b>显式白名单</b>，不是反射：新增一个动作参数字段必须同时改
/// <see cref="HostActionFields"/> 与下面的映射，漏改的表现是自检直接红，而不是运行期读到空值。
/// </para>
/// <para>
/// <b>键名用宿主的属性名</b>（<c>Parameter</c> 而不是 <c>path</c>）：认领了顶层类型的插件
/// 在语义上就是宿主的一部分（只是物理上分了个 DLL），说的自然是宿主的语言。
/// 社区插件不受这套约束 —— 它们用 <c>Type="Plugin"</c>、参数写 <c>ExtensionData</c>、
/// 键由插件自己起语义化名字。
/// </para>
/// </summary>
internal static class ActionParameterProjection
{
    /// <summary>
    /// 投影一份参数。<b>返回的是私有拷贝</b>：插件改写它污染不到用户正在编辑的配置对象。
    /// </summary>
    /// <remarks>
    /// <b>顺序很关键</b>：先铺 <see cref="ActionItem.ExtensionData"/>，再补裸字段。
    /// 将来参数真正迁移到新模型时，这个函数一行都不用改就能读到新值；
    /// 而在迁移之前，它读到的就是用户配置里的原值。
    /// </remarks>
    public static Dictionary<string, string> Project(ActionItem action)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (action == null) return map;

        if (action.ExtensionData != null)
        {
            foreach (KeyValuePair<string, string> pair in action.ExtensionData)
            {
                if (pair.Key != null) map[pair.Key] = pair.Value ?? "";
            }
        }

        // nameof 而不是字面量：宿主重命名字段时这里会变成编译错误，
        // 而不是在运行期把一份空字典交给插件（插件侧只会看到「参数没填」，
        // 而用户明明填过 —— 这类静默失效最难查）。
        Seed(map, nameof(action.Parameter), action.Parameter);
        Seed(map, nameof(action.Arguments), action.Arguments);
        Seed(map, nameof(action.CommandTerminal), action.CommandTerminal);
        Seed(map, nameof(action.BrowserChoice), action.BrowserChoice);
        Seed(map, nameof(action.BrowserPath), action.BrowserPath);

        // 布尔投影成不变文化字面量：宿主与插件两侧都按 InvariantCulture 解析，
        // 否则同一份配置在不同区域设置下会解析成不同的值。
        Seed(map, nameof(action.RunAsStandardUser), action.RunAsStandardUser ? "true" : "false");

        return map;
    }

    /// <summary>
    /// 某个参数键能否被投影出来。<b>供自检用</b>：
    /// <see cref="HostActionFields.All"/> 与上面的映射一旦对不上，
    /// 插件侧读到的就是空值，而且不会有任何报错 —— 只能靠自检把这条钉死。
    /// </summary>
    public static bool CanProject(string key) =>
        !string.IsNullOrWhiteSpace(key) && Project(new ActionItem()).ContainsKey(key);

    /// <summary>只在字典里没有这个键时补上；已有值（扩展数据或先补的裸字段）一律不覆盖。</summary>
    private static void Seed(Dictionary<string, string> map, string key, string? value)
    {
        if (!map.ContainsKey(key)) map[key] = value ?? "";
    }
}
