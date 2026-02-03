using System.Net.Http;
using System.Text.Json;
using System.Web;
using TramTracker.Models;

namespace TramTracker.Services;

public class GolemioService : IGolemioService
{
    private const string BaseUrl = "https://api.golemio.cz/v2/pid/departureboards/";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settings;
    private readonly string _apiKey;

    public TramState CurrentState { get; private set; } = new();
    public event EventHandler<TramState>? StateChanged;

    public GolemioService(SettingsService settings)
    {
        _settings = settings;
        _apiKey = EnvService.Get("GOLEMIO_API_KEY") ?? settings.Config.ApiKey ?? "";

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-access-token", _apiKey);
    }

    public async Task FetchDeparturesAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                UpdateState(TramState.Error("API key not configured"));
                return;
            }

            var config = _settings.Config;
            var url = BuildUrl(config);

            System.Diagnostics.Debug.WriteLine($"Fetching: {url}");

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                UpdateState(TramState.Error($"API error: {response.StatusCode}"));
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<GolemioResponse>(json);

            if (data?.Departures == null || data.Departures.Count == 0)
            {
                UpdateState(TramState.Error("No departures found"));
                return;
            }

            // Find the first matching departure based on config
            var departure = FindMatchingDeparture(data.Departures, config);

            if (departure == null)
            {
                // Build helpful error message showing what we're looking for
                var looking = new List<string>();
                if (!string.IsNullOrEmpty(config.LineNumber))
                    looking.Add($"line {config.LineNumber}");
                if (!string.IsNullOrEmpty(config.Direction))
                    looking.Add($"dir '{config.Direction}'");

                var available = data.Departures
                    .Select(d => $"{d.Route?.ShortName}→{d.Trip?.Headsign}")
                    .Distinct()
                    .Take(5);

                var msg = looking.Count > 0
                    ? $"No match for {string.Join(", ", looking)}\nAvailable: {string.Join(", ", available)}"
                    : "No departures";

                UpdateState(TramState.Error(msg));
                return;
            }

            var state = TramState.FromDeparture(departure);
            UpdateState(state);
        }
        catch (HttpRequestException ex)
        {
            UpdateState(TramState.Error($"Network error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            UpdateState(TramState.Error($"Error: {ex.Message}"));
        }
    }

    private string BuildUrl(AppConfig config)
    {
        var builder = new UriBuilder(BaseUrl);
        var query = HttpUtility.ParseQueryString(string.Empty);

        query["names"] = config.StationName;
        query["limit"] = config.DepartureLimit.ToString();
        query["preferredTimezone"] = "Europe/Prague";

        builder.Query = query.ToString();
        return builder.ToString();
    }

    private Departure? FindMatchingDeparture(List<Departure> departures, AppConfig config)
    {
        IEnumerable<Departure> filtered = departures;

        // Filter by line number if specified
        if (!string.IsNullOrEmpty(config.LineNumber))
        {
            filtered = filtered.Where(d =>
                d.Route?.ShortName?.Equals(config.LineNumber, StringComparison.OrdinalIgnoreCase) == true);
        }

        // Filter by direction (headsign) if specified
        if (!string.IsNullOrEmpty(config.Direction))
        {
            filtered = filtered.Where(d =>
                d.Trip?.Headsign?.Contains(config.Direction, StringComparison.OrdinalIgnoreCase) == true);
        }

        // Return the first matching departure (already sorted by time from API)
        return filtered.FirstOrDefault();
    }

    private void UpdateState(TramState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(this, state);
    }
}
