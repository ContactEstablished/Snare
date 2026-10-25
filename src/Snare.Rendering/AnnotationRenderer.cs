using SkiaSharp;
using Snare.Core.Annotations;

namespace Snare.Rendering;

/// <summary>Flattens an annotation document onto a copy of the original capture.</summary>
public sealed class AnnotationRenderer
{
    public SKBitmap Render(SKBitmap original, AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(document);

        var result = original.Copy()
            ?? throw new InvalidOperationException("Could not copy the original bitmap.");

        // Phase 2: draw document.Objects in z-order onto result, with golden-image tests per tool.
        return result;
    }
}
