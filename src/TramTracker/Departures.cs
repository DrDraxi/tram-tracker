using System.Text.Json;
using TaskbarWidget;

namespace TramTracker;

public sealed record Departure(string Line, string Headsign, string? Platform, DateTimeOffset Time, int? DelayMinutes);

/// <summary>Immutable snapshot of what the widget shows. Countdown values are computed at render time.</summary>
public sealed record TramState(
    Tracking Tracking,
    IReadOnlyList<Departure> Upcoming,
    DateTime? Updated,
    string? Error)
{
    public static TramState Loading(Tracking t) => new(t, Array.Empty<Departure>(), null, null);

    /// <summary>Departures that haven't left yet (a tram counts as "here" until ~20 s after its time).</summary>
    public IEnumerable<Departure> Remaining(DateTimeOffset now) =>
        Upcoming.Where(d => d.Time > now.AddSeconds(-20));
}

/// <summary>Golemio PID departure boards (https://api.golemio.cz/).</summary>
public static class Golemio
{
    const string BaseUrl = "https://api.golemio.cz/v2/pid/departureboards/";

    /// <summary>Blocking fetch — call from a background thread.</summary>
    public static TramState Fetch(string? apiKey, Tracking tracking, int limit)
    {
        if (string.IsNullOrEmpty(apiKey))
            return new TramState(tracking, Array.Empty<Departure>(), null,
                "No Golemio API key. Add GOLEMIO_API_KEY to a .env file next to the exe, or \"ApiKey\" to config.json.");

        // Busy stops serve many lines; ask for enough departures that the tracked line is in the list
        // (the old default of 5 regularly produced "no match" at stations like Malostranská).
        int effectiveLimit = string.IsNullOrEmpty(tracking.Line) && string.IsNullOrEmpty(tracking.Direction) ? limit : Math.Max(limit, 40);
        var url = $"{BaseUrl}?names={Uri.EscapeDataString(tracking.Station)}&limit={effectiveLimit}&preferredTimezone=Europe%2FPrague";

        HttpResult res;
        try
        {
            res = Http.Get(url, new Dictionary<string, string> { ["x-access-token"] = apiKey });
        }
        catch (Exception ex)
        {
            return new TramState(tracking, Array.Empty<Departure>(), null, ex.Message);
        }

        if (res.Status is 401 or 403) return new TramState(tracking, Array.Empty<Departure>(), null, "Golemio rejected the API key (HTTP " + res.Status + ")");
        if (res.Status == 404) return new TramState(tracking, Array.Empty<Departure>(), null, $"Stop \"{tracking.Station}\" not found");
        if (!res.IsSuccess) return new TramState(tracking, Array.Empty<Departure>(), null, $"Golemio API error (HTTP {res.Status})");

        List<Departure> all;
        try { all = Parse(res.Body); }
        catch (Exception ex) { return new TramState(tracking, Array.Empty<Departure>(), null, "Unexpected API response: " + ex.Message); }

        var matching = all.Where(d =>
                (string.IsNullOrEmpty(tracking.Line) || d.Line.Equals(tracking.Line, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(tracking.Direction) || d.Headsign.Contains(tracking.Direction, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(d => d.Time)
            .Take(4)
            .ToList();

        if (matching.Count == 0)
        {
            if (all.Count == 0) return new TramState(tracking, matching, DateTime.Now, $"No departures from {tracking.Station} right now");
            var wanted = string.Join(", ", new[]
            {
                tracking.Line is { Length: > 0 } l ? $"line {l}" : null,
                tracking.Direction is { Length: > 0 } dir ? $"towards \"{dir}\"" : null,
            }.Where(s => s != null));
            var available = string.Join(", ", all.Select(d => $"{d.Line} → {d.Headsign}").Distinct().Take(6));
            return new TramState(tracking, matching, DateTime.Now, $"Nothing matches {wanted} at {tracking.Station}.\nAvailable: {available}");
        }

        return new TramState(tracking, matching, DateTime.Now, null);
    }

    static List<Departure> Parse(byte[] json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<Departure>();
        if (!doc.RootElement.TryGetProperty("departures", out var deps) || deps.ValueKind != JsonValueKind.Array) return list;

        foreach (var d in deps.EnumerateArray())
        {
            string line = Get(d, "route", "short_name") ?? "?";
            string headsign = Get(d, "trip", "headsign") ?? "";
            string? platform = Get(d, "stop", "platform_code");

            // Predicted time when the vehicle is tracked, scheduled otherwise (the old widget showed "--"
            // for untracked departures even though a scheduled time was available).
            DateTimeOffset? time = null;
            if (d.TryGetProperty("departure_timestamp", out var ts))
                time = Time(ts, "predicted") ?? Time(ts, "scheduled");
            if (time == null) continue;

            int? delay = null;
            if (d.TryGetProperty("delay", out var dl) && dl.ValueKind == JsonValueKind.Object
                && dl.TryGetProperty("minutes", out var m) && m.ValueKind == JsonValueKind.Number)
                delay = m.GetInt32();

            list.Add(new Departure(line, headsign, platform, time.Value, delay));
        }
        return list;
    }

    static string? Get(JsonElement e, string obj, string prop) =>
        e.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object &&
        o.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    static DateTimeOffset? Time(JsonElement ts, string prop) =>
        ts.ValueKind == JsonValueKind.Object && ts.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String
        && p.TryGetDateTimeOffset(out var v) ? v : null;
}
