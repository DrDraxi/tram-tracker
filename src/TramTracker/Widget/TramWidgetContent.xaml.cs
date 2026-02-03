using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TramTracker.Models;
using Windows.UI;

namespace TramTracker.Widget;

public sealed partial class TramWidgetContent : UserControl
{
    private const double StopRadius = 3;
    private const double VehicleRadius = 4;
    private const double LineY = 10;  // Center of canvas
    private const double StopSpacing = 12;
    private const double FirstStopX = 6;

    private Ellipse? _vehicleIndicator;
    private readonly Ellipse[] _stopIndicators = new Ellipse[3];
    private Line? _traveledLine;    // Green line showing traveled path
    private Line? _remainingLine;   // Gray line showing remaining path

    // Colors
    private static readonly Color GreenColor = Color.FromArgb(255, 76, 175, 80);
    private static readonly Color GrayColor = Color.FromArgb(180, 128, 128, 128);

    public event EventHandler? Clicked;

    public TramWidgetContent()
    {
        InitializeComponent();

        HoverBorder.PointerPressed += OnPointerPressed;
        HoverBorder.PointerEntered += OnPointerEntered;
        HoverBorder.PointerExited += OnPointerExited;

        DrawRouteVisualization();
    }

    private void DrawRouteVisualization()
    {
        RouteCanvas.Children.Clear();

        var lastStopX = FirstStopX + StopSpacing * 2;

        // Draw remaining path line (gray) - full length, will be covered by traveled line
        _remainingLine = new Line
        {
            X1 = FirstStopX,
            Y1 = LineY,
            X2 = lastStopX,
            Y2 = LineY,
            Stroke = new SolidColorBrush(GrayColor),
            StrokeThickness = 2
        };
        RouteCanvas.Children.Add(_remainingLine);

        // Draw traveled path line (green) - starts at 0 length
        _traveledLine = new Line
        {
            X1 = FirstStopX,
            Y1 = LineY,
            X2 = FirstStopX,  // Will be updated based on vehicle position
            Y2 = LineY,
            Stroke = new SolidColorBrush(GreenColor),
            StrokeThickness = 2
        };
        RouteCanvas.Children.Add(_traveledLine);

        // Draw 3 stop indicators
        for (int i = 0; i < 3; i++)
        {
            var x = FirstStopX + i * StopSpacing;
            var stop = new Ellipse
            {
                Width = StopRadius * 2,
                Height = StopRadius * 2,
                Stroke = new SolidColorBrush(GrayColor),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Colors.Transparent)
            };
            Canvas.SetLeft(stop, x - StopRadius);
            Canvas.SetTop(stop, LineY - StopRadius);
            RouteCanvas.Children.Add(stop);
            _stopIndicators[i] = stop;
        }

        // Draw vehicle indicator (filled green circle) - on top
        _vehicleIndicator = new Ellipse
        {
            Width = VehicleRadius * 2,
            Height = VehicleRadius * 2,
            Fill = new SolidColorBrush(GreenColor)
        };
        Canvas.SetLeft(_vehicleIndicator, FirstStopX - VehicleRadius);
        Canvas.SetTop(_vehicleIndicator, LineY - VehicleRadius);
        RouteCanvas.Children.Add(_vehicleIndicator);
    }

    public void UpdateState(TramState state)
    {
        LineText.Text = state.LineNumber;
        ArrivalText.Text = state.FormattedArrivalTime;
        StatusToolTip.Content = state.TooltipText;

        // Update vehicle position
        UpdateVehiclePosition(state.VehiclePosition);

        // Update vehicle color based on delay
        var vehicleColor = GetVehicleColor(state.DelayMinutes);
        UpdateVehicleColor(vehicleColor);

        // Highlight stops based on vehicle position
        UpdateStopHighlights(state.VehiclePosition, vehicleColor);
    }

    private Color GetVehicleColor(int? delayMinutes)
    {
        if (delayMinutes == null || delayMinutes <= 1)
            return GreenColor;
        else if (delayMinutes <= 3)
            return Color.FromArgb(255, 255, 152, 0);  // Orange
        else
            return Color.FromArgb(255, 244, 67, 54);  // Red
    }

    private void UpdateVehiclePosition(double position)
    {
        if (_vehicleIndicator == null || _traveledLine == null) return;

        // Position goes from 0.0 (first stop) to 1.0 (last stop/current station)
        var totalDistance = StopSpacing * 2;
        var x = FirstStopX + position * totalDistance;

        // Move vehicle
        Canvas.SetLeft(_vehicleIndicator, x - VehicleRadius);

        // Extend traveled line to vehicle position
        _traveledLine.X2 = x;
    }

    private void UpdateVehicleColor(Color color)
    {
        if (_vehicleIndicator == null || _traveledLine == null) return;

        _vehicleIndicator.Fill = new SolidColorBrush(color);
        _traveledLine.Stroke = new SolidColorBrush(color);
    }

    private void UpdateStopHighlights(double position, Color vehicleColor)
    {
        // Stop positions: 0 = 0.0, 1 = 0.5, 2 = 1.0 (user's station)
        for (int i = 0; i < _stopIndicators.Length; i++)
        {
            if (_stopIndicators[i] == null) continue;

            var stopPosition = i / 2.0;  // 0, 0.5, 1.0
            var isPassed = position > stopPosition + 0.1;  // Vehicle has passed this stop
            var isUserStation = i == 2;  // Last stop is user's station

            if (isUserStation)
            {
                // User's station - always white outline, filled when vehicle arrives
                _stopIndicators[i].Stroke = new SolidColorBrush(Colors.White);
                _stopIndicators[i].Fill = position >= 0.95
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Colors.Transparent);
            }
            else if (isPassed)
            {
                // Passed stop - gray outline, colored fill (matches vehicle/line color)
                _stopIndicators[i].Stroke = new SolidColorBrush(GrayColor);
                _stopIndicators[i].Fill = new SolidColorBrush(vehicleColor);
            }
            else
            {
                // Upcoming stop - gray hollow
                _stopIndicators[i].Stroke = new SolidColorBrush(GrayColor);
                _stopIndicators[i].Fill = new SolidColorBrush(Colors.Transparent);
            }
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        HoverBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255));
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        HoverBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }
}
