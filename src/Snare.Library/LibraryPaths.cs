using System.Globalization;
using Snare.Core.Options;

namespace Snare.Library;

/// <summary>
/// Where everything lives. Exports go under the library root (visible, shareable);
/// the database, originals and thumbnails stay private under %LOCALAPPDATA%\Snare.
/// </summary>
public sealed class LibraryPaths
{
    public LibraryPaths(string libraryRoot, string appDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataRoot);
        LibraryRoot = libraryRoot;
        AppDataRoot = appDataRoot;
    }

    public string LibraryRoot { get; }

    public string AppDataRoot { get; }

    public string DatabasePath => Path.Combine(AppDataRoot, "library.db");

    public string OriginalsFolder => Path.Combine(AppDataRoot, "originals");

    public string ThumbnailsFolder => Path.Combine(AppDataRoot, "thumbs");

    public string LogsFolder => Path.Combine(AppDataRoot, "logs");

    /// <summary>Export path such as &lt;root&gt;\2026\10\2026-10-25_182301.png, in the capture's own local time.</summary>
    public string ExportPathFor(DateTimeOffset capturedAt, ExportFormat format)
    {
        var extension = format == ExportFormat.Jpeg ? "jpg" : "png";
        var year = capturedAt.ToString("yyyy", CultureInfo.InvariantCulture);
        var month = capturedAt.ToString("MM", CultureInfo.InvariantCulture);
        var stem = capturedAt.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(LibraryRoot, year, month, $"{stem}.{extension}");
    }

    public static LibraryPaths FromOptions(SnareOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var libraryRoot = string.IsNullOrWhiteSpace(options.LibraryRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Snare")
            : options.LibraryRoot;
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snare");

        return new LibraryPaths(libraryRoot, appDataRoot);
    }
}
