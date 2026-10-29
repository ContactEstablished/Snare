using Snare.Core.Annotations;

namespace Snare.Rendering.Tests;

/// <summary>Renders known annotation JSON over the sample image and compares with Golden/*.png.</summary>
public class GoldenRenderTests
{
    public static TheoryData<string, string> Cases => new()
    {
        {
            "arrow",
            """
            [{"type":"arrow","id":"a","from":{"x":30,"y":170},"to":{"x":150,"y":60},"stroke":"#E53935","strokeWidth":6},
             {"type":"arrow","id":"b","from":{"x":180,"y":170},"to":{"x":290,"y":60},"stroke":"#1E88E5","strokeWidth":4,"headStyle":"open","headSize":5}]
            """
        },
        {
            "line",
            """
            [{"type":"line","id":"a","from":{"x":20,"y":40},"to":{"x":300,"y":40},"stroke":"#E53935","strokeWidth":5},
             {"type":"line","id":"b","from":{"x":20,"y":120},"to":{"x":300,"y":180},"stroke":"#000000","strokeWidth":3,"dashed":true}]
            """
        },
        {
            "rectangle",
            """
            [{"type":"rectangle","id":"a","bounds":{"x":20,"y":20,"width":130,"height":80},"stroke":"#E53935","strokeWidth":4},
             {"type":"rectangle","id":"b","bounds":{"x":170,"y":90,"width":130,"height":90},"stroke":"#1E88E5","strokeWidth":3,"fill":"#80FFEB3B","cornerRadius":12}]
            """
        },
        {
            "ellipse",
            """
            [{"type":"ellipse","id":"a","bounds":{"x":30,"y":30,"width":150,"height":90},"stroke":"#43A047","strokeWidth":5},
             {"type":"ellipse","id":"b","bounds":{"x":190,"y":100,"width":100,"height":80},"stroke":"#000000","strokeWidth":0,"fill":"#AA8E24AA"}]
            """
        },
        {
            "pen",
            """
            [{"type":"pen","id":"a","points":[{"x":20,"y":150},{"x":60,"y":90},{"x":100,"y":140},{"x":140,"y":60},{"x":180,"y":130},{"x":220,"y":50},{"x":290,"y":110}],"stroke":"#8E24AA","strokeWidth":5}]
            """
        },
        {
            "highlighter",
            """
            [{"type":"highlighter","id":"a","points":[{"x":10,"y":40},{"x":300,"y":40}],"color":"#FFEB3B","width":16},
             {"type":"highlighter","id":"b","points":[{"x":10,"y":106},{"x":150,"y":106},{"x":300,"y":110}],"color":"#FF4081","width":16}]
            """
        },
        {
            "text",
            """
            [{"type":"text","id":"a","position":{"x":20,"y":20},"text":"Plain text","color":"#000000","fontSize":22},
             {"type":"text","id":"b","position":{"x":40,"y":90},"text":"Two lines\nwith a background","color":"#FFFFFF","fontSize":18,"background":"#DD1F1F1F","bold":true}]
            """
        },
        {
            "callout",
            """
            [{"type":"callout","id":"a","bounds":{"x":15,"y":15,"width":130,"height":50},"tail":{"x":40,"y":115},"text":"Click here","color":"#E53935","fontSize":18},
             {"type":"callout","id":"b","bounds":{"x":165,"y":20,"width":120,"height":60},"tail":{"x":312,"y":56},"text":"Two lines\nof text","color":"#000000","fontSize":16,"fill":"#FFF59D","strokeWidth":2,"bold":true},
             {"type":"callout","id":"c","bounds":{"x":60,"y":135,"width":170,"height":50},"tail":{"x":175,"y":95},"text":"Tail points up","color":"#FFFFFF","fontSize":18,"fill":"#263238"}]
            """
        },
        {
            "redact-solid",
            """
            [{"type":"redact","id":"a","bounds":{"x":40,"y":30,"width":150,"height":40},"mode":"solid","fill":"#000000"}]
            """
        },
        {
            "redact-blur",
            """
            [{"type":"redact","id":"a","bounds":{"x":40,"y":30,"width":200,"height":80},"mode":"blur","strength":6}]
            """
        },
        {
            "redact-pixelate",
            """
            [{"type":"redact","id":"a","bounds":{"x":40,"y":30,"width":200,"height":80},"mode":"pixelate","strength":10}]
            """
        },
        {
            "steps",
            """
            [{"type":"step","id":"a","center":{"x":60,"y":60},"fill":"#E53935"},
             {"type":"arrow","id":"x","from":{"x":80,"y":70},"to":{"x":140,"y":110},"stroke":"#E53935","strokeWidth":4},
             {"type":"step","id":"b","center":{"x":160,"y":120},"fill":"#E53935"},
             {"type":"step","id":"c","center":{"x":260,"y":60},"fill":"#1E88E5","radius":24}]
            """
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Tool_RendersLikeItsGolden(string name, string objectsJson)
    {
        var document = AnnotationJson.Deserialize(
            $$"""{"schemaVersion":1,"canvas":{"width":320,"height":200},"objects":{{objectsJson}}}""");
        using var original = BaseImages.Sample();

        using var rendered = new AnnotationRenderer().Render(original, document);

        Golden.AssertMatches(rendered, name);
    }

    [Fact]
    public void CropAndResize_RenderLikeTheirGolden()
    {
        var document = AnnotationJson.Deserialize("""
            {"schemaVersion":1,
             "canvas":{"width":320,"height":200,"crop":{"x":100,"y":40,"width":200,"height":120},"resize":{"width":100,"height":60}},
             "objects":[{"type":"arrow","id":"a","from":{"x":120,"y":140},"to":{"x":260,"y":70},"stroke":"#E53935","strokeWidth":6},
                        {"type":"step","id":"s","center":{"x":270,"y":60},"fill":"#1E88E5"}]}
            """);
        using var original = BaseImages.Sample();

        using var rendered = new AnnotationRenderer().Render(original, document);

        Assert.Equal((100, 60), (rendered.Width, rendered.Height));
        Golden.AssertMatches(rendered, "crop-resize");
    }
}
