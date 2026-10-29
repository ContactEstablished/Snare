namespace Snare.Recording.Ffmpeg;

/// <summary>FFmpeg exited with an error; the message ends with the last lines it printed.</summary>
public sealed class FfmpegException : Exception
{
    public FfmpegException()
    {
    }

    public FfmpegException(string message)
        : base(message)
    {
    }

    public FfmpegException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
