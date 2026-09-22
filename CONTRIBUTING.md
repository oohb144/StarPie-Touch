# 贡献指南 (Contributing Guide)

感谢你对 **StarPie (星盘)** 的关注与支持！我们欢迎一切形式的代码贡献、文档优化、设计建议与 Bug 反馈。

---

## 🛠️ 本地开发环境准备

1. **操作系统**：Windows 10 / 11 (x64)；
2. **.NET 8.0 SDK**：[下载并安装 .NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)；
3. **IDE / 编辑器**：Visual Studio 2022 (带 .NET 桌面开发工作负载) 或 VS Code / JetBrains Rider；
4. **Python 3.10+** (可选，用于运行端到端 GUI 自动化测试)：`pip install pytest pywinauto`。

---

## 🚀 提交流程与规范

1. **Fork 代码库** 并克隆至本地；
2. **基于 `main` 分支创建特性分支**：
   ```bash
   git checkout -b feature/your-feature-name
   # 或修复分支
   git checkout -b fix/your-bug-fix
   ```
3. **编写与验证代码**：
   - 保持 C# 编码风格与项目现有架构一致；
   - 新增 UI 字符串请同步在 `WinPieGestures/I18n.cs` 中添加四国语言（中/繁/英/日）翻译；
   - 运行自动化测试：`python -m pytest tests/test_settings.py -v` 确保 100% 绿灯。
4. **提交 Commit**（推荐采用约定式提交规范）：
   ```text
   feat: 增加新的轮盘渲染形态
   fix: 修复高分辨率缩放下的光晕偏移问题
   docs: 完善多语言配置文档
   ```
5. **发起 Pull Request (PR)**：
   - 清晰描述修改的背景、目的与实现细节；
   - 附带必要的界面截图或录屏；
   - 使用靠谱的AI对自己做的代码修改做一遍审查（上传审查结果或者意见的清晰截图）。

---

再次感谢你为 StarPie 开源社区做出的贡献！🎉
