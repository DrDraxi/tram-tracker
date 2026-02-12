using TaskbarWidget;
using TaskbarWidget.Rendering;
using TramTracker.Models;
using TramTracker.Services;

namespace TramTracker.Widget;

public class TramWidget : IDisposable
{
    // Route visualization constants (DIP)
    private const int CanvasWidth = 36;
    private const int CanvasHeight = 20;
    private const int StopSpacing = 12;
    private const int FirstStopX = 6;
    private const int LineY = 10;
    private const int StopRadius = 3;
    private const int VehicleRadius = 4;

    private static readonly Color GreenColor = Color.FromRgb(76, 175, 80);
    private static readonly Color OrangeColor = Color.FromRgb(255, 152, 0);
    private static readonly Color RedColor = Color.FromRgb(244, 67, 54);
    private static readonly Color GrayColor = Color.FromArgb(180, 128, 128, 128);
    private static readonly Color WhiteColor = Color.FromRgb(255, 255, 255);
    private static readonly Color YellowBadge = Color.FromRgb(255, 215, 0);
    private static readonly Color BlackColor = Color.FromRgb(0, 0, 0);

    private readonly IGolemioService _service;
    private readonly SettingsService _settings;
    private TaskbarWidget.Widget? _widget;
    private TramState _state = new();
    private bool _disposed;

    public TramWidget(IGolemioService service, SettingsService settings)
    {
        _service = service;
        _settings = settings;
        _service.StateChanged += OnStateChanged;
    }

    public void Initialize()
    {
        _state = _service.CurrentState;

        _widget = new TaskbarWidget.Widget("Tram", render: ctx =>
        {
            var state = _state;

            ctx.Horizontal(6, h =>
            {
                // Route visualization canvas
                h.Canvas(CanvasWidth, CanvasHeight, canvas =>
                {
                    DrawRoute(canvas, state);
                });

                // Line number badge
                h.Panel(p =>
                {
                    p.Size(18, 18);
                    p.Background(YellowBadge);
                    p.CornerRadius(3);
                    p.DrawText(state.LineNumber, new TextStyle
                    {
                        Color = BlackColor
                    });
                });

                // Arrival time
                h.DrawText(state.FormattedArrivalTime);
            });

            ctx.Tooltip(state.TooltipText);
        });

        _widget.Show();

        // Start refresh timer (30s default)
        var interval = _settings.Config.RefreshIntervalSeconds;
        if (interval <= 0) interval = 30;
        _widget.SetInterval(TimeSpan.FromSeconds(interval), () =>
        {
            _ = Task.Run(async () =>
            {
                await _service.FetchDeparturesAsync();
            });
        });

        // Initial fetch
        _ = Task.Run(async () =>
        {
            await _service.FetchDeparturesAsync();
        });
    }

    private void DrawRoute(CanvasContext canvas, TramState state)
    {
        var lastStopX = FirstStopX + StopSpacing * 2;
        var vehicleColor = GetVehicleColor(state.DelayMinutes);
        var vehicleX = (int)(FirstStopX + state.VehiclePosition * (lastStopX - FirstStopX));

        // Remaining path (gray line)
        canvas.DrawLine(FirstStopX, LineY, lastStopX, LineY, 2, GrayColor);

        // Traveled path (colored line to vehicle position)
        if (vehicleX > FirstStopX)
            canvas.DrawLine(FirstStopX, LineY, vehicleX, LineY, 2, vehicleColor);

        // 3 stop indicators
        for (int i = 0; i < 3; i++)
        {
            var x = FirstStopX + i * StopSpacing;
            var stopPosition = i / 2.0;
            var isPassed = state.VehiclePosition > stopPosition + 0.1;
            var isUserStation = i == 2;

            if (isUserStation)
            {
                canvas.DrawCircle(x, LineY, StopRadius, WhiteColor);
                if (state.VehiclePosition >= 0.95)
                    canvas.DrawFilledCircle(x, LineY, StopRadius, WhiteColor);
            }
            else if (isPassed)
            {
                canvas.DrawCircle(x, LineY, StopRadius, GrayColor);
                canvas.DrawFilledCircle(x, LineY, StopRadius - 1, vehicleColor);
            }
            else
            {
                canvas.DrawCircle(x, LineY, StopRadius, GrayColor);
            }
        }

        // Vehicle indicator (on top)
        canvas.DrawFilledCircle(vehicleX, LineY, VehicleRadius, vehicleColor);
    }

    private static Color GetVehicleColor(int? delayMinutes)
    {
        if (delayMinutes == null || delayMinutes <= 1) return GreenColor;
        if (delayMinutes <= 3) return OrangeColor;
        return RedColor;
    }

    private void OnStateChanged(object? sender, TramState state)
    {
        _state = state;
        _widget?.Invalidate();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service.StateChanged -= OnStateChanged;
        _widget?.Dispose();
    }
}
