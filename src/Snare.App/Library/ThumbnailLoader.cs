using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snare.Rendering;

namespace Snare.App.Library;

/// <summary>
/// Decodes WebP thumbnails with SkiaSharp rather than WIC, so the grid works whether or not
/// Windows has a WebP codec installed.
/// </summary>
public static class ThumbnailLoader
{
    /// <summary>Loads a thumbnail off the UI thread; returns a frozen bitmap, or null if it can't be read.</summary>
    public static Task<ImageSource?> LoadAsync(string path) => Task.Run<ImageSource?>(() =>
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var bitmap = ImageCodec.Decode(File.ReadAllBytes(path));
            var source = BitmapSource.Create(
                bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null, bitmap.Bytes, bitmap.RowBytes);
            source.Freeze();
            return source;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return null;
        }
    });
}
