namespace TramTracker.Services;

public static class EnvService
{
    private static readonly Dictionary<string, string> _variables = new();
    private static bool _loaded = false;

    static EnvService()
    {
        Load();
    }

    public static void Load()
    {
        if (_loaded) return;

        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            // Development: look in project root
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env")
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                LoadFromFile(path);
                _loaded = true;
                return;
            }
        }

        System.Diagnostics.Debug.WriteLine("Warning: No .env file found");
    }

    private static void LoadFromFile(string path)
    {
        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var equalsIndex = trimmed.IndexOf('=');
                if (equalsIndex > 0)
                {
                    var key = trimmed[..equalsIndex].Trim();
                    var value = trimmed[(equalsIndex + 1)..].Trim();

                    // Remove surrounding quotes if present
                    if ((value.StartsWith('"') && value.EndsWith('"')) ||
                        (value.StartsWith('\'') && value.EndsWith('\'')))
                    {
                        value = value[1..^1];
                    }

                    _variables[key] = value;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Loaded {_variables.Count} variables from .env");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading .env: {ex.Message}");
        }
    }

    public static string? Get(string key)
    {
        // First check our loaded variables
        if (_variables.TryGetValue(key, out var value))
            return value;

        // Fall back to environment variable
        return Environment.GetEnvironmentVariable(key);
    }

    public static string GetRequired(string key)
    {
        var value = Get(key);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required environment variable '{key}' is not set");
        }
        return value;
    }
}
