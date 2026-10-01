namespace StarPie.Plugin;

/// <summary>Optional SDK 1.5 extension. Existing IPluginContext implementations remain compatible.</summary>
public interface IContentInsightContext
{
    IHostContentInsightService ContentInsight { get; }
}

public enum InsightSource { Selection, Clipboard, Screen, Input }
public enum InsightOperation { Copy, OpenUrl, OpenPath, LocatePath, Search }

/// <summary>Host-rendered operation; plugins provide data, never XAML or executable callbacks.</summary>
public sealed class InsightCandidate
{
    public string Label { get; init; } = "";
    public InsightOperation Operation { get; init; }
    public string Value { get; init; } = "";
}

public sealed class InsightAnalysis
{
    public string Title { get; init; } = "";
    public string Result { get; init; } = "";
    public IReadOnlyList<InsightCandidate> Candidates { get; init; } = Array.Empty<InsightCandidate>();
}

public sealed class InsightSessionRequest
{
    public InsightSource Source { get; init; }
    public ActionContext Context { get; init; } = new();
    public string SearchUrlTemplate { get; init; } = "https://www.bing.com/search?q={query}";
    public bool OpenSelectedUrlImmediately { get; init; } = true;
    /// <summary>Opt-in Ctrl+C fallback; requires Clipboard and InputSimulation capabilities.</summary>
    public bool AllowCopyFallback { get; init; }
}

/// <summary>
/// Host owns acquisition and UI. Await until the card closes, so the plugin invocation lease
/// remains alive while the classifier is referenced. Cancellation closes the card and capture.
/// Ui is required; clipboard, screenshot, copy fallback and open operations check their own capabilities.
/// Classifiers must be quick pure calculations; no IO. Maximum input is 8192 characters.
/// </summary>
public interface IHostContentInsightService
{
    Task RunAsync(InsightSessionRequest request, Func<string, InsightAnalysis> classify,
        CancellationToken cancellationToken);
}
