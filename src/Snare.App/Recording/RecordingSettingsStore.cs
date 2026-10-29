using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Snare.Core.Recording;
using Snare.Library;

namespace Snare.App.Recording;

/// <summary>Remembers the last recording choices, in %LOCALAPPDATA%\Snare\recording.json.</summary>
public sealed class RecordingSettingsStore(LibraryPaths paths, ILogger<RecordingSettingsStore> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private string FilePath => Path.Combine(paths.AppDataRoot, "recording.json");

    /// <summary>The saved settings, or null the first time.</summary>
    public RecordingSettings? Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<RecordingSettings>(File.ReadAllText(FilePath), Json)?.Normalized();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Ignoring unreadable recording settings at {Path}", FilePath);
        }

        return null;
    }

    public void Save(RecordingSettings settings)
    {
        try
        {
            Directory.CreateDirectory(paths.AppDataRoot);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Json));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not save recording settings to {Path}", FilePath);
        }
    }
}
