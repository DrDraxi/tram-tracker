using System.Text.Json;
using TaskbarWidget;

namespace TramTracker;

public sealed record Departure(string Line, string Headsign, string? Platform, string? StopId, DateTimeOffset Time, int? DelayMinutes);

/// <summary>Immutable snapshot of what the widget shows. Countdown values are computed at render time.</summary>
public sealed record TramState(
    Tracking Tracking,
    IReadOnlyList<Departure> Upcoming,
    DateTime? Updated,
    string? Error)
{
    public static TramState Loading(Tracking t) => new(t, Array.Empty<Departure>(), null, null);

    /// <summary>
    /// Departures you can still catch: at least <see cref="Tracking.MinMinutes"/> away (without a minimum,
    /// a tram counts as "here" until ~20 s after its time).
    /// </summary>
    public IEnumerable<Departure> Remaining(DateTimeOffset now) =>
        Upcoming.Where(d => Tracking.MinMinutes > 0 ? d.Time >= now.AddMinutes(Tracking.MinMinutes) : d.Time > now.AddSeconds(-20));
}

/// <summary>Golemio PID departure boards (https://api.golemio.cz/).</summary>
public static class Golemio
{
    const string BaseUrl = "https://api.golemio.cz/v2/pid/departureboards/";

    // The API returns at most 100 departures and has no line filter. At a big stop like Karlovo náměstí
    // 100 departures (all lines, all platforms) span only ~25 minutes, so a tram 30 minutes out was "not found".
    // A single platform's board spans ~2 hours, so we find the platform the tracked line leaves from and
    // query just that one (one request per refresh once known).
    static readonly Dictionary<Tracking, string[]> KnownPlatforms = new();
    static readonly object Gate = new();

    /// <summary>Blocking fetch — call from a background thread.</summary>
    public static TramState Fetch(string? apiKey, Tracking tracking, int limit)
    {
        if (string.IsNullOrEmpty(apiKey))
            return Fail(tracking, "No Golemio API key. Add GOLEMIO_API_KEY to a .env file next to the exe, or \"ApiKey\" to config.json.");

        var headers = new Dictionary<string, string> { ["x-access-token"] = apiKey };
        string byName = "names=" + Uri.EscapeDataString(tracking.Station);
        bool filtered = !string.IsNullOrEmpty(tracking.Line) || !string.IsNullOrEmpty(tracking.Direction);

        if (!filtered)
        {
            var (any, err) = Board(headers, byName, Math.Clamp(limit, 10, 100), 120);
            if (err != null) return Fail(tracking, err);
            return any.Count == 0
                ? new TramState(tracking, any, DateTime.Now, $"No departures from {tracking.Station} right now")
                : new TramState(tracking, any.OrderBy(d => d.Time).Take(12).ToList(), DateTime.Now, null);
        }

        var key = tracking with { MinMinutes = 0 };
        string[]? known;
        lock (Gate) KnownPlatforms.TryGetValue(key, out known);

        // 1. The platform this line/direction left from last time.
        if (known != null)
        {
            var (deps, err) = Board(headers, Ids(known), 100, 180);
            if (err != null) return Fail(tracking, err);
            var m = Match(deps, tracking);
            if (m.Count > 0) return Ok(tracking, m);
        }

        // 2. The whole station: finds the platform whenever the line is due in the next ~25 minutes.
        var (all, err2) = Board(headers, byName, 100, 120);
        if (err2 != null) return Fail(tracking, err2);
        var matches = Match(all, tracking);
        if (matches.Count > 0)
        {
            var ids = matches.Select(d => d.StopId).OfType<string>().Distinct().ToArray();
            if (ids.Length > 0)
            {
                lock (Gate) KnownPlatforms[key] = ids;
                var (deps, err3) = Board(headers, Ids(ids), 100, 180);   // longer lookahead for "then in …"
                if (err3 == null && Match(deps, tracking) is { Count: > 0 } longer) return Ok(tracking, longer);
            }
            return Ok(tracking, matches);
        }

        // 3. Nothing due soon: try the station's platforms one by one (once — the hit is remembered).
        foreach (var id in all.Select(d => d.StopId).OfType<string>().Distinct().Where(id => known == null || !known.Contains(id)).Take(10))
        {
            Thread.Sleep(150); // stay well under the API rate limit
            var (deps, err) = Board(headers, Ids(new[] { id }), 100, 180);
            if (err != null) continue;
            var m = Match(deps, tracking);
            if (m.Count > 0)
            {
                lock (Gate) KnownPlatforms[key] = new[] { id };
                return Ok(tracking, m);
            }
        }

        if (all.Count == 0) return new TramState(tracking, Array.Empty<Departure>(), DateTime.Now, $"No departures from {tracking.Station} right now");
        var wanted = string.Join(", ", new[]
        {
            tracking.Line is { Length: > 0 } l ? $"line {l}" : null,
            tracking.Direction is { Length: > 0 } dir ? $"towards \"{dir}\"" : null,
        }.Where(s => s != null));
        var available = string.Join(", ", all.Select(d => $"{d.Line} → {d.Headsign}").Distinct().Take(6));
        return new TramState(tracking, Array.Empty<Departure>(), DateTime.Now,
            $"No {wanted} from {tracking.Station} in the next 3 hours.\nAvailable now: {available}");
    }

    static TramState Ok(Tracking t, List<Departure> deps) => new(t, deps, DateTime.Now, null);
    static TramState Fail(Tracking t, string error) => new(t, Array.Empty<Departure>(), null, error);

    static string Ids(IEnumerable<string> ids) => string.Join("&", ids.Select(i => "ids=" + Uri.EscapeDataString(i)));

    static List<Departure> Match(List<Departure> deps, Tracking t) => deps
        .Where(d => (string.IsNullOrEmpty(t.Line) || d.Line.Equals(t.Line, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(t.Direction) || d.Headsign.Contains(t.Direction, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(d => d.Time)
        .Take(12) // enough to skip the ones closer than MinMinutes and still show "then in …"
        .ToList();

    static (List<Departure> Departures, string? Error) Board(Dictionary<string, string> headers, string selector, int limit, int minutesAfter)
    {
        var url = $"{BaseUrl}?{selector}&limit={limit}&minutesAfter={minutesAfter}&preferredTimezone=Europe%2FPrague";
        HttpResult res;
        try { res = Http.Get(url, headers); }
        catch (Exception ex) { return (new List<Departure>(), ex.Message); }

        if (res.Status is 401 or 403) return (new List<Departure>(), $"Golemio rejected the API key (HTTP {res.Status})");
        if (res.Status == 404) return (new List<Departure>(), "Stop not found");
        if (res.Status == 429) return (new List<Departure>(), "Golemio rate limit hit, retrying shortly");
        if (!res.IsSuccess) return (new List<Departure>(), $"Golemio API error (HTTP {res.Status})");
        try { return (Parse(res.Body), null); }
        catch (Exception ex) { return (new List<Departure>(), "Unexpected API response: " + ex.Message); }
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
            string? stopId = Get(d, "stop", "id");

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

            list.Add(new Departure(line, headsign, platform, stopId, time.Value, delay));
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
