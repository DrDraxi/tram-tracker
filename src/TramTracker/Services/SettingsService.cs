using System.Text.Json;
using TramTracker.Models;

namespace TramTracker.Services;

public class SettingsService
{
    private readonly string _dataFolder;
    private readonly string _configFilePath;

    public AppConfig Config { get; private set; } = new();

    /// <summary>
    /// Gets the currently active tracking configuration based on time windows.
    /// This is evaluated fresh on each call, no caching needed.
    /// </summary>
    public TrackingConfig ActiveTrackingConfig => Config.GetActiveTrackingConfig();

    public SettingsService()
    {
        _dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TramTracker");
        _configFilePath = Path.Combine(_dataFolder, "config.json");

        EnsureDataFolder();
        LoadConfig();
    }

    private void EnsureDataFolder()
    {
        if (!Directory.Exists(_dataFolder))
        {
            Directory.CreateDirectory(_dataFolder);
        }
    }

    public void LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);

                if (config != null)
                {
                    ValidateConfig(config);
                    Config = config;
                }
                else
                {
                    Config = new AppConfig();
                }
            }
            else
            {
                Config = new AppConfig();
                SaveConfig();
            }
        }
        catch
        {
            Config = new AppConfig();
        }
    }

    public void SaveConfig()
    {
        try
        {
            EnsureDataFolder();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Config, options);
            File.WriteAllText(_configFilePath, json);
        }
        catch { }
    }

    /// <summary>
    /// Validates time window configurations and logs warnings for invalid entries.
    /// Invalid windows are kept but will never match (IsActive returns false).
    /// </summary>
    private void ValidateConfig(AppConfig config)
    {
        if (!config.IsTimeBasedConfig())
            return;

        for (int i = 0; i < config.TimeWindows!.Count; i++)
        {
            var window = config.TimeWindows[i];

            if (window.GetStartTime() == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Warning: TimeWindows[{i}] has invalid StartTime: '{window.StartTime}'");
            }

            if (window.GetEndTime() == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Warning: TimeWindows[{i}] has invalid EndTime: '{window.EndTime}'");
            }

            if (string.IsNullOrEmpty(window.StationName))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Warning: TimeWindows[{i}] has empty StationName");
            }
        }

        // Validate DefaultConfig exists for time-based configs
        if (config.DefaultConfig == null)
        {
            System.Diagnostics.Debug.WriteLine(
                "Warning: Time-based config should have DefaultConfig for fallback");
        }
    }
}
