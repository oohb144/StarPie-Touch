# StarPie Touch 来源与改动

本仓库 `oohb144/StarPie-Touch` 是 [StarPie 原项目（Star-Pie/StarPie）](https://github.com/Star-Pie/StarPie) 的独立衍生版本，并非原项目的官方发布。项目保留原作者署名及 [MIT 许可证](LICENSE)。本仓库从 2026 年 9 月的本地源码快照建立，未合并原仓库后续所有改动；因此不能把这里的版本号与上游同名版本视为同一份程序。

本衍生版主要增加全局双指轮盘、笔在场时的触控压制、简化的软件分层触控轮盘，以及抬手后动作派发的延迟优化。鼠标轮盘沿用 WPF 路径。触控轮盘的自定义图标、样式和动画尚未与原 WPF 轮盘等同。具体设备验收和测量范围见 [触控验证记录](docs/touch-pointer-phase0-phase1.md)。

原项目的官方插件仍由原项目的插件仓库提供，需由用户按原项目流程手动安装。本仓库的 Release 与应用内更新检查只指向 `oohb144/StarPie-Touch`。
