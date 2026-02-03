namespace TramTracker.Models;

public class TramState
{
    public string LineNumber { get; set; } = "--";
    public string Direction { get; set; } = "";
    public int MinutesToArrival { get; set; } = -1;
    public int? DelayMinutes { get; set; }
    public DateTime? PredictedArrival { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Vehicle position as a value from 0.0 to 1.0
    /// 0.0 = far away (before first stop)
    /// 1.0 = at the station
    /// </summary>
    public double VehiclePosition { get; set; } = 0.0;

    public bool HasData => MinutesToArrival >= 0 && ErrorMessage == null;

    public string FormattedArrivalTime
    {
        get
        {
            if (!HasData) return "--";
            if (MinutesToArrival == 0) return "<1m";
            return $"{MinutesToArrival}m";
        }
    }

    public string TooltipText
    {
        get
        {
            if (!HasData)
            {
                return ErrorMessage ?? "No data available";
            }

            var lines = new List<string>
            {
                $"Line {LineNumber} to {Direction}",
                $"Arrives in: {FormattedArrivalTime}"
            };

            if (PredictedArrival.HasValue)
            {
                lines.Add($"At: {PredictedArrival.Value:HH:mm}");
            }

            if (DelayMinutes.HasValue && DelayMinutes.Value > 0)
            {
                lines.Add($"Delay: +{DelayMinutes.Value}min");
            }

            if (LastUpdated != DateTime.MinValue)
            {
                lines.Add($"Updated: {LastUpdated:HH:mm:ss}");
            }

            return string.Join("\n", lines);
        }
    }

    public static TramState FromDeparture(Departure departure)
    {
        var state = new TramState
        {
            LineNumber = departure.Route?.ShortName ?? "--",
            Direction = departure.Trip?.Headsign ?? "",
            DelayMinutes = departure.Delay?.Minutes,
            PredictedArrival = departure.DepartureTimestamp?.Predicted,
            LastUpdated = DateTime.Now
        };

        if (departure.DepartureTimestamp?.Predicted.HasValue == true)
        {
            var diff = departure.DepartureTimestamp.Predicted.Value - DateTime.Now;
            state.MinutesToArrival = Math.Max(0, (int)diff.TotalMinutes);
            state.VehiclePosition = CalculateVehiclePosition(state.MinutesToArrival);
        }

        return state;
    }

    public static TramState Error(string message)
    {
        return new TramState
        {
            ErrorMessage = message,
            LastUpdated = DateTime.Now
        };
    }

    private static double CalculateVehiclePosition(int minutesToArrival)
    {
        // < 1 min: at station (position 1.0)
        // 1-3 min: approaching (position 0.5-1.0)
        // 3-6 min: further away (position 0.0-0.5)
        // > 6 min: before first stop (position 0.0)

        if (minutesToArrival < 1) return 1.0;
        if (minutesToArrival <= 3) return 0.5 + (3 - minutesToArrival) * 0.25;
        if (minutesToArrival <= 6) return (6 - minutesToArrival) * 0.167;
        return 0.0;
    }
}
