using System.Text.Json.Nodes;
using TaskbarWidget;

namespace TramTracker;

/// <summary>Which stop / line / direction to track, and how far away a tram must be to be worth showing.</summary>
public sealed record Tracking(string Station, string? Line, string? Direction, int MinMinutes = 0);

/// <summary>
/// %LOCALAPPDATA%\TramTracker\config.json — same file and format as previous versions:
/// flat legacy fields (StationName/LineNumber/Direction) or TimeWindows + DefaultConfig.
/// Keys are case-insensitive; the file is re-read before every fetch so edits apply without a restart.
/// </summary>
public sealed class TramConfig
{
    readonly JsonConfig _json;

    public TramConfig(string? path = null)
    {
        _json = new JsonConfig(path ?? Path.Combine(AppKit.DataDir("TramTracker"), "config.json"));
        if (!_json.Exists) WriteDefault();
    }

    public string FilePath => _json.Path;
    public string? LoadError => _json.LoadError;

    public void Reload() => _json.Reload();

    /// <summary>GOLEMIO_API_KEY from .env / environment wins over the config's ApiKey.</summary>
    public string? ApiKey
    {
        get
        {
            var key = AppKit.Env("GOLEMIO_API_KEY") ?? _json.GetString("ApiKey");
            return key is null or "your_api_key_here" ? null : key.Trim();
        }
    }
    public int RefreshSeconds => Math.Clamp(_json.GetInt("RefreshIntervalSeconds", 30), 10, 3600);
    public int DepartureLimit => Math.Clamp(_json.GetInt("DepartureLimit", 5), 1, 100);
    public bool StartWithWindows => _json.GetBool("StartWithWindows", true);

    /// <summary>"auto" (follow taskbar theme), "white" or "black".</summary>
    public string TextColor => _json.GetString("TextColor")?.ToLowerInvariant() ?? "auto";

    public void SetStartWithWindows(bool enabled)
    {
        _json.Set("StartWithWindows", enabled);
        _json.Save();
    }

    /// <summary>The tracking target for the given moment. The first matching time window wins.</summary>
    public Tracking Active(DateTime moment)
    {
        var now = TimeOnly.FromDateTime(moment);
        if (JsonConfig.Find(_json.Root, "TimeWindows") is JsonArray windows && windows.Count > 0)
        {
            foreach (var node in windows)
            {
                if (node is not JsonObject w) continue;
                if (!TimeOnly.TryParse(JsonConfig.Str(JsonConfig.Find(w, "StartTime")) ?? "", out var start)) continue;
                if (!TimeOnly.TryParse(JsonConfig.Str(JsonConfig.Find(w, "EndTime")) ?? "", out var end)) continue;
                bool active = start < end ? now >= start && now < end : now >= start || now < end; // supports crossing midnight
                if (active && OnDay(JsonConfig.Find(w, "Days"), moment.DayOfWeek)) return Read(w);
            }
            if (JsonConfig.Find(_json.Root, "DefaultConfig") is JsonObject def) return Read(def);
            return new Tracking("Kobylisy", null, null);
        }
        return Read(_json.Root);
    }

    static readonly string[] DayNames = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };

    /// <summary>
    /// Optional "Days" on a time window: "Mon-Fri", "Sat,Sun", "weekdays", "weekends", or an array of those.
    /// Missing/empty means every day.
    /// </summary>
    internal static bool OnDay(JsonNode? days, DayOfWeek day)
    {
        var tokens = new List<string>();
        if (days is JsonArray arr) foreach (var n in arr) { if (JsonConfig.Str(n) is { } t) tokens.Add(t); }
        else if (JsonConfig.Str(days) is { } str) tokens.Add(str);
        if (tokens.Count == 0) return true;

        int d = (int)day;
        foreach (var part in tokens.SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            var p = part.ToLowerInvariant();
            if (p is "weekdays" or "workdays") { if (d is >= 1 and <= 5) return true; continue; }
            if (p is "weekends" or "weekend") { if (d is 0 or 6) return true; continue; }
            var range = p.Split('-', StringSplitOptions.TrimEntries);
            int a = Array.IndexOf(DayNames, range[0].Length >= 3 ? range[0][..3] : range[0]);
            int b = range.Length > 1 ? Array.IndexOf(DayNames, range[1].Length >= 3 ? range[1][..3] : range[1]) : a;
            if (a < 0 || b < 0) continue;
            // Ranges are Monday-based so "Fri-Mon" wraps over the weekend.
            int Mon(int x) => (x + 6) % 7;
            int ma = Mon(a), mb = Mon(b), md = Mon(d);
            if (ma <= mb ? md >= ma && md <= mb : md >= ma || md <= mb) return true;
        }
        return false;
    }

    static Tracking Read(JsonObject o) => new(
        JsonConfig.Str(JsonConfig.Find(o, "StationName")) ?? "Kobylisy",
        JsonConfig.Str(JsonConfig.Find(o, "LineNumber")),
        JsonConfig.Str(JsonConfig.Find(o, "Direction")),
        // Skip trams you can't make it to (e.g. the walk to the stop takes 5 minutes).
        Math.Clamp(JsonConfig.Int(JsonConfig.Find(o, "MinMinutes")) ?? 0, 0, 120));

    void WriteDefault()
    {
        var r = _json.Root;
        r["StationName"] = "Kobylisy";
        r["LineNumber"] = "17";
        r["Direction"] = null;
        r["RefreshIntervalSeconds"] = 30;
        r["DepartureLimit"] = 5;
        r["ApiKey"] = null;
        r["TextColor"] = "auto";
        r["StartWithWindows"] = true;
        _json.Save();
    }
}
