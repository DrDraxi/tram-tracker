namespace TramTracker.Models;

public class AppConfig
{
    // Legacy fields - still used in old format
    public string StationName { get; set; } = "Kobylisy";
    public string? LineNumber { get; set; } = "17";
    public string? Direction { get; set; } = null;

    // Global settings (apply to all time windows)
    public int RefreshIntervalSeconds { get; set; } = 30;
    public int DepartureLimit { get; set; } = 5;
    public string? ApiKey { get; set; } = null;

    // NEW: Time-based configuration fields
    public List<TimeWindowConfig>? TimeWindows { get; set; } = null;
    public TrackingConfig? DefaultConfig { get; set; } = null;

    /// <summary>
    /// Determines if this config uses the new time-based format.
    /// </summary>
    public bool IsTimeBasedConfig()
    {
        return TimeWindows != null && TimeWindows.Count > 0;
    }

    /// <summary>
    /// Gets the active tracking configuration based on current time.
    /// Falls back to legacy fields or DefaultConfig if no time window matches.
    /// </summary>
    public TrackingConfig GetActiveTrackingConfig()
    {
        if (!IsTimeBasedConfig())
        {
            // Legacy format - use top-level fields
            return new TrackingConfig
            {
                StationName = StationName,
                LineNumber = LineNumber,
                Direction = Direction
            };
        }

        // Time-based format - find matching window
        var currentTime = TimeOnly.FromDateTime(DateTime.Now);
        var activeWindow = TimeWindows!.FirstOrDefault(w => w.IsActive(currentTime));

        if (activeWindow != null)
        {
            return activeWindow.ToTrackingConfig();
        }

        // No matching window - use default
        if (DefaultConfig != null)
        {
            return DefaultConfig;
        }

        // Ultimate fallback
        return new TrackingConfig
        {
            StationName = "Kobylisy",
            LineNumber = null,
            Direction = null
        };
    }
}
