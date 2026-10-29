using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Snare.Library;
using Snare.Rendering.Editing;

namespace Snare.App.Editor;

/// <summary>Remembers each tool's last style across sessions, in %LOCALAPPDATA%\Snare\editor-styles.json.</summary>
public sealed class EditorStyleStore(LibraryPaths paths, ILogger<EditorStyleStore> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private string FilePath => Path.Combine(paths.AppDataRoot, "editor-styles.json");

    public IReadOnlyDictionary<EditorTool, ToolStyle> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Dictionary<EditorTool, ToolStyle>>(File.ReadAllText(FilePath), Json) ?? [];
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Ignoring unreadable editor styles at {Path}", FilePath);
        }

        return new Dictionary<EditorTool, ToolStyle>();
    }

    public void Save(IReadOnlyDictionary<EditorTool, ToolStyle> styles)
    {
        try
        {
            Directory.CreateDirectory(paths.AppDataRoot);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(styles, Json));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not save editor styles to {Path}", FilePath);
        }
    }
}
