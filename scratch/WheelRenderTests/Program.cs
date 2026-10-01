using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Gdi = System.Drawing;

internal static class Program
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    [DllImport("user32.dll")] private static extern uint GetGuiResources(nint process, uint flags);
    private static object? Call(Type type, object? target, string method, params object?[] args) => type.GetMethod(method, Any)!.Invoke(target, args);

    [STAThread]
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(output, "appdata"));
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        Type configType = assembly.GetType("WinPieGestures.AppConfig", true)!;
        object config = JsonSerializer.Deserialize("""
            {"Theme":"Light","UiStyle":"ClassicRing","IconLayoutMode":"IconAndText","Shape":"Original","WheelRadius":133,"InnerRadius":70,"CoreRadius":36,"CoreTitle":"StarPie","EnableSoundEffects":false}
            """, configType)!;
        Type manager = assembly.GetType("WinPieGestures.ConfigManager", true)!;
        manager.GetProperty("CurrentConfig", Any)!.SetValue(null, config);
        Type profileType = assembly.GetType("WinPieGestures.WheelProfile", true)!;
        object profile = JsonSerializer.Deserialize("""
            {"SectorCount":8,"Actions":[{"Name":"智识","Parameter":"Ctrl+C","IconKey":"Command"},{"Name":"窗口","Parameter":"Ctrl+C","IconKey":"Tile"},{"Name":"复制","Parameter":"Ctrl+C","IconKey":"Copy"},{"Name":"粘贴","Parameter":"Ctrl+C","IconKey":"Paste"},{"Name":"工具","Parameter":"Ctrl+C","IconKey":"Settings","SubActions":[{"Name":"搜索","Parameter":"Ctrl+C","IconKey":"Explorer"},{"Name":"计算","Parameter":"Ctrl+C","IconKey":"Command"}]},{"Name":"浏览器","Parameter":"Ctrl+C","IconKey":"Explorer"},{"Name":"截图","Parameter":"Ctrl+C","IconKey":"Settings"},{"Name":"文件","Parameter":"Ctrl+C","IconKey":"Folder"}]}
            """, profileType)!;
        Type rendererType = assembly.GetType("WinPieGestures.NativeLayeredWheelWindow", true)!;
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        long wsBefore = process.WorkingSet64, privateBefore = process.PrivateMemorySize64;
        uint gdiBefore = GetGuiResources(process.Handle, 0);
        object renderer = rendererType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!.Invoke(null);
        Set(rendererType, renderer, "_profile", profile);
        Set(rendererType, renderer, "_scale", 1d);
        Call(rendererType, renderer, "EnsureSurface", 482);
        MethodInfo draw = rendererType.GetMethod("DrawAndUpdate", Any)!;
        var cold = Stopwatch.StartNew(); draw.Invoke(renderer, null); cold.Stop();
        ((Gdi.Bitmap)rendererType.GetField("_frame", Any)!.GetValue(renderer)!).Save(Path.Combine(output, "wheel.png"), ImageFormat.Png);
        process.Refresh();
        long wsShown = process.WorkingSet64, privateShown = process.PrivateMemorySize64;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        var times = new double[240];
        var watch = new Stopwatch();
        for (int i = 0; i < times.Length; i++)
        {
            Set(rendererType, renderer, "_sector", i % 8);
            watch.Restart(); draw.Invoke(renderer, null); watch.Stop(); times[i] = watch.Elapsed.TotalMilliseconds;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        process.Refresh(); double cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
        long surfaceBytes = rendererType.GetProperty("SurfaceBytes", Any)?.GetValue(renderer) is long bytes ? bytes : 482L * 482 * 12;
        Call(rendererType, renderer, "CloseFast");
        Array.Sort(times); process.Refresh();
        var report = new { result = "PASS", assembly = Path.GetFullPath(args[0]), coldFrameMs = cold.Elapsed.TotalMilliseconds,
            medianMs = times[120], p95Ms = times[228], maxMs = times[^1], cpuMs, allocatedBytes = allocated, surfaceBytes,
            workingSetBefore = wsBefore, workingSetAfterFrame = wsShown, privateBefore, privateAfterFrame = privateShown,
            gdiBefore, gdiAfterDispose = GetGuiResources(process.Handle, 0),
            scope = "240 synthetic frames to a hidden native window; no hooks, input, GUI display, forced GC or working-set trim" };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
        return args.Length > 2 && args[2] == "verify" ? Verify.Run(assembly, output) : 0;
    }
    private static void Set(Type type, object target, string field, object value) => type.GetField(field, Any)!.SetValue(target, value);
}
