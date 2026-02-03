using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TramTracker.Services;
using TramTracker.Widget;

namespace TramTracker;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TramWidget? _widget;
    private DispatcherQueueTimer? _refreshTimer;

    private readonly SettingsService _settings;
    private readonly IGolemioService _golemioService;

    public App()
    {
        InitializeComponent();
        _settings = new SettingsService();
        _golemioService = new GolemioService(_settings);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Create hidden window (required for WinUI lifecycle)
        _mainWindow = new MainWindow();
        // Don't call Activate() - keep window hidden

        // Create and show the widget
        _widget = new TramWidget(_golemioService, _settings);
        _widget.Initialize();

        // Start the refresh timer
        StartRefreshTimer();

        // Do initial fetch
        _ = RefreshAsync();
    }

    private void StartRefreshTimer()
    {
        var intervalSeconds = _settings.Config.RefreshIntervalSeconds;
        if (intervalSeconds <= 0) intervalSeconds = 30;

        _refreshTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
        _refreshTimer.Tick += (s, e) => _ = RefreshAsync();
        _refreshTimer.Start();
    }

    private async Task RefreshAsync()
    {
        try
        {
            await Task.Run(async () => await _golemioService.FetchDeparturesAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Refresh error: {ex.Message}");
        }
    }
}
