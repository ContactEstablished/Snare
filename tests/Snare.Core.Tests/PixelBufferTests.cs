using Snare.Core.Imaging;

namespace Snare.Core.Tests;

public class PixelBufferTests
{
    [Fact]
    public void Filled_StoresBgraBytes()
    {
        var buffer = PixelBuffer.Filled(1, 1, 0xFF112233);

        Assert.Equal(new byte[] { 0x33, 0x22, 0x11, 0xFF }, buffer.Pixels);
        Assert.Equal(0xFF112233u, buffer.GetArgb(0, 0));
    }

    [Fact]
    public void Constructor_RejectsTheWrongByteCount()
    {
        Assert.Throws<ArgumentException>(() => new PixelBuffer(2, 2, new byte[15]));
    }

    [Fact]
    public void ByteCount_RejectsSizesThatOverflowAnArray()
    {
        Assert.Equal(5120 * 2880 * 4, PixelBuffer.ByteCount(5120, 2880));
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelBuffer.ByteCount(40_000, 40_000));
    }

    [Fact]
    public void GetArgb_RejectsPointsOutsideTheImage()
    {
        var buffer = PixelBuffer.Filled(2, 2, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetArgb(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetArgb(0, -1));
    }
}
