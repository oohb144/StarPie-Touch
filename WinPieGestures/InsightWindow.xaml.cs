using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using StarPie.Plugin;
using WinPieGestures.Insight;
using WinPieGestures.Plugins;

namespace WinPieGestures;

public partial class InsightWindow : Window
{
    private static int _openCount;
    internal static bool IsSessionOpen => Volatile.Read(ref _openCount) > 0;
    private readonly PluginContentInsightService _service;
    private readonly InsightSessionRequest _request;
    private Func<string, InsightAnalysis>? _classify;
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _token;
    private readonly CancellationTokenRegistration _cancelRegistration;
    private string _text = "";
    private InsightSource _source;
    private bool _updating;
    private bool _needsAnalysis;
    private bool _busy;
    private LanguageCode _displayLanguage = I18n.CurrentLanguage;
    private ScreenSnipWindow? _capture;
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    internal InsightWindow(PluginContentInsightService service, InsightSessionRequest request,
        Func<string, InsightAnalysis> classify, CancellationToken cancellation)
    {
        InitializeComponent();
        _service = service; _request = request; _classify = classify;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _token = _lifetime.Token;
        AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig?.AppTheme ?? "System");
        ApplyLocalization();
        I18n.LanguageChanged += ApplyLocalization;
        _cancelRegistration = cancellation.Register(() => Dispatcher.BeginInvoke(new Action(Close)));
        ContentRendered += (_, _) => PlaceNearOrigin();
        Interlocked.Increment(ref _openCount);
    }

    private void ApplyLocalization()
    {
        foreach (var entry in InsightText.All)
            if (StatusText.Text == entry.Value.Get(_displayLanguage))
            { StatusText.Text = entry.Value.Get(I18n.CurrentLanguage); break; }
        _displayLanguage = I18n.CurrentLanguage;
        Title = InsightText.T("Title");
        CaptureButton.Content = InsightText.T("ScreenButton"); ClipboardButton.Content = InsightText.T("ClipboardButton");
        EditButton.Content = InsightText.T("EditButton"); CloseButton.Content = InsightText.T("Close");
        AnalyzeButton.Content = InsightText.T("Analyze");
        SourceText.Text = InsightText.T(_source.ToString());
        if (_classify != null && !_updating && !_busy && !_needsAnalysis) Analyze();
    }

    internal void SetContent(string text, InsightSource source, string notice = "")
    {
        if (text.Length > 8192) { text = ""; notice = InsightText.T("TooLong"); }
        _text = text; _source = source; _needsAnalysis = false; _updating = true;
        ContentEditor.Text = text; _updating = false;
        SourceText.Text = InsightText.T(source.ToString());
        PreviewText.Text = text;
        Analyze(); StatusText.Text = notice;
    }

    private void Analyze()
    {
        if (_classify == null) return;
        InsightAnalysis result = _classify(_text);
        AnalysisTitle.Text = result.Title; ResultText.Text = result.Result;
        OperationsPanel.Children.Clear();
        for (int i = 0; i < Math.Min(result.Candidates.Count, 8); i++)
        {
            InsightCandidate candidate = result.Candidates[i];
            var button = new Button { Content = candidate.Label, Tag = candidate, MaxWidth = 390 };
            if (i == 0) { button.Background = (Brush)FindResource("AccentPrimaryBrush"); button.Foreground = Brushes.White; }
            button.Click += Operation_Click;
            OperationsPanel.Children.Add(button);
        }
    }

    private async void Operation_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _needsAnalysis || sender is not Button { Tag: InsightCandidate candidate }) return;
        try
        {
            SetBusy(true);
            bool success = candidate.Operation == InsightOperation.Copy
                ? _service.Execute(candidate, _request.SearchUrlTemplate, _token)
                : await Task.Run(() => _service.Execute(candidate, _request.SearchUrlTemplate, _token)).WaitAsync(_token);
            if (_lifetime.IsCancellationRequested) return;
            if (!success) StatusText.Text = InsightText.T("Failed");
            else if (candidate.Operation == InsightOperation.Copy) StatusText.Text = InsightText.T("Copied");
            else Close();
        }
        catch (PluginCapabilityDeniedException ex) { StatusText.Text = ex.Message; }
        catch (OperationCanceledException) { }
        finally { if (!_lifetime.IsCancellationRequested) SetBusy(false); }
    }

    private async void Clipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = await _service.ReadClipboardAsync();
            SetContent(text, InsightSource.Clipboard, text.Length == 0 ? InsightText.T("EmptyClipboard") : "");
        }
        catch (Exception ex) { StatusText.Text = ex is PluginCapabilityDeniedException ? ex.Message : InsightText.T("Failed"); }
    }
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        ContentEditor.Visibility = Visibility.Visible; AnalyzeButton.Visibility = Visibility.Visible;
        Activate(); ContentEditor.Focus(); ContentEditor.CaretIndex = ContentEditor.Text.Length;
    }
    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating || OperationsPanel == null) return;
        _needsAnalysis = true;
        OperationsPanel.Children.Clear(); ResultText.Text = "";
    }
    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        _text = ContentEditor.Text; _source = InsightSource.Input; _needsAnalysis = false;
        SourceText.Text = InsightText.T("Input"); PreviewText.Text = _text; StatusText.Text = ""; Analyze();
    }
    private void Capture_Click(object sender, RoutedEventArgs e) => StartScreenCapture();
    internal async void StartScreenCapture()
    {
        if (_busy) return;
        try
        {
            _service.Require(PluginCapability.ScreenCapture);
            SetBusy(true);
            Hide(); await Task.Delay(40, _token);
            var selected = new TaskCompletionSource<System.Drawing.Bitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var selectionCancellation = _token.Register(() => selected.TrySetCanceled(_token));
            _capture = new ScreenSnipWindow(bitmap => { if (!selected.TrySetResult(bitmap)) bitmap?.Dispose(); });
            _capture.Show();
            var bitmap = await selected.Task;
            _capture = null;
            if (_lifetime.IsCancellationRequested) { bitmap?.Dispose(); return; }
            Show(); StatusText.Text = bitmap == null ? "" : InsightText.T("Recognizing");
            if (bitmap != null)
            {
                // OCR owns and disposes the bitmap; cancellation of the session never leaves a UI callback.
                var recognition = await OcrManager.RecognizeForInsightAsync(bitmap);
                _token.ThrowIfCancellationRequested();
                SetContent(recognition.Success ? recognition.Text : "", InsightSource.Screen,
                    InsightText.T(recognition.Success ? "ReviewOcr" : "OcrFailed"));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_lifetime.IsCancellationRequested) { Show(); StatusText.Text = ex is PluginCapabilityDeniedException ? ex.Message : InsightText.T("OcrFailed"); } }
        finally { if (!_lifetime.IsCancellationRequested) SetBusy(false); }
    }
    private void SetBusy(bool busy)
    {
        _busy = busy;
        CaptureButton.IsEnabled = ClipboardButton.IsEnabled = EditButton.IsEnabled = !busy;
        OperationsPanel.IsEnabled = ContentEditor.IsEnabled = AnalyzeButton.IsEnabled = !busy;
        // Close and Escape remain available during OCR and slow network-path checks.
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }

    private void PlaceNearOrigin()
    {
        var anchor = new System.Drawing.Point(_request.Context.CursorX, _request.Context.CursorY);
        var area = System.Windows.Forms.Screen.FromPoint(anchor).WorkingArea;
        var scale = RadialWindow.GetMonitorDpiScale(new Point(anchor.X, anchor.Y));
        Width = Math.Min(460, Math.Max(240, area.Width / scale.scaleX - 24));
        MaxHeight = Math.Max(180, area.Height / scale.scaleY - 24);
        UpdateLayout();
        int width = (int)Math.Ceiling(ActualWidth * scale.scaleX);
        int height = (int)Math.Ceiling(ActualHeight * scale.scaleY);
        int x = Math.Clamp(anchor.X + 20, area.Left + 12, Math.Max(area.Left + 12, area.Right - width - 12));
        int y = Math.Clamp(anchor.Y + 20, area.Top + 12, Math.Max(area.Top + 12, area.Bottom - height - 12));
        SetWindowPos(new WindowInteropHelper(this).Handle, 0, x, y, 0, 0, 0x0011); // NOSIZE | NOACTIVATE
    }

    protected override void OnClosed(EventArgs e)
    {
        Interlocked.Decrement(ref _openCount);
        I18n.LanguageChanged -= ApplyLocalization;
        _cancelRegistration.Dispose();
        _lifetime.Cancel(); _capture?.Close(); _capture = null;
        _lifetime.Dispose();
        _classify = null; OperationsPanel.Children.Clear();
        base.OnClosed(e);
    }
}
