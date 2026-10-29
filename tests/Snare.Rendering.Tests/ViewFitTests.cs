using Snare.Rendering.Editing;

namespace Snare.Rendering.Tests;

public class ViewFitTests
{
    [Fact]
    public void AScreenshot_FitsWhole_Centered()
    {
        var (scale, x, y) = ViewFit.Fit(1032, 632, 2000, 1000, margin: 32, minScale: 0.05f);

        Assert.Equal(0.5f, scale);
        Assert.Equal((16f, 66f), (x, y));
    }

    [Fact]
    public void ASmallImage_IsNeverUpscaled()
    {
        Assert.Equal(1f, ViewFit.Fit(1000, 800, 200, 100, 32, 0.05f).Scale);
    }

    [Fact]
    public void AScrollingCapture_FitsTheWidth_FromTheTop()
    {
        var (scale, x, y) = ViewFit.Fit(1032, 832, 2000, 30000, margin: 32, minScale: 0.05f);

        Assert.Equal(0.5f, scale);
        Assert.Equal((16f, 16f), (x, y));
    }

    [Fact]
    public void ANarrowTallCapture_StaysAtActualSize()
    {
        var (scale, x, _) = ViewFit.Fit(1032, 832, 800, 9000, margin: 32, minScale: 0.05f);

        Assert.Equal(1f, scale);
        Assert.Equal(116f, x);
    }
}
