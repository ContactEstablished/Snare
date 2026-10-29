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

    /// <summary>
    /// The export path for a capture, with _2, _3 and so on appended when another capture
    /// from the same second already has the name.
    /// </summary>
    public string UniqueExportPathFor(DateTimeOffset capturedAt, ExportFormat format, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);

        var path = ExportPathFor(capturedAt, format);
        var folder = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var n = 2; exists(path); n++)
        {
            path = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{stem}_{n}{extension}"));
        }

        return path;
    }

    /// <summary>A recording's path, named like a still capture but .mp4, with the same _n suffixes.</summary>
    public string UniqueVideoPathFor(DateTimeOffset capturedAt, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var png = UniqueExportPathFor(capturedAt, ExportFormat.Png, p => exists(Path.ChangeExtension(p, ".mp4")));
        return Path.ChangeExtension(png, ".mp4");
    }

    public string OriginalPathFor(Guid id) => Path.Combine(OriginalsFolder, $"{id:N}.png");

    public string ThumbnailPathFor(Guid id) => Path.Combine(ThumbnailsFolder, $"{id:N}.webp");

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
