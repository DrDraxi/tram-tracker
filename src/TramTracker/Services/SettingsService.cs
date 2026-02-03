using System.Text.Json;
using TramTracker.Models;

namespace TramTracker.Services;

public class SettingsService
{
    private readonly string _dataFolder;
    private readonly string _configFilePath;

    public AppConfig Config { get; private set; } = new();

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
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
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
}
