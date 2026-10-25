using System.IO;
using Serilog;

namespace Snare.App.Infrastructure;

internal static class SnareLogging
{
    /// <summary>Rolling daily log in %LOCALAPPDATA%\Snare\logs, kept for 14 days.</summary>
    public static Serilog.ILogger CreateLogger()
    {
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snare", "logs");

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logFolder, "snare-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();
    }
}
