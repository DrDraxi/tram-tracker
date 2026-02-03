using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using TaskbarWidget;
using TramTracker.Models;
using TramTracker.Services;

namespace TramTracker.Widget;

public class TramWidget : IDisposable
{
    private const string WidgetClassName = "TramTrackerWidget";
    private const int DefaultWidth = 105;

    private readonly IGolemioService _service;
    private readonly SettingsService _settings;
    private readonly DispatcherQueue _dispatcherQueue;

    private TaskbarInjectionHelper? _injectionHelper;
    private DesktopWindowXamlSource? _xamlSource;
    private TramWidgetContent? _content;
    private bool _disposed;

    public TramWidget(IGolemioService service, SettingsService settings)
    {
        _service = service;
        _settings = settings;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _service.StateChanged += OnStateChanged;
    }

    public void Initialize()
    {
        var config = new TaskbarInjectionConfig
        {
            ClassName = WidgetClassName,
            WindowTitle = "TramTracker",
            WidthDip = DefaultWidth,
            DeferInjection = true
        };

        _injectionHelper = new TaskbarInjectionHelper(config);
        var result = _injectionHelper.Initialize();

        if (!result.Success || result.WindowHandle == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("Failed to initialize taskbar injection");
            return;
        }

        var windowId = Win32Interop.GetWindowIdFromWindow(result.WindowHandle);
        _xamlSource = new DesktopWindowXamlSource();
        _xamlSource.Initialize(windowId);
        _xamlSource.SiteBridge.ResizePolicy = Microsoft.UI.Content.ContentSizePolicy.ResizeContentToParentWindow;

        _content = new TramWidgetContent();
        _content.Clicked += OnWidgetClicked;

        var rootGrid = new Microsoft.UI.Xaml.Controls.Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
        };
        rootGrid.Children.Add(_content);

        _xamlSource.Content = rootGrid;
        _injectionHelper.Inject();
        _injectionHelper.Show();

        // Update with initial state
        _content.UpdateState(_service.CurrentState);
    }

    private void OnStateChanged(object? sender, TramState state)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _content?.UpdateState(state);
        });
    }

    private void OnWidgetClicked(object? sender, EventArgs e)
    {
        // Could open a popup with more details, or trigger a refresh
        System.Diagnostics.Debug.WriteLine("Widget clicked");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _service.StateChanged -= OnStateChanged;

        if (_content != null)
        {
            _content.Clicked -= OnWidgetClicked;
        }

        _xamlSource?.Dispose();
        _injectionHelper?.Dispose();
    }
}
