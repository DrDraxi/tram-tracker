namespace TramTracker.Models;

public class TimeWindowConfig
{
    /// <summary>
    /// Start time in 24-hour format (HH:mm), e.g. "08:00"
    /// </summary>
    public string StartTime { get; set; } = "00:00";

    /// <summary>
    /// End time in 24-hour format (HH:mm), e.g. "16:00"
    /// If EndTime < StartTime, window crosses midnight (e.g. 22:00-02:00)
    /// </summary>
    public string EndTime { get; set; } = "23:59";

    public string StationName { get; set; } = "Kobylisy";
    public string? LineNumber { get; set; } = null;
    public string? Direction { get; set; } = null;

    /// <summary>
    /// Parses the StartTime string into TimeOnly. Returns null if invalid.
    /// </summary>
    public TimeOnly? GetStartTime()
    {
        return TimeOnly.TryParse(StartTime, out var time) ? time : null;
    }

    /// <summary>
    /// Parses the EndTime string into TimeOnly. Returns null if invalid.
    /// </summary>
    public TimeOnly? GetEndTime()
    {
        return TimeOnly.TryParse(EndTime, out var time) ? time : null;
    }

    /// <summary>
    /// Checks if this time window is currently active.
    /// </summary>
    public bool IsActive(TimeOnly currentTime)
    {
        var start = GetStartTime();
        var end = GetEndTime();

        if (start == null || end == null)
            return false;

        // Normal case: StartTime < EndTime (e.g., 08:00-16:00)
        if (start < end)
        {
            return currentTime >= start && currentTime < end;
        }

        // Midnight crossing: StartTime > EndTime (e.g., 22:00-02:00)
        // Active if time >= start OR time < end
        return currentTime >= start || currentTime < end;
    }

    /// <summary>
    /// Converts to TrackingConfig for use by services.
    /// </summary>
    public TrackingConfig ToTrackingConfig()
    {
        return new TrackingConfig
        {
            StationName = StationName,
            LineNumber = LineNumber,
            Direction = Direction
        };
    }
}
