using System.Globalization;
using System.Text.RegularExpressions;

namespace StarPie.Plugin.ContentInsight;

/// <summary>Bounded pure recognition; no script evaluation, filesystem or network IO.</summary>
public static class InsightRules
{
    private static readonly Regex Links = new(@"https?://[^\s<>""\u3000]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex PathPattern = new(@"^(?:[A-Za-z]:[\\/]|\\\\[^\\/\s]+[\\/][^\\/\s]+)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex BareDomain = new(@"^(?:[\p{L}\d](?:[\p{L}\d-]*[\p{L}\d])?\.)+[\p{L}]{2,}(?::\d{1,5})?(?:[/?#].*)?$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    public static bool IsSearchTemplate(string template) => template.Length <= 2048 &&
        template.Split("{query}", StringSplitOptions.None).Length == 2 &&
        TryUrl(template.Replace("{query}", "test", StringComparison.Ordinal), out _);

    public static bool TryUrl(string text, out string url)
    {
        url = "";
        if (text.Any(char.IsWhiteSpace) || text.Length > 2048) return false;
        string candidate = text;
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            if (!BareDomain.IsMatch(candidate) || candidate.Contains('\\') || candidate.Contains('@')) return false;
            candidate = "https://" + candidate;
        }
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || string.IsNullOrEmpty(uri.Host) ||
            uri.UserInfo.Length != 0 || uri.HostNameType == UriHostNameType.Unknown) return false;
        url = uri.AbsoluteUri;
        return true;
    }

    public static InsightAnalysis Classify(string text, string language = "zh-CN")
    {
        text = text.Trim();
        if (text.Length > 8192) throw new ArgumentException("Content exceeds 8192 characters.");
        string T(string cn, string en, string tw, string ja) => language switch { "en" or "en-US" => en, "zh-TW" => tw, "ja" or "ja-JP" => ja, _ => cn };
        var candidates = new List<InsightCandidate>();
        string title = T("文字", "Text", "文字", "テキスト");
        string result = "";
        void Add(string label, InsightOperation operation, string value) => candidates.Add(new() { Label = label, Operation = operation, Value = value });
        if (text.Length == 0) return new() { Title = title };

        if (TryUrl(text, out string url))
        {
            title = T("网址", "Web address", "網址", "URL");
            Add(T("打开网页", "Open website", "開啟網頁", "開く"), InsightOperation.OpenUrl, url);
        }
        else if (PathPattern.IsMatch(text.Trim('"')) && !text.Contains('\n') && !text.Contains('\r'))
        {
            title = T("文件路径", "File path", "檔案路徑", "ファイルパス");
            string path = text.Trim('"');
            Add(T("打开", "Open", "開啟", "開く"), InsightOperation.OpenPath, path);
            Add(T("定位", "Show in folder", "定位", "フォルダーで表示"), InsightOperation.LocatePath, path);
        }
        else if (Arithmetic.TryCalculate(text, out decimal value))
        {
            title = T("计算结果", "Calculation", "計算結果", "計算結果");
            result = value.ToString("G29", CultureInfo.InvariantCulture);
            Add(T("复制结果", "Copy result", "複製結果", "結果をコピー"), InsightOperation.Copy, result);
        }
        else
        {
            foreach (Match match in Links.Matches(text))
            {
                if (candidates.Count >= 4) break;
                string link = match.Value.TrimEnd('.', ',', ';', ')', ']', '。', '，', '；', '）');
                if (TryUrl(link, out string extracted) && candidates.All(c => c.Value != extracted))
                    Add(T("打开链接", "Open link", "開啟連結", "リンクを開く") + $" {candidates.Count + 1}", InsightOperation.OpenUrl, extracted);
            }
        }
        Add(T("搜索", "Search", "搜尋", "検索"), InsightOperation.Search, text);
        Add(T("复制原文", "Copy text", "複製原文", "テキストをコピー"), InsightOperation.Copy, text);
        return new() { Title = title, Result = result, Candidates = candidates };
    }
}

/// <summary>Recursive descent decimal parser with depth, input and overflow limits.</summary>
public static class Arithmetic
{
    public static bool TryCalculate(string input, out decimal value)
    {
        value = 0;
        if (input.Length > 1024 || !input.Any(c => "+-*/×÷()".Contains(c))) return false;
        try
        {
            var parser = new Parser(input.Replace('×', '*').Replace('÷', '/').Trim().TrimEnd('=').Trim());
            value = parser.Expression(0);
            parser.Space();
            return parser.AtEnd;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or DivideByZeroException) { return false; }
    }
    private sealed class Parser(string input)
    {
        private int _position;
        public bool AtEnd => _position == input.Length;
        public void Space() { while (!AtEnd && char.IsWhiteSpace(input[_position])) _position++; }
        private bool Eat(char c) { Space(); if (!AtEnd && input[_position] == c) { _position++; return true; } return false; }
        public decimal Expression(int depth)
        {
            decimal value = Term(depth);
            while (true) { if (Eat('+')) value = checked(value + Term(depth)); else if (Eat('-')) value = checked(value - Term(depth)); else return value; }
        }
        private decimal Term(int depth)
        {
            decimal value = Factor(depth);
            while (true) { if (Eat('*')) value = checked(value * Factor(depth)); else if (Eat('/')) value /= Factor(depth); else return value; }
        }
        private decimal Factor(int depth)
        {
            if (depth > 32) throw new FormatException();
            if (Eat('+')) return Factor(depth + 1);
            if (Eat('-')) return -Factor(depth + 1);
            if (Eat('(')) { decimal value = Expression(depth + 1); if (!Eat(')')) throw new FormatException(); return value; }
            Space();
            int start = _position;
            while (!AtEnd && (char.IsAsciiDigit(input[_position]) || input[_position] == '.')) _position++;
            if (start == _position || !decimal.TryParse(input.AsSpan(start, _position - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number)) throw new FormatException();
            return number;
        }
    }
}
