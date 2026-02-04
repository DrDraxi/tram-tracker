namespace TramTracker.Models;

/// <summary>
/// Represents the tracking configuration (station, line, direction).
/// Used to separate time-invariant settings from time-variant tracking parameters.
/// </summary>
public class TrackingConfig
{
    public string StationName { get; set; } = "Kobylisy";
    public string? LineNumber { get; set; } = null;
    public string? Direction { get; set; } = null;
}
