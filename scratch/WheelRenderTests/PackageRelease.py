"""Archive verified touch.3 outputs without removing files or touching user data."""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

workspace = Path(__file__).resolve().parents[2]
release = workspace / "releases/v1.8.0-touch.3"
validation = release / "Validation"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read(path):
    return json.loads((workspace / path).read_text(encoding="utf-8-sig"))


wheel = read("scratch/wheel-render-results/delivery-updated-2/verification.json")
standalone = read("scratch/wheel-render-results/standalone-verified/verification.json")
insight = read("scratch/wheel-insight-results/release-verified/report.json")
touch = read("releases/v1.8.0-touch.3/Validation/touch-recognizer.json")
bundle = read("releases/v1.8.0-touch.3/Validation/standalone-bundle.json")
assert all(report["result"] == "PASS" for report in [wheel, standalone, insight, touch, bundle])
assert wheel["checks"] == standalone["checks"] == 362
assert insight["checks"] == 222 and touch["checks"] == 12
assert (release / "Standalone/StarPie.exe").stat().st_size == bundle["bytes"]
assert digest(release / "Standalone/StarPie.exe") == digest(workspace / "scratch/wheel-standalone-complete/StarPie.exe")

shutil.copy2(workspace / "scratch/wheel-render-results/standalone-verified/verification.json", validation / "Wheel/standalone-verification.json")
for file in (workspace / "scratch/wheel-insight-results/release-verified").glob("*.png"):
    shutil.copy2(file, validation / "ContentInsight" / file.name)
shutil.copy2(workspace / "scratch/wheel-insight-results/release-verified/report.json", validation / "ContentInsight/report.json")

for name in ["Lightweight", "Standalone"]:
    folder = release / name
    for source, target in [("LICENSE", "LICENSE"), ("docs/content-insight.md", "CONTENT_INSIGHT.md"),
                           ("docs/touch-wheel-rendering.md", "TOUCH_WHEEL_RENDERING.md")]:
        shutil.copy2(workspace / source, folder / target)
    requirement = "需要 Windows x64 的 .NET 8 桌面运行时。" if name == "Lightweight" else "内置 .NET 8 桌面运行时，解压即可运行。"
    (folder / "README.md").write_text(f"""# StarPie Touch 1.8.0-touch.3

{requirement}

退出托盘中的旧版本，再运行本目录的 `StarPie.exe`。已有轮盘与配置继续使用。
双指按住并同向滑动呼出轮盘，抬手执行。触屏沿用原有主题、图标与形状；隐藏后按需释放绘图资源。
智识插件单独安装，见 [安装与使用](CONTENT_INSIGHT.md)。插件压缩包不包含在主程序包内。
外观与本地对照结果见 [验证说明](TOUCH_WHEEL_RENDERING.md)。数字来自离屏测试；实机触控、完整 GUI 和 OCR 提供方仍待验收。

原 StarPie 项目及本衍生版本采用随包的 MIT 许可证。
""", encoding="utf-8")
shutil.copy2(release / "Lightweight/StarPie.Plugin.Abstractions.xml", release / "Standalone/StarPie.Plugin.Abstractions.xml")

light_files = ["app_icon.ico", "Microsoft.Windows.SDK.NET.dll", "StarPie.deps.json", "StarPie.dll", "StarPie.exe",
               "StarPie.Plugin.Abstractions.dll", "StarPie.Plugin.Abstractions.xml", "StarPie.runtimeconfig.json",
               "tray_icon.ico", "WinRT.Runtime.dll", "LICENSE", "README.md", "CONTENT_INSIGHT.md", "TOUCH_WHEEL_RENDERING.md"]
stand_files = ["StarPie.exe", "StarPie.Plugin.Abstractions.xml", "LICENSE", "README.md", "CONTENT_INSIGHT.md", "TOUCH_WHEEL_RENDERING.md"]
plugin_files = ["StarPie.Plugin.ContentInsight.dll", "plugin.json", "LICENSE", "README.md"]
archives = []
for directory, archive_name, files in [
    ("Lightweight", "StarPie-v1.8.0-touch.3-Lightweight-win-x64.zip", light_files),
    ("Standalone", "StarPie-v1.8.0-touch.3-Standalone-win-x64.zip", stand_files),
    ("ContentInsight", "StarPie-ContentInsight-v1.0.0-win-x64.zip", plugin_files),
]:
    archive_path = release / archive_name
    assert not archive_path.exists(), "Never overwrite an existing release archive"
    assert all((release / directory / file).is_file() for file in files)
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for file in files:
            archive.write(release / directory / file, file)
    with zipfile.ZipFile(archive_path) as archive:
        assert archive.testzip() is None
        assert archive.namelist() == files
        assert not any(file.endswith(".pdb") or file.startswith("plugin/") for file in archive.namelist())
        if directory != "ContentInsight":
            assert not any(file.startswith("StarPie.Plugin.") and file != "StarPie.Plugin.Abstractions.dll"
                           and file != "StarPie.Plugin.Abstractions.xml" for file in archive.namelist())
        assert hashlib.sha256(archive.read(files[0])).hexdigest() == digest(release / directory / files[0])
    archives.append({"archive": archive_name, "bytes": archive_path.stat().st_size,
                     "sha256": digest(archive_path), "files": files})

source_files = set()
for directory in ["WinPieGestures", "StarPie.Plugin.Abstractions", "samples/ContentInsight", "scratch/WheelRenderTests", "scratch/ContentInsightTests", "scratch/TouchRecognizerTests"]:
    for path in (workspace / directory).rglob("*"):
        if path.is_file() and path.suffix in {".cs", ".xaml", ".csproj", ".py", ".json"} and not {"bin", "obj", "scratch"}.intersection(path.relative_to(workspace / directory).parts):
            source_files.add(path)
for file in ["AGENTS.md", "CHANGELOG.md", "README.md", "README_EN.md", "docs/touch-wheel-rendering.md", "docs/content-insight.md", "docs/touch-pointer-phase0-phase1.md", "installer/StarPie.iss", "installer/build-installer.ps1"]:
    source_files.add(workspace / file)
manifest = {"hostVersion": "1.8.0-touch.3", "sdkContract": "1.5", "pluginVersion": "1.0.0", "result": "PASS",
            "checks": {"wheelPerPublishedHost": 362, "contentInsight": 222, "touchRecognizer": 12},
            "build": {"configuration": "Release", "warnings": 0, "errors": 0},
            "published": ["Lightweight", "Standalone"], "runtime": "8.0.26",
            "standaloneBundle": {"files": bundle["files"], "bytes": bundle["bytes"], "result": "PASS"},
            "onlineNuGetAudit": "unavailable; disabled for standalone publish only",
            "limitations": ["Offscreen/model tests only; no visible GUI or desktop input", "Actual touch, UIA and OCR-provider acceptance pending", "Buffer/CPU benchmark is not complete app working-set evidence"],
            "artifacts": archives,
            "sourceHashes": {str(file.relative_to(workspace)).replace("\\", "/"): digest(file) for file in sorted(source_files)}}
(release / "BUILD_MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
(release / "SHA256SUMS.txt").write_text("".join(f"{item['sha256']}  {item['archive']}\n" for item in archives), encoding="utf-8")
print(json.dumps({"result": "PASS", "archives": archives, "sourceFiles": len(source_files)}, ensure_ascii=False))
