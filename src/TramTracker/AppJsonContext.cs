using System.Text.Json.Serialization;
using TramTracker.Models;

namespace TramTracker;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(GolemioResponse))]
internal partial class AppJsonContext : JsonSerializerContext { }
