using Snare.Core.Geometry;

namespace Snare.Capture.Tests;

public class DesktopCropperTests
{
    [Fact]
    public void Crop_InsideOneMonitor_CopiesItsPixels()
    {
        var crop = DesktopCropper.Crop(TestDesktops.Stacked(), new PixelRect(5, 5, 10, 4));

        Assert.Equal((10, 4), (crop.Width, crop.Height));
        Assert.Equal(TestDesktops.PrimaryColor, crop.GetArgb(0, 0));
        Assert.Equal(TestDesktops.PrimaryColor, crop.GetArgb(9, 3));
    }

    [Fact]
    public void Crop_AcrossMonitors_JoinsThemAndBlacksOutTheGap()
    {
        // From (10, -5) to (30, 5): left of the upper monitor is uncovered, then upper, then primary.
        var crop = DesktopCropper.Crop(TestDesktops.Stacked(), new PixelRect(10, -5, 20, 10));

        Assert.Equal(VirtualDesktopComposer.UncoveredArgb, crop.GetArgb(0, 0));   // (10, -5): no monitor
        Assert.Equal(TestDesktops.UpperColor, crop.GetArgb(10, 0));               // (20, -5): upper
        Assert.Equal(TestDesktops.PrimaryColor, crop.GetArgb(0, 5));              // (10, 0): primary
        Assert.Equal(TestDesktops.PrimaryColor, crop.GetArgb(19, 9));
    }

    [Fact]
    public void Crop_ClipsToTheDesktop()
    {
        var crop = DesktopCropper.Crop(TestDesktops.Stacked(), new PixelRect(90, 30, 40, 20));

        Assert.Equal((10, 10), (crop.Width, crop.Height));
    }

    [Fact]
    public void Crop_OutsideTheDesktop_Throws()
    {
        Assert.Throws<ArgumentException>(() => DesktopCropper.Crop(TestDesktops.Stacked(), new PixelRect(500, 500, 10, 10)));
    }

    [Fact]
    public void Crop_MatchesTheComposedDesktop()
    {
        var desktop = TestDesktops.Stacked();
        var composed = VirtualDesktopComposer.Compose(desktop);

        var crop = DesktopCropper.Crop(desktop, desktop.Bounds);

        Assert.Equal(composed.Pixels, crop.Pixels);
    }
}
