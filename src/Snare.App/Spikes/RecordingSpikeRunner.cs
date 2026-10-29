using System.IO;
using Microsoft.Extensions.Logging;
using Snare.App.Services;
using Snare.Library;
using Snare.Recording;

namespace Snare.App.Spikes;

/// <summary>Phase 0 spike: runs the scripted recording from the tray and reports the result there.</summary>
public sealed class RecordingSpikeRunner(
    RecordingSpike spike,
    LibraryPaths paths,
    TrayIconService tray,
    ILogger<RecordingSpikeRunner> logger)
{
    private bool _running;

    public async void Run()
    {
        // async void: started from a menu click; every failure is caught and reported below.
        if (_running)
        {
            return;
        }

        _running = true;
        try
        {
            tray.Notify("Recording spike", "Recording for about 22 seconds. Talk, play audio and click around.");
            var path = await spike.RunAsync(Path.Combine(paths.AppDataRoot, "spikes"));
            tray.Notify("Recording spike done", path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recording spike failed");
            tray.Notify("Recording spike failed", ex.Message);
        }
        finally
        {
            _running = false;
        }
    }
}
