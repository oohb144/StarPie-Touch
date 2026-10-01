# 智识无界面验证

从仓库根目录运行：

```powershell
dotnet build scratch/ContentInsightTests -c Release --artifacts-path scratch/insight-artifacts
dotnet scratch/insight-artifacts/bin/ContentInsightTests/release/ContentInsightTests.dll
```

测试不启动 GUI，不修改真实用户配置。每次创建独立 `scratch/insight-test-results/<随机ID>` 沙箱，保留报告、渲染图和插件数据，不执行卸载或批量删除。安装测试使用上述构建目录中的独立插件 DLL，不使用测试程序目录里混放宿主和 SDK 的 DLL。

覆盖规则限制、多语言、深浅主题离屏渲染、编辑后操作撤销、剪贴板独立快照、源窗口上下文不落盘、能力检查，以及实际宿主沙箱里的静态扫描、安装、启用、注册和完整停用。插件贡献点检查放在独立 NoInlining 方法内，避免测试局部变量持有贡献点而阻止可回收加载上下文卸载。

触屏、UIA 外部应用兼容性、真实剪贴板注入和 OCR 提供方不在本测试内；按 `docs/content-insight.md` 实机检查。旧 `scratch/check_i18n.py` 依赖已淘汰的字典格式，不作为这次本地化通过证据。
