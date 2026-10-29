using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Snare.Core.Imaging;

namespace Snare.App.Services;

/// <summary>
/// Puts images on the clipboard as both a bitmap (for every app) and PNG data (for apps that
/// prefer it). Other apps briefly lock the clipboard; WPF retries for about a second, and this tries once more.
/// </summary>
public sealed class ClipboardService(ILogger<ClipboardService> logger)
{
    private const int Attempts = 2; // Clipboard.SetDataObject already retries internally
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>Copies captured pixels. Call on the UI thread.</summary>
    public Task<bool> CopyAsync(PixelBuffer image, byte[]? png = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Pixels, image.Stride);
        bitmap.Freeze();
        return SetAsync(bitmap, png);
    }

    /// <summary>Copies a file as a file drop (paste into Explorer, Slack, email). Call on the UI thread.</summary>
    public async Task<bool> CopyFileDropAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var data = new DataObject();
        data.SetFileDropList([path]);
        return await SetDataAsync(data);
    }

    /// <summary>Copies plain text (Grab Text). Call on the UI thread.</summary>
    public async Task<bool> CopyTextAsync(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        return await SetDataAsync(data);
    }

    /// <summary>Copies a saved PNG or JPG file. Call on the UI thread.</summary>
    public async Task<bool> CopyFileAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = await File.ReadAllBytesAsync(path);
        var frame = BitmapFrame.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        frame.Freeze();
        var isPng = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        return await SetAsync(frame, isPng ? bytes : null);
    }

    private async Task<bool> SetAsync(BitmapSource bitmap, byte[]? png)
    {
        var data = new DataObject();
        data.SetImage(bitmap);
        if (png is not null)
        {
            data.SetData("PNG", new MemoryStream(png), autoConvert: false);
        }

        return await SetDataAsync(data);
    }

    private async Task<bool> SetDataAsync(DataObject data)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return true;
            }
            catch (COMException ex) when (attempt < Attempts)
            {
                logger.LogDebug(ex, "Clipboard busy, attempt {Attempt}", attempt);
                await Task.Delay(RetryDelay);
            }
            catch (COMException ex)
            {
                logger.LogWarning(ex, "Could not copy to the clipboard");
            }
        }

        return false;
    }
}
