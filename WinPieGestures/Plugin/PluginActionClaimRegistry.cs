using System;
using System.Collections.Generic;
using System.Linq;

namespace WinPieGestures.Plugins;

internal sealed class PluginTypeClaimBinding
{
    public string TypeName { get; init; } = "";
    public string PluginId { get; init; } = "";
    public string ContributionId { get; init; } = "";
    public string FullId => $"{PluginId}.{ContributionId}";
}

/// <summary>官方在线插件的旧动作类型兼容路由表。</summary>
internal static class PluginActionClaimRegistry
{
    private static readonly object Gate = new();
    private static Dictionary<string, PluginTypeClaimBinding> _claims =
        new(StringComparer.OrdinalIgnoreCase);

    // 这些类型已经完成官方插件交割。即使插件当前未安装，也不能再回退到主程序旧 switch，
    // 否则“插件是唯一执行方式”的语义会被破坏。
    private static readonly HashSet<string> OfficialClaimedTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Launch", "Folder", "OpenFolder", "WebUrl", "Url", "Command", "Ocr", "ScreenOcr",
        "ShellTool", "System", "MoveMonitor", "SwitchWindow", "Tile", "TileRestore", "ToggleTopmost", "WindowOpacity",
    };

    public static void Rebuild(IEnumerable<PluginRegistryEntry> entries)
    {
        var candidates = new Dictionary<string, List<PluginTypeClaimBinding>>(StringComparer.OrdinalIgnoreCase);

        foreach (PluginRegistryEntry entry in entries.Where(entry => entry.Official))
        {
            foreach (string wire in entry.ClaimedTypes ?? new List<string>())
            {
                List<StarPie.Plugin.PluginTypeClaim> parsed =
                    StarPie.Plugin.PluginTypeClaim.ParseAll(wire, out List<string> malformed);
                if (malformed.Count > 0 || parsed.Count != 1)
                {
                    AppLogger.LogWarn($"[plugin] {entry.Id} 的类型认领格式无效：{wire}");
                    continue;
                }

                StarPie.Plugin.PluginTypeClaim claim = parsed[0];
                if (string.Equals(claim.TypeName, StarPie.Plugin.PluginApi.ActionTypeName,
                        StringComparison.OrdinalIgnoreCase)
                    || BuiltinActionCatalog.TryGet(claim.TypeName, out _))
                {
                    AppLogger.LogError($"[plugin] {entry.Id} 试图认领保留或内建类型 {claim.TypeName}，已拒绝。");
                    continue;
                }

                if (!candidates.TryGetValue(claim.TypeName, out List<PluginTypeClaimBinding>? list))
                {
                    list = new List<PluginTypeClaimBinding>();
                    candidates[claim.TypeName] = list;
                }

                var binding = new PluginTypeClaimBinding
                {
                    TypeName = claim.TypeName,
                    PluginId = entry.Id,
                    ContributionId = claim.ContributionId,
                };
                list.Add(binding);

                // TileRestore 是拆分前的独立历史类型；现在由同一个 Tile 插件的 Restore 参数承载。
                // 认领别名只影响旧配置路由，不新增 UI 动作类型。
                if (string.Equals(claim.TypeName, "Tile", StringComparison.OrdinalIgnoreCase))
                {
                    if (!candidates.TryGetValue("TileRestore", out List<PluginTypeClaimBinding>? restoreList))
                    {
                        restoreList = new List<PluginTypeClaimBinding>();
                        candidates["TileRestore"] = restoreList;
                    }

                    restoreList.Add(new PluginTypeClaimBinding
                    {
                        TypeName = "TileRestore",
                        PluginId = entry.Id,
                        ContributionId = claim.ContributionId,
                    });
                }
            }
        }

        var next = new Dictionary<string, PluginTypeClaimBinding>(StringComparer.OrdinalIgnoreCase);
        foreach ((string type, List<PluginTypeClaimBinding> bindings) in candidates)
        {
            if (bindings.Count == 1)
            {
                next[type] = bindings[0];
                continue;
            }

            AppLogger.LogError($"[plugin] 顶层类型 {type} 被多个官方插件认领：{string.Join(", ", bindings.Select(x => x.PluginId))}；全部拒绝。");
        }

        lock (Gate) _claims = next;
    }

    public static bool IsOfficialClaimedType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && OfficialClaimedTypeNames.Contains(type.Trim());

    public static bool TryResolve(string? type, out PluginTypeClaimBinding binding)
    {
        binding = null!;
        if (string.IsNullOrWhiteSpace(type)) return false;
        lock (Gate) return _claims.TryGetValue(type.Trim(), out binding!);
    }

    public static IReadOnlyList<PluginTypeClaimBinding> Snapshot()
    {
        lock (Gate) return _claims.Values.ToArray();
    }
}
