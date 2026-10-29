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

    /// <summary>
    /// Writes every setting, replacing the file atomically so a reload never sees half of it. The
    /// temp file is written through to disk first, so a power cut can't leave an empty settings file.
    /// </summary>
    public async Task SaveAsync(SnareOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, new Dictionary<string, SnareOptions> { [SnareOptions.SectionName] = options }, Json, cancellationToken);
        }

        File.Move(temp, FilePath, overwrite: true);
    }

    /// <summary>A deep copy to edit, so a cancelled Settings window never changes the live options.</summary>
    public static SnareOptions Copy(SnareOptions options) =>
        JsonSerializer.Deserialize<SnareOptions>(JsonSerializer.Serialize(options, Json), Json)!;
}
