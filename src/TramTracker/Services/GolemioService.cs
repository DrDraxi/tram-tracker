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
            // Reload config on each fetch to pick up changes without restart
            _settings.LoadConfig();

            if (string.IsNullOrEmpty(_apiKey))
            {
                UpdateState(TramState.Error("API key not configured"));
                return;
            }

            var trackingConfig = _settings.ActiveTrackingConfig;
            var url = BuildUrl(trackingConfig.StationName, _settings.Config.DepartureLimit);

            System.Diagnostics.Debug.WriteLine($"Fetching: {url}");

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                UpdateState(TramState.Error($"API error: {response.StatusCode}\n{url}"));
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize(json, AppJsonContext.Default.GolemioResponse);

            if (data?.Departures == null || data.Departures.Count == 0)
            {
                UpdateState(TramState.Error("No departures found"));
                return;
            }

            // Find matching departures based on config
            var matchingDepartures = FindMatchingDepartures(data.Departures, trackingConfig).ToList();

            if (matchingDepartures.Count == 0)
            {
                // Build helpful error message showing what we're looking for
                var looking = new List<string>();
                if (!string.IsNullOrEmpty(trackingConfig.LineNumber))
                    looking.Add($"line {trackingConfig.LineNumber}");
                if (!string.IsNullOrEmpty(trackingConfig.Direction))
                    looking.Add($"dir '{trackingConfig.Direction}'");

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

            var currentDeparture = matchingDepartures[0];
            var nextDeparture = matchingDepartures.Count > 1 ? matchingDepartures[1] : null;

            var state = TramState.FromDeparture(currentDeparture, nextDeparture);
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

    private string BuildUrl(string stationName, int departureLimit)
    {
        var builder = new UriBuilder(BaseUrl);
        var query = HttpUtility.ParseQueryString(string.Empty);

        query["names"] = stationName;
        query["limit"] = departureLimit.ToString();
        query["preferredTimezone"] = "Europe/Prague";

        builder.Query = query.ToString();
        return builder.ToString();
    }

    private IEnumerable<Departure> FindMatchingDepartures(List<Departure> departures, TrackingConfig trackingConfig)
    {
        IEnumerable<Departure> filtered = departures;

        // Filter by line number if specified
        if (!string.IsNullOrEmpty(trackingConfig.LineNumber))
        {
            filtered = filtered.Where(d =>
                d.Route?.ShortName?.Equals(trackingConfig.LineNumber, StringComparison.OrdinalIgnoreCase) == true);
        }

        // Filter by direction (headsign) if specified
        if (!string.IsNullOrEmpty(trackingConfig.Direction))
        {
            filtered = filtered.Where(d =>
                d.Trip?.Headsign?.Contains(trackingConfig.Direction, StringComparison.OrdinalIgnoreCase) == true);
        }

        // Return all matching departures (already sorted by time from API)
        return filtered;
    }

    private void UpdateState(TramState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(this, state);
    }
}
