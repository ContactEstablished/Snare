namespace Snare.Recording.Ffmpeg;

/// <summary>
/// Finds ffmpeg.exe: the path in settings, then tools\ffmpeg next to the app, then PATH
/// (which finds a winget install). File checks and PATH are injected for tests.
/// </summary>
public sealed class FfmpegLocator(Func<string, bool> fileExists, string? pathVariable, string appDirectory)
{
    public const string ExeName = "ffmpeg.exe";

    public static FfmpegLocator ForThisMachine() =>
        new(File.Exists, Environment.GetEnvironmentVariable("PATH"), AppContext.BaseDirectory);

    public string? Find(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configured = Directory.Exists(configuredPath) ? Path.Combine(configuredPath, ExeName) : configuredPath;
            if (fileExists(configured))
            {
                return configured;
            }
        }

        var bundled = Path.Combine(appDirectory, "tools", "ffmpeg", ExeName);
        if (fileExists(bundled))
        {
            return bundled;
        }

        foreach (var folder in (pathVariable ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(folder.Trim('"'), ExeName);
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
