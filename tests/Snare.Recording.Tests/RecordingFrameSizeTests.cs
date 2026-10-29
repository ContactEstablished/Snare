using Snare.Core.Geometry;

namespace Snare.Recording.Tests;

public class RecordingFrameSizeTests
{
    [Fact]
    public void MattsPrimaryMonitor_ScalesTo4096Wide()
    {
        Assert.Equal(new PixelSize(4096, 1152), RecordingFrameSize.Fit(new PixelSize(5120, 1440)));
    }

    [Fact]
    public void MattsUpperMonitor_KeepsNativeSize()
    {
        Assert.Equal(new PixelSize(3440, 1440), RecordingFrameSize.Fit(new PixelSize(3440, 1440)));
    }

    [Fact]
    public void ExactlyTheLimit_IsUnchanged()
    {
        Assert.Equal(new PixelSize(4096, 2160), RecordingFrameSize.Fit(new PixelSize(4096, 2160)));
    }

    [Fact]
    public void OddDimensions_RoundDownToEven()
    {
        Assert.Equal(new PixelSize(1000, 500), RecordingFrameSize.Fit(new PixelSize(1001, 501)));
    }

    [Fact]
    public void ScaledHeight_IsEvenAndKeepsTheAspectRatio()
    {
        // 5000x1235 -> 4096 x 1011.7 -> 1012 -> even 1012.
        Assert.Equal(new PixelSize(4096, 1012), RecordingFrameSize.Fit(new PixelSize(5000, 1235)));
        // A region spanning both stacked monitors: 5120x2880 -> 4096x2304.
        Assert.Equal(new PixelSize(4096, 2304), RecordingFrameSize.Fit(new PixelSize(5120, 2880)));
    }

    [Fact]
    public void TinyAreas_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordingFrameSize.Fit(new PixelSize(1, 100)));
    }
}
