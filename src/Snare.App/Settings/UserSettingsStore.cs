using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Snare.Core.Options;
using Snare.Library;

namespace Snare.App.Settings;

/// <summary>
/// Matt's settings, in %LOCALAPPDATA%\Snare\settings.json: the whole "Snare" section, layered
/// over appsettings.json (with reload on change), so a save reaches IOptionsMonitor at once.
/// </summary>
public sealed class UserSettingsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath => Path.Combine(LibraryPaths.DefaultAppDataRoot, "settings.json");

    public string FilePath { get; } = path;

    /// <summary>Writes every setting, replacing the file atomically so a reload never sees half of it.</summary>
    public void Save(SnareOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(new Dictionary<string, SnareOptions> { [SnareOptions.SectionName] = options }, Json);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }

    /// <summary>A deep copy to edit, so a cancelled Settings window never changes the live options.</summary>
    public static SnareOptions Copy(SnareOptions options) =>
        JsonSerializer.Deserialize<SnareOptions>(JsonSerializer.Serialize(options, Json), Json)!;
}
