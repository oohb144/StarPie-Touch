# 程序启动插件兼容性修复

2026-10-03：触屏版 `1.8.0-touch.3` 提供 SDK 1.5，但本机已安装的官方 `starpie.builtin.launch` 1.1.0 使用 SDK 1.8。实际 DLL 的成员解析发现三个缺失项：`ParameterField.FallbackParameterKey`、`ParameterField.FallbackValueMap`、`IHostActionInvoker.LaunchWithMode`，并缺少后者引用的 `ProcessLaunchMode` 类型。初始化读取 Parameters 时先触发缺失方法异常，此时目标应用尚未启动。

`1.8.0-touch.4` 回移这组启动接口。旧接口签名保持不变，新方法使用默认接口实现以兼容旧适配器；宿主实现对新方法检查 Process 能力，并复用原有默认启动路径。管理员启动使用 Shell 的 runas，普通用户启动使用 Explorer Shell；显式模式失败不回退。参数编辑表单根据插件声明回填旧 RunAsStandardUser，已有显式权限值优先，回填不修改配置。

本衍生版仍保留 SDK 1.5 基线和智识扩展，未实现完整上游 SDK 1.8。装载插件后、执行其构造函数和 Initialize 前，解析主 DLL 对 SDK 的直接类型与成员引用；缺失时报告具体接口。此检查不覆盖反射动态调用、泛型 TypeSpec 成员和插件私有依赖中的所有引用，因此不把通过检查视为所有插件功能已验证。

接口依据：[上游参数定义](https://github.com/Star-Pie/StarPie/blob/main/plugin/sdk/StarPie.Plugin.Abstractions/Actions.cs)、[上游启动接口](https://github.com/Star-Pie/StarPie/blob/main/plugin/sdk/StarPie.Plugin.Abstractions/Services.cs)、[官方 LaunchAction](https://github.com/Star-Pie/StarPie-Official-Plugins/blob/main/src/StarPie.Plugin.Launch/LaunchAction.cs)。

验证代码在 `scratch/PluginCompatibilityTests` 与 `scratch/LaunchPluginTests`。前者解析真实安装 DLL 的 SDK 成员引用，可分别对旧 SDK 和修复 SDK 验证失败与通过；后者隔离 LOCALAPPDATA 和插件目录，使用真实官方 Launch DLL 跑初始化、注册、类型路由与进程启动，子进程写入标记以证明参数已传递。权限分支通过可注入的进程启动委托验证，避免弹 UAC 或启动管理员程序。测试保留沙箱，不批量删除文件。

构建时可使用 `.NET 8` 的 `UseArtifactsOutput=true` 与工作区内 `ArtifactsPath`，避开历史 obj 输出的访问权限问题。先 `dotnet build`，再运行 artifacts 下的测试 EXE；当前 SDK 的 `dotnet run` 不会正确采用此输出位置。

升级时退出旧程序，再运行新发行包的 StarPie.exe。配置和已安装插件继续从原 LOCALAPPDATA 读取。主程序包不包含官方插件 DLL，不自动下载或重装插件。
