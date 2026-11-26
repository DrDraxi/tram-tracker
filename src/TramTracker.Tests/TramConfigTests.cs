using System.Text.Json.Nodes;
using TramTracker;

namespace TramTracker.Tests;

public sealed class TramConfigTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("tram-test-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    TramConfig Write(string json)
    {
        var path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, json);
        return new TramConfig(path);
    }

    const string Commute = """
    {
      "TimeWindows": [
        { "StartTime": "10:00", "EndTime": "15:59", "StationName": "Chotkovy sady", "LineNumber": "12", "Direction": "Lehovec" },
        { "StartTime": "16:00", "EndTime": "18:10", "Days": "Mon-Fri", "StationName": "Karlovo náměstí", "LineNumber": "3", "Direction": "Sídliště Modřany" },
        { "StartTime": "16:00", "EndTime": "00:00", "StationName": "Jiráskovo náměstí", "LineNumber": "17", "Direction": "Modřany" }
      ],
      "DefaultConfig": { "StationName": "Jiráskovo náměstí", "LineNumber": "17", "Direction": "Libuš" }
    }
    """;

    // 2026-11-27 is a Friday, 2026-11-28 a Saturday.
    [Theory]
    [InlineData("2026-11-27 12:00", "12")]
    [InlineData("2026-11-27 16:00", "3")]
    [InlineData("2026-11-27 18:09", "3")]
    [InlineData("2026-11-27 18:10", "17")]
    [InlineData("2026-11-27 23:30", "17")]
    [InlineData("2026-11-28 17:00", "17")]   // weekend: tram 3 doesn't go to Modřany
    [InlineData("2026-11-28 08:00", "17")]   // default
    public void PicksTheRightWindow(string at, string line)
    {
        var cfg = Write(Commute);
        Assert.Equal(line, cfg.Active(DateTime.Parse(at)).Line);
    }

    [Fact]
    public void DefaultUsedOutsideWindows() =>
        Assert.Equal("Libuš", Write(Commute).Active(DateTime.Parse("2026-11-27 07:00")).Direction);

    [Fact]
    public void LegacyFlatConfigStillWorks()
    {
        var cfg = Write("""{ "StationName": "Kobylisy", "LineNumber": "17", "Direction": null }""");
        var t = cfg.Active(DateTime.Parse("2026-11-27 12:00"));
        Assert.Equal(("Kobylisy", "17", (string?)null), (t.Station, t.Line, t.Direction));
    }

    [Fact]
    public void WindowsCanCrossMidnight()
    {
        var cfg = Write("""{ "TimeWindows": [ { "StartTime": "22:00", "EndTime": "02:00", "StationName": "Night", "LineNumber": "91" } ] }""");
        Assert.Equal("91", cfg.Active(DateTime.Parse("2026-11-27 23:00")).Line);
        Assert.Equal("91", cfg.Active(DateTime.Parse("2026-11-28 01:30")).Line);
        Assert.Null(cfg.Active(DateTime.Parse("2026-11-27 03:00")).Line);
    }

    [Theory]
    [InlineData("Mon-Fri", DayOfWeek.Friday, true)]
    [InlineData("Mon-Fri", DayOfWeek.Saturday, false)]
    [InlineData("weekends", DayOfWeek.Sunday, true)]
    [InlineData("Sat,Sun", DayOfWeek.Monday, false)]
    [InlineData("Fri-Mon", DayOfWeek.Sunday, true)]
    [InlineData("Fri-Mon", DayOfWeek.Wednesday, false)]
    [InlineData("Tuesday", DayOfWeek.Tuesday, true)]
    [InlineData("", DayOfWeek.Wednesday, true)]
    public void DayFilter(string days, DayOfWeek day, bool expected) =>
        Assert.Equal(expected, TramConfig.OnDay(JsonValue.Create(days), day));

    [Fact]
    public void DayFilterAcceptsArrays() =>
        Assert.True(TramConfig.OnDay(new JsonArray("Mon", "Wed-Thu"), DayOfWeek.Thursday));
}
