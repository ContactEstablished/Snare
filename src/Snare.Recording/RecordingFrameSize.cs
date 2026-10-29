using Snare.Core.Geometry;

namespace Snare.Recording;

/// <summary>Output size rules for H.264 recordings.</summary>
public static class RecordingFrameSize
{
    /// <summary>NVENC H.264 can't encode frames wider than this.</summary>
    public const int MaxH264Width = 4096;

    /// <summary>
    /// Scales a recording area down to fit the encoder's width limit, keeping the aspect ratio.
    /// H.264 needs even dimensions, so both are rounded down to even. Matt's 5120x1440 monitor
    /// records at 4096x1152.
    /// </summary>
    public static PixelSize Fit(PixelSize source, int maxWidth = MaxH264Width)
    {
        if (source.Width < 2 || source.Height < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "A recording must be at least 2x2 pixels.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxWidth, 2);

        if (source.Width <= maxWidth)
        {
            return new PixelSize(Even(source.Width), Even(source.Height));
        }

        var height = (int)Math.Round((double)source.Height * maxWidth / source.Width);
        return new PixelSize(Even(maxWidth), Math.Max(2, Even(height)));
    }

    private static int Even(int value) => value & ~1;
}
