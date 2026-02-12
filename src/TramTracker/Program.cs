using TramTracker.Services;
using TramTracker.Widget;

namespace TramTracker;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var settings = new SettingsService();
        var golemioService = new GolemioService(settings);

        StartupService.SyncWithConfig(settings.Config.StartWithWindows);

        var widget = new TramWidget(golemioService, settings);
        widget.Initialize();

        TaskbarWidget.Widget.RunMessageLoop();
    }
}
