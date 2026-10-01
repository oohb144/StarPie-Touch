using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StarPie.Plugin;
using StarPie.Plugin.ContentInsight;
using WinPieGestures;

internal static class Program
{
    private static int _checks;
    private static readonly List<string> Failures = new();
    private static readonly Assembly Host = typeof(App).Assembly;
    private static readonly BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static string _output = "";
    private static void Check(bool result, string name) { _checks++; if (!result) Failures.Add(name); }
    private static Type Type(string name) => Host.GetType(name, true)!;
    private static object? Call(Type type, string name, object? target, params object?[] arguments) => type.GetMethod(name, Any)!.Invoke(target, arguments);

    [STAThread]
    private static int Main(string[] args)
    {
        _output = Path.GetFullPath(args.Length > 0 ? args[0] : "scratch/insight-test-results/" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_output);
        // Set before touching host static state: all config/log/plugin IO stays in this new sandbox.
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(_output, "localappdata"));
        try
        {
            TestRules();
            TestContext();
            var app = new App(); app.InitializeComponent();
            TestCard();
            TestPlugin();
        }
        catch (Exception ex) { Failures.Add(ex.ToString()); }
        var report = new { checks = _checks, failures = Failures, result = Failures.Count == 0 ? "PASS" : "FAIL", output = _output,
            limitations = new[] { "No live touch/UIA/clipboard injection/OCR provider test", "Offscreen rendering only; no GUI shown", "Sandbox retained; no bulk deletion" } };
        File.WriteAllText(Path.Combine(_output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
        return Failures.Count == 0 ? 0 : 1;
    }

    private static void TestRules()
    {
        foreach (var (input, expected) in new[] { ("200×1.13", 226m), ("(1+2)*3", 9m), ("1024/8*2", 256m),
            ("-2*-3", 6m), ("0.5+0.25", .75m), (" 2 + 3 * 4 ", 14m), ("10÷4", 2.5m), ("1--2", 3m), ("2+3= ", 5m) })
            Check(Arithmetic.TryCalculate(input, out var value) && value == expected, "calculate " + input);
        foreach (string input in new[] { "123456", "1/0", "(1+2", "1+2)", "1..2+3", "1+foo", "1;Process.Start()", "Math.Sin(1)",
            new string('(', 40) + "1+2" + new string(')', 40), new string('1', 1100) + "+2", "79228162514264337593543950335+1" })
            Check(!Arithmetic.TryCalculate(input, out _), "reject expression " + input[..Math.Min(input.Length, 30)]);
        foreach (string input in new[] { "https://example.com/a?q=中文", "example.com/path", "https://例子.中国", "http://127.0.0.1:8080/a" })
            Check(InsightRules.Classify(input).Candidates[0].Operation == InsightOperation.OpenUrl, "recognize URL " + input);
        foreach (string input in new[] { "0.5", "1.2+3.4", "hello world", "https://user:password@example.com", "file:///C:/a.txt", "javascript:alert(1)", "calc", "C:\\a.txt" })
            Check(!InsightRules.TryUrl(input, out _), "reject URL " + input);
        Check(InsightRules.Classify("1.2+3.4").Result == "4.6", "arithmetic wins over numeric URL");
        Check(InsightRules.Classify("123456").Candidates[0].Operation == InsightOperation.Search, "ambiguous digits search");
        foreach (string input in new[] { "C:\\文件夹\\a.txt", "\"D:\\file name.txt\"", "\\\\server\\share\\a.txt" })
            Check(InsightRules.Classify(input).Candidates[0].Operation == InsightOperation.OpenPath, "path " + input);
        var multi = InsightRules.Classify("资料 https://example.com/a。\n另见 https://example.org/b");
        Check(multi.Candidates.Count(c => c.Operation == InsightOperation.OpenUrl) == 2, "multiple explicit links");
        Check(InsightRules.IsSearchTemplate("https://www.bing.com/search?q={query}"), "search template accepted");
        foreach (string input in new[] { "https://example.com", "https://example.com/{query}/{query}", "file:///C:/{query}", "https://user:pass@example.com/{query}" })
            Check(!InsightRules.IsSearchTemplate(input), "reject search template " + input);
        CultureInfo old = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        Check(InsightRules.Classify("0.5+0.25").Result == "0.75", "invariant decimal under de-DE");
        CultureInfo.CurrentCulture = old;
        try { InsightRules.Classify(new string('a', 8193)); Check(false, "input limit"); } catch (ArgumentException) { Check(true, "input limit"); }
        foreach (string language in new[] { "zh-CN", "zh-TW", "en", "ja" })
            Check(InsightRules.Classify("1+2", language).Candidates.All(c => c.Label.Length > 0), "localized candidates " + language);
    }

    private static void TestContext()
    {
        var action = new ActionItem { Type = "Plugin", Parameter = "x" };
        var origin = new ActionContext { ForegroundWindowHandle = 123, CursorX = 320, CursorY = 210 };
        typeof(ActionItem).GetProperty("InvocationContext", Any)!.SetValue(action, origin);
        Check(ReferenceEquals(typeof(ActionItem).GetProperty("InvocationContext", Any)!.GetValue(action.Clone()), origin), "clone keeps origin snapshot");
        string json = JsonSerializer.Serialize(action);
        Check(!json.Contains("InvocationContext") && !json.Contains("ForegroundWindowHandle"), "origin excluded from persisted config");
        Check(!typeof(IPluginContext).GetProperties().Any(p => p.Name == "ContentInsight"), "legacy context interface unchanged");
        Check(PluginApi.ApiVersion == $"{PluginApi.ApiVersionMajor}.{PluginApi.ApiVersionMinor}", "SDK version consistent");
        var source = new System.Windows.DataObject();
        source.SetData("UnicodeText", "old text", false);
        source.SetData("HTML Format", "<b>old text</b>", false);
        var snapshot = ((bool Success, System.Windows.IDataObject? Data))Call(Type("WinPieGestures.Insight.InsightContentReader"), "SnapshotClipboard", null, source)!;
        Check(snapshot.Success && (string)snapshot.Data!.GetData("HTML Format", false) == "<b>old text</b>", "clipboard materializes rich data");
        source.SetData("UnicodeText", "changed", false);
        Check((string)snapshot.Data!.GetData("UnicodeText", false) == "old text", "clipboard snapshot stays independent");
        var unsupported = new System.Windows.DataObject(); unsupported.SetData("Custom", new object(), false);
        var rejected = ((bool Success, System.Windows.IDataObject? Data))Call(Type("WinPieGestures.Insight.InsightContentReader"), "SnapshotClipboard", null, unsupported)!;
        Check(!rejected.Success, "unsupported clipboard skips copy fallback");
    }

    private static void TestCard()
    {
        var serviceType = Type("WinPieGestures.Plugins.PluginContentInsightService");
        object service = Activator.CreateInstance(serviceType, Any, null,
            new object[] { "test.insight", PluginCapability.Ui | PluginCapability.Clipboard | PluginCapability.ScreenCapture | PluginCapability.Process | PluginCapability.FileSystem }, null)!;
        var request = new InsightSessionRequest { Source = InsightSource.Input };
        foreach (var language in Enum.GetValues<LanguageCode>())
        {
            I18n.CurrentLanguage = language;
            var texts = (IEnumerable<KeyValuePair<string, LocalizedString>>)Type("WinPieGestures.Insight.InsightText").GetProperty("All", Any)!.GetValue(null)!;
            foreach (var entry in texts)
            {
                string value = entry.Value.Get(language);
                Check(!string.IsNullOrWhiteSpace(value) && entry.Value.ZhTw != null && entry.Value.En != null && entry.Value.Ja != null,
                    "translated host text " + language + ":" + entry.Key);
                if (language == LanguageCode.En)
                    Check(!System.Text.RegularExpressions.Regex.IsMatch(value, @"[\u3040-\u30ff\u3400-\u9fff]"), "English text " + entry.Key);
            }
            var card = (Window)Activator.CreateInstance(typeof(InsightWindow), Any, null,
                new object[] { service, request, (Func<string, InsightAnalysis>)(text => InsightRules.Classify(text, I18n.CurrentLanguageCode)), CancellationToken.None }, null)!;
            Call(typeof(InsightWindow), "SetContent", card, "200×1.13", InsightSource.Input, "");
            Check(((TextBlock)card.FindName("ResultText")).Text == "226", "card result " + language);
            Check(((WrapPanel)card.FindName("OperationsPanel")).Children.Count == 3, "card operations " + language);
            Check(((Button)card.FindName("CaptureButton")).MinHeight >= 48, "touch target " + language);
            var editor = (TextBox)card.FindName("ContentEditor");
            editor.Text = "2+3";
            Check(((WrapPanel)card.FindName("OperationsPanel")).Children.Count == 0, "editing clears stale actions " + language);
            I18n.CurrentLanguage = language == LanguageCode.En ? LanguageCode.ZhCn : LanguageCode.En;
            I18n.CurrentLanguage = language;
            Check(((WrapPanel)card.FindName("OperationsPanel")).Children.Count == 0, "language switch cannot revive stale actions " + language);
            Call(typeof(InsightWindow), "Analyze_Click", card, editor, new RoutedEventArgs());
            Check(((TextBlock)card.FindName("ResultText")).Text == "5", "edited content reanalysis " + language);
            Call(typeof(InsightWindow), "SetContent", card, "200×1.13", InsightSource.Screen,
                (string)Call(Type("WinPieGestures.Insight.InsightText"), "T", null, "ReviewOcr")!);
            foreach (string theme in new[] { "Light", "Dark" })
            {
                AppThemeManager.ApplyTheme(card, theme);
                RenderCard(card, $"card-{language}-{theme}.png");
            }
            card.Close();
        }
        Check(!(bool)typeof(InsightWindow).GetProperty("IsSessionOpen", Any)!.GetValue(null)!, "card session count restored after close");
        I18n.CurrentLanguage = LanguageCode.ZhCn;
        var deniedService = (IHostContentInsightService)Activator.CreateInstance(serviceType, Any, null,
            new object[] { "test.denied", PluginCapability.Process }, null)!;
        try { deniedService.RunAsync(request, _ => new(), CancellationToken.None).GetAwaiter().GetResult(); Check(false, "UI capability gate"); }
        catch (PluginCapabilityDeniedException ex) { Check(ex.Capability == PluginCapability.Ui, "UI capability gate"); }
        // Cross-capability assertion: Ui does not imply ScreenCapture or Clipboard.
        var uiOnly = (IHostContentInsightService)Activator.CreateInstance(serviceType, Any, null,
            new object[] { "test.ui", PluginCapability.Ui }, null)!;
        foreach (var (source, cap) in new[] { (InsightSource.Screen, PluginCapability.ScreenCapture), (InsightSource.Clipboard, PluginCapability.Clipboard) })
        {
            try { uiOnly.RunAsync(new() { Source = source }, _ => new(), CancellationToken.None).GetAwaiter().GetResult(); Check(false, "source gate " + source); }
            catch (PluginCapabilityDeniedException ex) { Check(ex.Capability == cap, "source gate " + source); }
        }
        foreach (var (operation, cap) in new[] { (InsightOperation.Copy, PluginCapability.Clipboard), (InsightOperation.OpenUrl, PluginCapability.Process), (InsightOperation.OpenPath, PluginCapability.FileSystem) })
        {
            object gated = operation == InsightOperation.OpenPath ? deniedService : uiOnly;
            try
            {
                Call(serviceType, "Execute", gated, new InsightCandidate { Operation = operation, Value = "" }, request.SearchUrlTemplate, CancellationToken.None);
                Check(false, "operation gate " + operation);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is PluginCapabilityDeniedException denied)
            { Check(denied.Capability == cap, "operation gate " + operation); }
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        ((IHostContentInsightService)service).RunAsync(request, _ => new(), cancelled.Token).GetAwaiter().GetResult();
        Check(!(bool)typeof(InsightWindow).GetProperty("IsSessionOpen", Any)!.GetValue(null)!, "cancelled invocation creates no card");
    }

    private static void RenderCard(Window card, string filename)
    {
        var content = (FrameworkElement)card.Content;
        content.Measure(new Size(460, 680));
        content.Arrange(new Rect(new Point(), content.DesiredSize)); content.UpdateLayout();
        int width = (int)Math.Ceiling(content.ActualWidth), height = (int)Math.Ceiling(content.ActualHeight);
        Check(width > 0 && height > 0, "render size " + filename);
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(card.Background, null, new Rect(0, 0, width, height));
        image.Render(background);
        image.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(_output, filename)); encoder.Save(stream);
    }

    private static void TestPlugin()
    {
        Type paths = Type("WinPieGestures.Plugins.PluginPaths");
        Call(paths, "OverrideRootsForTesting", null, Path.Combine(_output, "plugin-data"), Path.Combine(_output, "absent-scan-root"));
        Type host = Type("WinPieGestures.Plugins.PluginHost");
        host.GetProperty("HeadlessMode", Any)!.SetValue(null, true);
        Call(host, "Initialize", null);
        string plugin = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "ContentInsight", "release", "StarPie.Plugin.ContentInsight.dll"));
        object scan = Call(host, "PrepareInstall", null, plugin)!;
        Check((bool)scan.GetType().GetProperty("Accepted")!.GetValue(scan)!, "static plugin scan");
        object options = Activator.CreateInstance(Type("WinPieGestures.Plugins.PluginInstallOptions"))!;
        options.GetType().GetProperty("Acknowledged")!.SetValue(options, true);
        options.GetType().GetProperty("EnableAfterInstall")!.SetValue(options, true);
        var installed = Call(host, "CommitInstall", null, scan, options)!;
        Check((bool)installed.GetType().GetProperty("Success")!.GetValue(installed)!, "sandbox plugin install");
        Check((bool)installed.GetType().GetProperty("Enabled")!.GetValue(installed)!, "sandbox plugin enable");
        object catalog = host.GetField("Catalog", Any)!.GetValue(null)!;
        InspectPluginActions(catalog);
        var stopReason = Enum.Parse(Type("WinPieGestures.Plugins.PluginStopReason"), "UserDisabled");
        var stopping = (Task)Call(host, "DisableAsync", null, "community.contentinsight", stopReason, null, CancellationToken.None)!;
        stopping.GetAwaiter().GetResult();
        var stopResult = stopping.GetType().GetProperty("Result")!.GetValue(stopping)!;
        Check((bool)stopResult.GetType().GetProperty("IsFullyStopped")!.GetValue(stopResult)!, "plugin completely stopped");
        int after = 0; foreach (var _ in (System.Collections.IEnumerable)Call(catalog.GetType(), "SnapshotActions", catalog)!) after++;
        Check(after == 0, "plugin registrations revoked");
        Check(!Directory.Exists(Path.Combine(_output, "absent-scan-root")), "candidate scan root stays read-only");
        // Deliberately no uninstall/selftest cleanup: user prohibits bulk deletion.
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void InspectPluginActions(object catalog)
    {
        var registrations = (System.Collections.IEnumerable)Call(catalog.GetType(), "SnapshotActions", catalog)!;
        int count = 0;
        foreach (object registration in registrations)
        {
            count++;
            var contribution = (IActionContribution)registration.GetType().GetProperty("Contribution")!.GetValue(registration)!;
            Check(contribution.Descriptor.Kind == ActionKind.Background, "background action " + contribution.Descriptor.Id);
            Check(contribution.Validate(new Dictionary<string, string>()) == null, "defaults valid " + contribution.Descriptor.Id);
            Check(contribution.Validate(new Dictionary<string, string> { ["searchUrl"] = "javascript:{query}" }) != null, "invalid URL rejected " + contribution.Descriptor.Id);
        }
        Check(count == 3, "three registered entry points");
    }
}
