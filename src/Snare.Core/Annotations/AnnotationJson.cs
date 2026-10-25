using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snare.Core.Annotations;

/// <summary>The one place annotation JSON is read and written, so the format stays consistent.</summary>
public static class AnnotationJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static AnnotationDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<AnnotationDocument>(json, Options)
            ?? throw new JsonException("Annotation JSON deserialized to null.");
    }
}
