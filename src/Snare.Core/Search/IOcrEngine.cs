using Snare.Core.Imaging;

namespace Snare.Core.Search;

/// <summary>Reads the text in an image. Lines are separated by '\n'; no text is an empty string.</summary>
public interface IOcrEngine
{
    Task<string> RecognizeAsync(PixelBuffer image, CancellationToken cancellationToken = default);
}
