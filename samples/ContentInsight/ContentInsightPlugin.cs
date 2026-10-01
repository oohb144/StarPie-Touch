namespace StarPie.Plugin.ContentInsight;

public sealed class ContentInsightPlugin : IStarPiePlugin
{
    private readonly List<IDisposable> _tokens = new();

    public void Initialize(IPluginContext context)
    {
        if (context is not IContentInsightContext extended)
            throw new PluginContractException("Content Insight requires SDK 1.5 host services.");
        context.I18n.Register("selection", "智识", "Content Insight");
        context.I18n.Register("screen", "框选识屏", "Screen Insight");
        context.I18n.Register("clipboard", "识别剪贴板", "Clipboard Insight");
        context.I18n.Register("search", "搜索网址模板（用 {query} 表示关键词）", "Search URL template (use {query})");
        context.I18n.Register("direct", "选区中的完整网址直接打开", "Open a complete selected URL immediately");
        context.I18n.Register("fallback", "允许模拟复制获取选区（默认关闭）", "Allow Ctrl+C fallback (off by default)");
        string icon = context.Icons.RegisterSvg("insight", "M4 4H10V6H6V10H4Z M14 4H20V10H18V6H14Z M4 14H6V18H10V20H4Z M18 14H20V20H14V18H18Z M9 9H15V15H9Z");
        foreach (var (id, source) in new[] { ("selection", InsightSource.Selection), ("screen", InsightSource.Screen), ("clipboard", InsightSource.Clipboard) })
            _tokens.Add(context.Actions.Register(new Contribution(context, extended.ContentInsight, id, source, icon)));
    }

    public void Shutdown()
    {
        foreach (var token in _tokens) token.Dispose();
        _tokens.Clear();
    }

    private sealed class Contribution(IPluginContext context, IHostContentInsightService service,
        string id, InsightSource source, string icon) : IActionContribution
    {
        public ActionDescriptor Descriptor => new()
        {
            Id = id, DisplayName = id switch { "screen" => "框选识屏", "clipboard" => "识别剪贴板", _ => "智识" },
            DisplayNameKey = id, Category = "Content Insight", IconKey = icon,
            Kind = ActionKind.Background, TimeoutSeconds = 300
        };
        public IReadOnlyList<ParameterField> Parameters => new[]
        {
            new ParameterField { Key = "searchUrl", LabelKey = "search", Type = ParameterFieldType.Text, DefaultValue = "https://www.bing.com/search?q={query}", Required = true, MaxLength = 2048 },
            new ParameterField { Key = "directUrl", LabelKey = "direct", Type = ParameterFieldType.Bool, DefaultValue = "true" },
            new ParameterField { Key = "copyFallback", LabelKey = "fallback", Type = ParameterFieldType.Bool, DefaultValue = "false" }
        };
        public string? Validate(IReadOnlyDictionary<string, string> parameters)
        {
            string template = parameters.GetValueOrDefault("searchUrl") ?? "https://www.bing.com/search?q={query}";
            return InsightRules.IsSearchTemplate(template) ? null : "Search URL must be http(s) and contain exactly one {query}.";
        }
        public string Preview(IReadOnlyDictionary<string, string> parameters) => Descriptor.DisplayName;
        public async Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
        {
            await service.RunAsync(new InsightSessionRequest
            {
                Source = source, Context = input.Context,
                SearchUrlTemplate = input.Parameter("searchUrl") ?? "https://www.bing.com/search?q={query}",
                OpenSelectedUrlImmediately = input.Bool("directUrl", true),
                AllowCopyFallback = input.Bool("copyFallback")
            }, text => InsightRules.Classify(text, context.Info.LanguageCode), cancellationToken);
            return ActionResult.Empty;
        }
    }
}
