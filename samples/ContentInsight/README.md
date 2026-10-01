# Content Insight / 智识

StarPie Touch 的独立社区插件，包含选中文字、框选识屏和剪贴板三个动作。需要 `1.8.0-touch.2` 或更新的兼容宿主（SDK 1.5）。

构建：

```powershell
dotnet build samples/ContentInsight -c Release --artifacts-path scratch/insight-artifacts
```

分发目录只需插件 DLL 与相邻的 `plugin.json`；不携带宿主 SDK DLL。规则识别仅使用 BCL，无 NuGet 依赖。安装、轮盘配置、参数与实机验收详见 [使用文档](../../docs/content-insight.md)。
