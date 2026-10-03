using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class Program
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Assembly Host = null!;
    private static int Checks;
    private static readonly List<string> Failures = new();
    private static readonly List<object> Installed = new();
    private static Type Type(string name) => Host.GetType("WinPieGestures.Plugins." + name, true)!;
    private static object? Call(string type, string name, params object?[] args) =>
        Type(type).GetMethod(name, Any)!.Invoke(null, args);
    private static object? Property(object target, string name) => target.GetType().GetProperty(name, Any)!.GetValue(target);
    private static void Check(bool ok, string name) { Checks++; if (!ok) Failures.Add(name); }
    private static object Await(object task)
    {
        ((Task)task).GetAwaiter().GetResult();
        return Property(task, "Result")!;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new InvalidOperationException("Use a new output directory; no cleanup is performed.");
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(output, "localappdata"));
        Environment.SetEnvironmentVariable("TEMP", Path.Combine(output, "downloads"));
        Environment.SetEnvironmentVariable("TMP", Path.Combine(output, "downloads"));
        Directory.CreateDirectory(Path.Combine(output, "downloads"));
        Host = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
        try
        {
            if (args.Contains("--baseline"))
            {
                string json = File.ReadAllText(args[2]);
                object catalog = JsonSerializer.Deserialize(json, Type("OfficialPluginCatalog"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                try { Call("OfficialPluginClient", "ValidateCatalog", catalog); Check(false, "old release should reproduce schema 2 rejection"); }
                catch (TargetInvocationException ex) { Check(ex.InnerException is InvalidDataException, "old release rejects the real schema 2 catalog: " + ex.InnerException?.Message); }
            }
            else
            {
                TestCatalogs();
                if (args.Contains("--reload"))
                {
                    string source = Path.GetFullPath(args[Array.IndexOf(args, "--reload") + 1]);
                    foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                    {
                        string target = Path.Combine(output, "plugins", Path.GetRelativePath(source, file));
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Copy(file, target);
                    }
                }
                Call("PluginPaths", "OverrideRootsForTesting", Path.Combine(output, "plugins"), Path.Combine(output, "candidates"));
                Type("PluginHost").GetProperty("HeadlessMode", Any)!.SetValue(null, true);
                Call("PluginHost", "Initialize");
                if (args.Contains("--reload")) TestOfflineReload(output);
                else Check((int)Type("PluginHost").GetProperty("InstalledCount", Any)!.GetValue(null)! == 0, "fresh install begins with no official software or plugins");
                Check(!Directory.Exists(Path.Combine(output, "candidates")), "startup leaves read-only plugin source absent");
                if (args.Contains("--online")) TestOnline(output);
                Call("PluginHost", "ShutdownAll");
            }
        }
        catch (Exception ex) { Failures.Add(ex.ToString()); }
        var report = new { checks = Checks, failures = Failures, installed = Installed, result = Failures.Count == 0 ? "PASS" : "FAIL",
            limitations = "Headless fresh-install download/installation/initialization only. No live touchscreen, plugin action side effects or GUI test. All sandboxes retained." };
        string reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(output, "report.json"), reportJson);
        Console.WriteLine(reportJson);
        return Failures.Count == 0 ? 0 : 1;
    }

    private static JsonObject Asset(string version, string api = "1.4") => new()
    {
        ["name"] = "Official test", ["version"] = version, ["releaseTag"] = "test",
        ["assetName"] = "test.spkg", ["packageUrl"] = "https://github.com/Star-Pie/StarPie-Official-Plugins/releases/download/test/test.spkg",
        ["sha256"] = new string('a', 64), ["size"] = 10, ["apiVersion"] = api,
        ["minHostVersion"] = "1.8.0-beta.1", ["targetFramework"] = "net8.0-windows",
        ["typeClaims"] = new JsonArray("Launch"), ["capabilities"] = new JsonArray("Process")
    };

    private static JsonObject Catalog(int schema, params JsonObject[] modules) => new()
    {
        ["schemaVersion"] = schema, ["releaseTag"] = "test", ["catalogVersion"] = "test",
        ["modules"] = new JsonArray(modules.Select(module => (JsonNode)module).ToArray())
    };
    private static object Parse(JsonObject root) => Call("OfficialPluginClient", "ParseCatalog", Encoding.UTF8.GetBytes(root.ToJsonString()), "test")!;
    private static object[] Modules(object catalog) => ((System.Collections.IEnumerable)Property(catalog, "Modules")!).Cast<object>().ToArray();
    private static void Reject(JsonObject root, string name)
    {
        try { Parse(root); Check(false, name); }
        catch (TargetInvocationException ex) { Check(ex.InnerException is InvalidDataException, name); }
    }

    private static void TestCatalogs()
    {
        JsonObject flat = Asset("1.0.1"); flat["id"] = "starpie.test";
        object first = Modules(Parse(Catalog(1, flat)))[0];
        Check((string)Property(first, "Version")! == "1.0.1", "schema 1 still supported");
        JsonObject newest = Asset("1.1.0", "1.8"), old = Asset("1.0.9"), middle = Asset("1.0.10");
        JsonObject group = new() { ["id"] = "starpie.test", ["versions"] = new JsonArray(newest, old, middle) };
        JsonObject nested = Catalog(2, group);
        object selected = Modules(Parse(nested))[0];
        Check((string)Property(selected, "Id")! == "starpie.test", "schema 2 inherits module ID");
        Check((string)Property(selected, "Version")! == "1.0.10", "latest compatible version chosen numerically, not list order");
        Check((string)Property(selected, "CompatibilityError")! == "", "selected historical asset compatible");
        object currentCard = Activator.CreateInstance(Type("OfficialPluginListItem"), Any, null, new[] { selected, "1.1.0" }, null)!;
        Check(!(bool)Property(currentCard, "CanInstall")!, "installed newer version never offered a downgrade");
        var language = Host.GetType("WinPieGestures.I18n", true)!.GetMethod("SetLanguage", Any)!;
        foreach (string code in new[] { "zh-CN", "zh-TW", "en", "ja" })
        {
            language.Invoke(null, new object[] { code });
            JsonObject incompatible = Asset("1.1.0", "1.8"); incompatible["id"] = "starpie.test";
            object item = Modules(Parse(Catalog(1, incompatible)))[0];
            string message = (string)Property(item, "CompatibilityError")!;
            Check(message.Contains("1.8") && message.Contains("1.5") && !message.Contains("PluginsOfficial"), "SDK requirement translated " + code);
        }
        language.Invoke(null, new object[] { "zh-CN" });

        foreach (string field in new[] { "apiVersion", "minHostVersion", "maxHostVersion", "targetFramework" })
        {
            JsonObject unsupported = Asset("2.0.0");
            unsupported[field] = field switch { "apiVersion" => "1.8", "minHostVersion" => "9.0.0", "maxHostVersion" => "1.0.0", _ => "net9.0-windows" };
            JsonObject blocked = new() { ["id"] = "starpie.blocked", ["versions"] = new JsonArray(unsupported) };
            object blockedModule = Modules(Parse(Catalog(2, blocked)))[0];
            Check(!string.IsNullOrEmpty((string)Property(blockedModule, "CompatibilityError")!), "compatibility reason: " + field);
            object card = Activator.CreateInstance(Type("OfficialPluginListItem"), Any, null, new object?[] { blockedModule, null }, null)!;
            Check(!(bool)Property(card, "CanInstall")!, "incompatible install disabled: " + field);
        }
        JsonObject invalid = (JsonObject)nested.DeepClone();
        invalid["modules"]![0]!["versions"]![0]!["packageUrl"] = "https://example.com/test.spkg";
        Reject(invalid, "invalid unused version cannot hide behind compatible fallback");
        invalid = (JsonObject)nested.DeepClone(); invalid["modules"]![0]!["versions"]![0]!["sha256"] = "bad";
        Reject(invalid, "invalid package hash rejected");
        invalid = (JsonObject)nested.DeepClone(); invalid["modules"]![0]!["versions"]![0]!["id"] = "starpie.other";
        Reject(invalid, "mismatched inner module ID rejected");
        invalid = (JsonObject)nested.DeepClone(); invalid["modules"]![0]!["versions"] = new JsonArray();
        Reject(invalid, "empty schema 2 version list rejected");
        invalid = (JsonObject)nested.DeepClone(); invalid["schemaVersion"] = 3;
        Reject(invalid, "unknown schema rejected");
        invalid = (JsonObject)nested.DeepClone(); invalid["releaseTag"] = "other";
        Reject(invalid, "catalog/release mismatch rejected");
        invalid = (JsonObject)nested.DeepClone(); invalid["modules"]!.AsArray().Add(invalid["modules"]![0]!.DeepClone());
        Reject(invalid, "duplicate module group rejected");
    }

    private static void TestOfflineReload(string output)
    {
        object[] instances = ((System.Collections.IEnumerable)Call("PluginHost", "ListInstances")!).Cast<object>().ToArray();
        Check(instances.Length >= 12, "offline restart discovers installed default action modules");
        foreach (object instance in instances)
        {
            object?[] loadArgs = { "" };
            bool loaded = (bool)instance.GetType().GetMethod("Load", Any)!.Invoke(instance, loadArgs)!;
            Check(loaded, "offline restart loads " + Property(instance, "PluginId") + ": " + loadArgs[0]);
        }
        Check(!Directory.EnumerateFileSystemEntries(Path.Combine(output, "downloads")).Any(), "offline restart never downloads or reinstalls official modules");
    }

    private static void TestOnline(string output)
    {
        object catalog = Await(Call("OfficialPluginClient", "FetchCatalogAsync", CancellationToken.None)!);
        object[] modules = Modules(catalog);
        Check(modules.Length >= 12, "live official catalog contains default action modules");
        File.WriteAllText(Path.Combine(output, "catalog.json"), JsonSerializer.Serialize(catalog, catalog.GetType(), new JsonSerializerOptions { WriteIndented = true }));
        foreach (object module in modules)
        {
            string id = (string)Property(module, "Id")!;
            string version = (string)Property(module, "Version")!;
            string incompatibility = (string)Property(module, "CompatibilityError")!;
            if (!string.IsNullOrEmpty(incompatibility)) { Installed.Add(new { id, version, status = "unsupported", reason = incompatibility }); continue; }
            object result = Await(Call("OfficialPluginClient", "InstallAsync", module, CancellationToken.None)!);
            bool success = (bool)Property(result, "Success")!, enabled = (bool)Property(result, "Enabled")!;
            Check(success, "fresh download and hash-verified installation " + id + ": " + Property(result, "Error"));
            Check(enabled, "fresh official plugin initialized and enabled " + id);
            object? instance = Call("PluginHost", "Find", id);
            Check(instance != null && (bool)Property(instance, "IsLoaded")!, "actual plugin loaded from fresh sandbox " + id);
            Check(File.Exists(Path.Combine(output, "plugins", id, "plugin.json")), "manifest installed in writable host root " + id);
            Installed.Add(new { id, version, success, enabled });
        }
        foreach (string claim in new[] { "Launch", "Folder", "WebUrl", "Command", "Ocr", "System", "ShellTool", "Tile", "TileRestore", "SwitchWindow", "MoveMonitor", "ToggleTopmost", "WindowOpacity" })
        {
            object?[] binding = { claim, null };
            Check((bool)Call("PluginHost", "TryResolveClaimedType", binding)!, "fresh registry routes " + claim);
        }
        object launch = modules.Single(module => (string)Property(module, "Id")! == "starpie.builtin.launch");
        object launchInstance = Call("PluginHost", "Find", "starpie.builtin.launch")!;
        object entry = Property(launchInstance, "Entry")!;
        var entryVersion = entry.GetType().GetProperty("Version", Any)!;
        string savedVersion = (string)entryVersion.GetValue(entry)!;
        try
        {
            entryVersion.SetValue(entry, "99.0.0");
            object result = Await(Call("OfficialPluginClient", "InstallAsync", launch, CancellationToken.None)!);
            Check(!(bool)Property(result, "Success")!, "installer refuses downgrade before downloading");
            Check((string)entryVersion.GetValue(entry)! == "99.0.0", "downgrade refusal preserves installed entry");
        }
        finally { entryVersion.SetValue(entry, savedVersion); }
    }
}
