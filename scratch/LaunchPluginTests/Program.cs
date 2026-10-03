using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using StarPie.Plugin;
using WinPieGestures;

internal static class Program
{
    private static readonly Assembly Host = typeof(App).Assembly;
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<string> Failures = new();
    private static int Checks;
    private static Type HostType(string name) => Host.GetType("WinPieGestures." + name, true)!;
    private static object? Call(Type type, string name, object? target, params object?[] arguments) => type.GetMethod(name, Any)!.Invoke(target, arguments);
    private static void Check(bool ok, string name) { Checks++; if (!ok) Failures.Add(name); }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--write-marker") { File.WriteAllText(args[1], "started"); return 0; }
        string output = Path.GetFullPath(args[0]);
        string source = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(output, "localappdata"));
        try
        {
            TestPermissions();
            TestPlugin(output, source);
        }
        catch (Exception ex) { Failures.Add(ex.ToString()); }
        var report = new { checks = Checks, failures = Failures, result = Failures.Count == 0 ? "PASS" : "FAIL",
            limitations = "No UAC dialog or live touchscreen test. Sandbox retained; no bulk deletion." };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
        AppLogger.Shutdown();
        return Failures.Count == 0 ? 0 : 1;
    }

    private static void TestPermissions()
    {
        Type executor = HostType("ProcessLaunchExecutor");
        MethodInfo start = executor.GetMethods(Any).Single(m => m.Name == "Start" && m.GetParameters().Length == 4);
        foreach (ProcessLaunchMode mode in Enum.GetValues<ProcessLaunchMode>())
        {
            int standardCalls = 0, processCalls = 0;
            ProcessStartInfo? received = null;
            Func<string, string, string, bool> standard = (_, _, _) => { standardCalls++; return false; };
            Func<ProcessStartInfo, bool> process = info => { processCalls++; received = info; return false; };
            var input = new ProcessStartInfo { FileName = "probe.exe", Arguments = "--test", WorkingDirectory = "C:\\", WindowStyle = ProcessWindowStyle.Hidden };
            bool success = (bool)start.Invoke(null, new object[] { input, mode, standard, process })!;
            Check(!success, "failed launch stays failed " + mode);
            Check(standardCalls == (mode == ProcessLaunchMode.StandardUser ? 1 : 0), "standard user route " + mode);
            Check(processCalls == (mode == ProcessLaunchMode.StandardUser ? 0 : 1), "no fallback " + mode);
            if (mode == ProcessLaunchMode.Administrator)
                Check(received?.Verb == "runas" && received.Arguments == "--test" && received.WindowStyle == ProcessWindowStyle.Hidden, "administrator request preserves arguments and visibility");
        }
        Type service = HostType("Plugins.PluginHostActionInvoker");
        var denied = (IHostActionInvoker)Activator.CreateInstance(service, Any, null, new object[] { "probe", PluginCapability.None }, null)!;
        try { denied.LaunchWithMode("", ProcessLaunchMode.Default); Check(false, "Process capability denied"); }
        catch (PluginCapabilityDeniedException) { Check(true, "Process capability denied"); }
        var allowed = (IHostActionInvoker)Activator.CreateInstance(service, Any, null, new object[] { "probe", PluginCapability.Process }, null)!;
        Check(!allowed.LaunchWithMode("", ProcessLaunchMode.Default), "empty path fails without process");
        Check(!allowed.LaunchWithMode("probe.exe", (ProcessLaunchMode)999), "invalid mode fails without process");
        MethodInfo guard = service.GetMethods(Any).Single(m => m.Name == "Guard" && m.GetParameters()[1].ParameterType == typeof(Func<bool>));
        Check(!(bool)guard.Invoke(allowed, new object[] { "probe", (Func<bool>)(() => false) })!, "host preserves a failed explicit launch result");
    }

    private static void TestPlugin(string output, string source)
    {
        string root = Path.Combine(output, "plugins");
        Directory.CreateDirectory(root);
        JsonNode registry = JsonNode.Parse(File.ReadAllText(Path.Combine(source, "registry.json")))!;
        JsonNode entry = registry["entries"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == "starpie.builtin.launch")!.DeepClone();
        registry["entries"] = new JsonArray(entry);
        File.WriteAllText(Path.Combine(root, "registry.json"), registry.ToJsonString());
        string copied = Path.Combine(root, "starpie.builtin.launch");
        Directory.CreateDirectory(copied);
        foreach (string name in new[] { "plugin.json", "StarPie.Plugin.Launch.dll" })
            File.Copy(Path.Combine(source, "starpie.builtin.launch", name), Path.Combine(copied, name));

        Type paths = HostType("Plugins.PluginPaths");
        Call(paths, "OverrideRootsForTesting", null, root, Path.Combine(output, "candidates"));
        Type host = HostType("Plugins.PluginHost");
        host.GetProperty("HeadlessMode", Any)!.SetValue(null, true);
        Call(host, "Initialize", null);
        object? instance = Call(host, "Find", null, "starpie.builtin.launch");
        Check(instance != null, "installed launch discovered");
        object?[] loadArgs = { "" };
        bool loaded = (bool)instance!.GetType().GetMethod("Load", Any)!.Invoke(instance, loadArgs)!;
        Check(loaded, "real Launch 1.1.0 Initialize: " + loadArgs[0]);
        if (!loaded) return;
        Check((int)instance.GetType().GetProperty("ActionCount", Any)!.GetValue(instance)! == 1, "one launch contribution registered");

        object catalog = host.GetField("Catalog", Any)!.GetValue(null)!;
        object?[] registrationArgs = { "starpie.builtin.launch.launch", null };
        Call(catalog.GetType(), "TryGetAction", catalog, registrationArgs);
        object registration = registrationArgs[1]!;
        var contribution = (IActionContribution)registration.GetType().GetProperty("Contribution", Any)!.GetValue(registration)!;
        ParameterField modeField = contribution.Parameters.Single(f => f.FallbackParameterKey == "RunAsStandardUser");
        Type form = HostType("Plugins.PluginParameterForm");
        foreach (string legacy in new[] { "true", "false" })
        {
            var stored = new Dictionary<string, string> { ["RunAsStandardUser"] = legacy };
            string value = (string)Call(form, "ResolveInitialValue", null, modeField, stored)!;
            Check(value == modeField.FallbackValueMap![legacy], "legacy mode backfill " + legacy);
            Check(stored.Count == 1, "backfill never changes saved config " + legacy);
        }
        var explicitMode = new Dictionary<string, string> { [modeField.Key] = "administrator", ["RunAsStandardUser"] = "true" };
        Check((string)Call(form, "ResolveInitialValue", null, modeField, explicitMode)! == "administrator", "explicit mode takes precedence");

        object?[] bindingArgs = { "Launch", null };
        Check((bool)Call(host, "TryResolveClaimedType", null, bindingArgs)!, "Launch type routed to official plugin");
        string marker = Path.Combine(output, "launch.marker");
        var action = new ActionItem { Type = "Launch", Parameter = Environment.ProcessPath!, Arguments = "--write-marker \"" + marker + "\"" };
        object outcome = Call(host, "ExecuteClaimedAction", null, action, bindingArgs[1])!;
        Check((bool)outcome.GetType().GetProperty("Success", Any)!.GetValue(outcome)!, "real plugin dispatch starts process");
        var watch = Stopwatch.StartNew();
        while (!File.Exists(marker) && watch.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(50);
        Check(File.Exists(marker) && File.ReadAllText(marker) == "started", "child process received arguments and wrote marker");
        action.Parameter = Path.Combine(output, "does-not-exist.exe");
        outcome = Call(host, "ExecuteClaimedAction", null, action, bindingArgs[1])!;
        Check(!string.IsNullOrEmpty((string)outcome.GetType().GetProperty("Message", Any)!.GetValue(outcome)!), "missing target reports launch failure without quarantining plugin");

        Type compatibility = HostType("Plugins.PluginContractCompatibility");
        string incompatibleDll = Path.Combine(source, "starpie.plugin.floatingball", "StarPie.Plugin.FloatingBall.dll");
        if (File.Exists(incompatibleDll))
        {
            Assembly incompatible = AssemblyLoadContext.Default.LoadFromAssemblyPath(incompatibleDll);
            object?[] compatibilityArgs = { incompatible, incompatibleDll, "" };
            Check(!(bool)Call(compatibility, "Check", null, compatibilityArgs)!, "missing SDK caught before Initialize");
            Check(compatibilityArgs[2]!.ToString()!.Contains("SDK") && !compatibilityArgs[2]!.ToString()!.Contains("missing method"), "incompatibility gives actionable message");
        }
        Call(host, "ShutdownAll", null);
    }
}
