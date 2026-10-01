using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gdi = System.Drawing;
using Gdi2D = System.Drawing.Drawing2D;

internal static class Verify
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetGuiResources(nint process, uint flags);
    private static readonly List<string> Failures = new();
    private static int _checks;
    private static void Check(bool ok, string name) { _checks++; if (!ok) Failures.Add(name); }
    private static object? Get(Type t, object o, string name) => t.GetField(name, Any)!.GetValue(o);
    private static void Set(Type t, object o, string name, object value) => t.GetField(name, Any)!.SetValue(o, value);
    private static object? Call(Type t, object o, string name, params object?[] args) => t.GetMethod(name, Any)!.Invoke(o, args);
    private static void Property(object o, string name, object value) => o.GetType().GetProperty(name)!.SetValue(o, value);
    internal static int Run(Assembly host, string output)
    {
        Type renderer = host.GetType("WinPieGestures.NativeLayeredWheelWindow", true)!;
        Type configType = host.GetType("WinPieGestures.AppConfig", true)!;
        Type profileType = host.GetType("WinPieGestures.WheelProfile", true)!;
        var cfg = JsonSerializer.Deserialize("""{"Theme":"Light","UiStyle":"ClassicRing","IconLayoutMode":"IconOnly","CoreSubtitle":"Touch","EnableSoundEffects":false} """, configType)!;
        host.GetType("WinPieGestures.ConfigManager")!.GetProperty("CurrentConfig", Any)!.SetValue(null, cfg);
        object wheel = renderer.GetConstructor(Any & ~BindingFlags.Static, null, Type.EmptyTypes, null)!.Invoke(null);
        try
        {
            long revision = 0;
            object NewProfile(int count) => JsonSerializer.Deserialize(JsonSerializer.Serialize(new { SectorCount = count,
                Actions = Enumerable.Range(0, count).Select(i => new { Name = "动作" + i, Parameter = "Ctrl+C", IconKey = i % 2 == 0 ? "Copy" : "Paste",
                    SubActions = i == 0 ? new[] { new { Name = "子一", Parameter = "Ctrl+A", IconKey = "Folder" }, new { Name = "子二", Parameter = "Ctrl+V", IconKey = "Command" } } : [] }) }), profileType)!;
            Set(renderer, wheel, "_profile", NewProfile(8));
            void Draw()
            {
                Set(renderer, wheel, "_configurationRevision", ++revision);
                Call(renderer, wheel, "EnsureSurface", 520);
                Call(renderer, wheel, "DrawAndUpdate");
            }
            object rootOnly = JsonSerializer.Deserialize("""{"SectorCount":4,"Actions":[{"Name":"自定义","IconKey":"Copy","SubActions":[{"Name":"子动作","Parameter":"Ctrl+C"}]}]}""", profileType)!;
            for (int i = 0; i < 3; i++) Call(profileType, rootOnly, "EnsureLayers");
            var rootActions = (System.Collections.IList)profileType.GetProperty("Actions")!.GetValue(rootOnly)!;
            Check((string)rootActions[0]!.GetType().GetProperty("Name")!.GetValue(rootActions[0])! == "自定义", "root/layer alias repair preserves configured icons and sub-actions");
            Draw();
            object scene = Get(renderer, wheel, "_scene")!;
            Call(renderer, wheel, "DrawAndUpdate");
            Check(ReferenceEquals(scene, Get(renderer, wheel, "_scene")), "warm repaint reuses scene");
            Check(!IsWindowVisible((nint)Get(renderer, wheel, "_window")!), "test HWND stays hidden");
            var bitmap = (Gdi.Bitmap)Get(renderer, wheel, "_frame")!;
            Check(bitmap.GetPixel(0, 0).A == 0, "transparent surface border");
            var normal = bitmap.GetPixel(380, 260);
            Check(normal.R > 230 && normal.B > 230, "original light theme palette");
            nint bits = (nint)Get(renderer, wheel, "_bits")!;
            int offset = (260 * 520 + 380) * 4;
            Check(Marshal.ReadByte(bits, offset + 3) == normal.A && Math.Abs(Marshal.ReadByte(bits, offset + 2) - normal.R * normal.A / 255) <= 1,
                "frame bitmap and premultiplied DIB share pixel storage");
            Check((long)renderer.GetProperty("SurfaceBytes", Any)!.GetValue(wheel)! == 520L * 520 * 4, "one pixel buffer");
            Set(renderer, wheel, "_sector", 0); Call(renderer, wheel, "DrawAndUpdate");
            var selected = bitmap.GetPixel(380, 260);
            Check(selected.B > 180 && selected.R < 70, "theme highlight at sector zero/east");
            Check(ReferenceEquals(scene, Get(renderer, wheel, "_scene")), "highlight preserves cached resources");
            // A clipped incremental repaint must be pixel-identical to a fresh
            // full repaint, including translucent backgrounds and halo cleanup.
            foreach (string theme in new[] { "Light", "Dark", "GlacialIce" })
                foreach (string submenu in new[] { "Wheel", "Fan" })
                {
                    Property(cfg, "Theme", theme); Property(cfg, "SubmenuStyle", submenu); Draw();
                    foreach (var state in new[] { (7, -1, false, false), (0, 1, true, false), (0, 0, true, false),
                        (0, 0, true, true), (3, -1, false, false), (-1, -1, false, false) })
                    {
                        Set(renderer, wheel, "_sector", state.Item1); Set(renderer, wheel, "_subSector", state.Item2);
                        Set(renderer, wheel, "_showSubTier", state.Item3); Set(renderer, wheel, "_escaped", state.Item4);
                        Call(renderer, wheel, "DrawAndUpdate");
                        byte[] partial = Pixels((Gdi.Bitmap)Get(renderer, wheel, "_frame")!);
                        ((IDisposable)Get(renderer, wheel, "_scene")!).Dispose(); renderer.GetField("_scene", Any)!.SetValue(wheel, null);
                        Call(renderer, wheel, "DrawAndUpdate");
                        Check(partial.SequenceEqual(Pixels((Gdi.Bitmap)Get(renderer, wheel, "_frame")!)), $"incremental equals full {theme}/{submenu}/{state}");
                    }
                }
            foreach (string theme in new[] { "Light", "Dark", "MatchaForest", "GlacialIce", "MorandiMuted", "Custom" })
                foreach (string style in new[] { "ClassicRing", "CleanSectors", "Glassmorphism" })
                    foreach (string shape in new[] { "Original", "Circle", "HexagonHive", "RoundedCapsule" })
                    {
                        Property(cfg, "Theme", theme); Property(cfg, "UiStyle", style); Property(cfg, "Shape", shape); Draw();
                        Check(!ReferenceEquals(scene, Get(renderer, wheel, "_scene")), $"rebuild {theme}/{style}/{shape}");
                        scene = Get(renderer, wheel, "_scene")!;
                    }
            Property(cfg, "Theme", "Light"); Property(cfg, "UiStyle", "ClassicRing"); Property(cfg, "Shape", "Original");
            foreach (int count in new[] { 4, 8, 12 })
                foreach (string shape in new[] { "Original", "Circle", "HexagonHive", "RoundedCapsule" })
                {
                    Set(renderer, wheel, "_profile", NewProfile(count)); Property(cfg, "Shape", shape); Draw();
                    var slots = (System.Collections.IEnumerable)Get(scene.GetType(), Get(renderer, wheel, "_scene")!, "_slots")!;
                    int main = 0;
                    foreach (object slot in slots)
                    {
                        int sub = (int)slot.GetType().GetProperty("SubIndex")!.GetValue(slot)!;
                        if (sub >= 0) continue;
                        int index = (int)slot.GetType().GetProperty("Parent")!.GetValue(slot)!;
                        var path = (Gdi2D.GraphicsPath)slot.GetType().GetProperty("Shape")!.GetValue(slot)!;
                        double a = index * Math.PI * 2 / count;
                        Check(path.IsVisible((float)(260 + Math.Cos(a) * 101.5), (float)(260 + Math.Sin(a) * 101.5)), $"geometry direction {count}/{shape}/{index}");
                        Check(slot.GetType().GetProperty("Icon")!.GetValue(slot) != null, "vector icon present"); main++;
                    }
                    Check(main == count, "sector count " + count);
                    ((Gdi.Bitmap)Get(renderer, wheel, "_frame")!).Save(Path.Combine(output, $"wheel-{count}-{shape}.png"), ImageFormat.Png);
                }
            Set(renderer, wheel, "_profile", NewProfile(8));
            foreach (string layout in new[] { "IconOnly", "IconAndText", "TextOnly" })
                foreach (string submenu in new[] { "Wheel", "Fan" })
                {
                    Property(cfg, "IconLayoutMode", layout); Property(cfg, "SubmenuStyle", submenu); Property(cfg, "Shape", "Original");
                    Set(renderer, wheel, "_sector", 0); Set(renderer, wheel, "_subSector", 1); Set(renderer, wheel, "_showSubTier", true); Draw();
                    ((Gdi.Bitmap)Get(renderer, wheel, "_frame")!).Save(Path.Combine(output, $"wheel-{layout}-{submenu}.png"), ImageFormat.Png);
                    Check(((Gdi.Bitmap)Get(renderer, wheel, "_frame")!).GetPixel(0, 0).A == 0, "submenu stays within surface");
                }
            Set(renderer, wheel, "_scale", 2d); Call(renderer, wheel, "EnsureSurface", 1040); Call(renderer, wheel, "DrawAndUpdate");
            Check((long)renderer.GetProperty("SurfaceBytes", Any)!.GetValue(wheel)! == 1040L * 1040 * 4, "DPI resize uses one buffer");
            // Releasing idle resources must never tear down an active gesture.
            Set(renderer, wheel, "_presented", true); Call(renderer, wheel, "IdleRelease_Tick", null, EventArgs.Empty);
            Check(Get(renderer, wheel, "_frame") != null, "idle tick guards visible gesture");
            Call(renderer, wheel, "Dismiss", 999L); Check((bool)Get(renderer, wheel, "_presented")!, "stale dismiss ignored");
            Call(renderer, wheel, "Dismiss", 0L);
            Check(((System.Windows.Threading.DispatcherTimer)Get(renderer, wheel, "_idleRelease")!).IsEnabled, "dismiss schedules one-shot idle release");
            Call(renderer, wheel, "IdleRelease_Tick", null, EventArgs.Empty);
            Check(Get(renderer, wheel, "_scene") == null && Get(renderer, wheel, "_frame") == null && (nint)Get(renderer, wheel, "_dib")! == 0,
                "idle release disposes scene, wrapper and DIB");
            Check(!((System.Windows.Threading.DispatcherTimer)Get(renderer, wheel, "_idleRelease")!).IsEnabled, "idle timer stops");
            Call(renderer, wheel, "EnsureSurface", 520); Set(renderer, wheel, "_scale", 1d); Call(renderer, wheel, "DrawAndUpdate");
            Check(Get(renderer, wheel, "_scene") != null, "wheel rebuilds after idle release");
            object layers = JsonSerializer.Deserialize("""{"Layers":[{"SectorCount":4,"Name":"第一层","Actions":[{"Name":"复制","Parameter":"Ctrl+C"}]},{"SectorCount":8,"Name":"第二层","Actions":[{"Name":"粘贴","Parameter":"Ctrl+V"}]}]}""", profileType)!;
            Set(renderer, wheel, "_profile", layers); Draw(); object firstLayer = Get(renderer, wheel, "_scene")!;
            Check((int)profileType.GetProperty("SectorCount")!.GetValue(layers)! == 4, "fresh layered profile normalizes roots before rendering");
            Call(renderer, wheel, "SwitchToLayer", 1); Call(renderer, wheel, "DrawAndUpdate");
            Check(!ReferenceEquals(firstLayer, Get(renderer, wheel, "_scene")) && (int)profileType.GetProperty("SectorCount")!.GetValue(layers)! == 8, "layer switch rebuilds scene and sector geometry");
            Set(renderer, wheel, "_profile", NewProfile(8)); Draw();
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            uint gdiBefore = GetGuiResources(process.Handle, 0);
            for (int i = 0; i < 30; i++) { Call(renderer, wheel, "ReleaseSurface"); Call(renderer, wheel, "EnsureSurface", 520); Call(renderer, wheel, "DrawAndUpdate"); }
            Check(GetGuiResources(process.Handle, 0) <= gdiBefore + 2, "rebuild loop does not grow GDI handles");
            // Cached raster conversion must not depend on shell/file IO or D3D.
            byte[] rgba = Enumerable.Repeat((byte)255, 32 * 16 * 4).ToArray();
            var source = BitmapSource.Create(32, 16, 96, 96, PixelFormats.Pbgra32, null, rgba, 32 * 4); source.Freeze();
            var iconCache = (System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource>)host.GetType("WinPieGestures.IconHelper")!.GetField("_pinnedIcons", Any)!.GetValue(null)!;
            iconCache["test-app.exe"] = source; iconCache["custom_img:test-image.png"] = source;
            var profile = NewProfile(8); Set(renderer, wheel, "_profile", profile);
            Call(profileType, profile, "EnsureLayers");
            var actions = (System.Collections.IList)profileType.GetProperty("Actions")!.GetValue(profile)!;
            Property(actions[0]!, "InheritAppIconPath", "test-app.exe");
            Property(cfg, "ShowCoreIcon", true); Property(cfg, "CoreCustomImagePath", "test-image.png");
            Property(cfg, "CoreIconScale", 2d); Property(cfg, "CoreImageOffsetX", 8d);
            Set(renderer, wheel, "_sector", -1); Set(renderer, wheel, "_showSubTier", false); Set(renderer, wheel, "_escaped", false); Draw();
            object rasterScene = Get(renderer, wheel, "_scene")!;
            Check(Get(rasterScene.GetType(), rasterScene, "_coreImage") is Gdi.Bitmap, "cached core raster converted");
            Check(((Gdi.Bitmap)Get(renderer, wheel, "_frame")!).GetPixel(295, 295).A == 0, "scaled core image stays clipped to circle");
            Check(((Gdi.Bitmap)Get(renderer, wheel, "_frame")!).GetPixel(260, 260).R == 255, "core image draws in center");
            ((Gdi.Bitmap)Get(renderer, wheel, "_frame")!).Save(Path.Combine(output, "wheel-cached-image.png"), ImageFormat.Png);
            Property(cfg, "CoreCustomImagePath", "missing.png"); Property(cfg, "CoreCustomIconSvg", "broken path"); Draw();
            Check(Get(Get(renderer, wheel, "_scene")!.GetType(), Get(renderer, wheel, "_scene")!, "_coreIcon") != null, "invalid core SVG falls back safely");
            foreach (string style in new[] { "ClassicRing", "CleanSectors", "Glassmorphism" })
            {
                object palette = host.GetType("WinPieGestures.StyleRendererFactory")!.GetMethod("CreateRenderer")!.Invoke(null, [style])!;
                Call(palette.GetType(), palette, "Initialize", "Light", cfg);
                foreach (string name in new[] { "DefaultSectorBrush", "HighlightSectorBrush", "SectorBorderBrush", "HighlightBorderBrush", "TextColorBrush", "CoreBgBrush", "CoreBorderBrush" })
                    Check(((Freezable)palette.GetType().GetProperty(name)!.GetValue(palette)!).IsFrozen, "frozen palette " + style + "/" + name);
            }
        }
        catch (Exception ex) { Failures.Add(ex.ToString()); }
        finally { Call(renderer, wheel, "CloseFast"); Call(renderer, wheel, "CloseFast"); }
        var report = new { checks = _checks, failures = Failures, result = Failures.Count == 0 ? "PASS" : "FAIL",
            limitations = "Hidden HWND/offscreen only; no hardware touch, live UIA, application hooks or desktop input" };
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report)); return Failures.Count == 0 ? 0 : 1;
    }

    private static byte[] Pixels(Gdi.Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Gdi.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try { var bytes = new byte[data.Stride * bitmap.Height]; Marshal.Copy(data.Scan0, bytes, 0, bytes.Length); return bytes; }
        finally { bitmap.UnlockBits(data); }
    }
}
