using Microsoft.Extensions.DependencyInjection;
using Snare.Core.Recording;
using Snare.Recording.Ffmpeg;

namespace Snare.Recording;

public static class RecordingServiceCollectionExtensions
{
    /// <summary>Registers the screen recorder and the FFmpeg video tools.</summary>
    public static IServiceCollection AddSnareRecording(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<RecordingSpike>();
        services.AddSingleton<IScreenRecorder, ScreenRecorderLibRecorder>();
        services.AddSingleton(_ => FfmpegLocator.ForThisMachine());
        services.AddSingleton<VideoTools>();
        return services;
    }
}
