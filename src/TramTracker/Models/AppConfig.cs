namespace TramTracker.Models;

public class AppConfig
{
    public string StationName { get; set; } = "Kobylisy";
    public string? LineNumber { get; set; } = "17";
    public string? Direction { get; set; } = null;
    public int RefreshIntervalSeconds { get; set; } = 30;
    public int DepartureLimit { get; set; } = 5;
}
