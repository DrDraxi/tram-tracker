using System.Text.Json;
using System.Text.Json.Serialization;

namespace TramTracker.Models;

public class GolemioResponse
{
    [JsonPropertyName("departures")]
    public List<Departure> Departures { get; set; } = new();

    [JsonPropertyName("stops")]
    public List<Stop> Stops { get; set; } = new();
}

public class Departure
{
    [JsonPropertyName("route")]
    public Route? Route { get; set; }

    [JsonPropertyName("trip")]
    public Trip? Trip { get; set; }

    [JsonPropertyName("departure_timestamp")]
    public DepartureTimestamp? DepartureTimestamp { get; set; }

    [JsonPropertyName("delay")]
    public Delay? Delay { get; set; }

    [JsonPropertyName("stop")]
    public Stop? Stop { get; set; }
}

public class Route
{
    [JsonPropertyName("short_name")]
    public string? ShortName { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }
}

public class Trip
{
    [JsonPropertyName("headsign")]
    public string? Headsign { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    // These can be bool, null, or sometimes other types from API - ignore them
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class DepartureTimestamp
{
    [JsonPropertyName("predicted")]
    public DateTime? Predicted { get; set; }

    [JsonPropertyName("scheduled")]
    public DateTime? Scheduled { get; set; }
}

public class Delay
{
    [JsonPropertyName("minutes")]
    public int? Minutes { get; set; }

    [JsonPropertyName("seconds")]
    public int? Seconds { get; set; }

    [JsonPropertyName("is_available")]
    public bool? IsAvailable { get; set; }
}

public class Stop
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("platform_code")]
    public string? PlatformCode { get; set; }
}
