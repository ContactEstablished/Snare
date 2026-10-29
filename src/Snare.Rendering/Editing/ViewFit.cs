namespace Snare.Rendering.Editing;

/// <summary>How the editor first shows an image: the zoom and the image's top-left in view pixels.</summary>
public static class ViewFit
{
    /// <summary>
    /// Fits the whole image, never above 100%. A very tall image (a scrolling capture) is fitted
    /// to the view's width instead and shown from its top, so its text stays readable; the wheel
    /// scrolls down it.
    /// </summary>
    public static (float Scale, float X, float Y) Fit(
        float viewWidth, float viewHeight, int imageWidth, int imageHeight, float margin, float minScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        var byWidth = (viewWidth - margin) / imageWidth;
        var byHeight = (viewHeight - margin) / imageHeight;
        var tall = imageHeight > 2L * imageWidth && byHeight < byWidth;

        var scale = Math.Max(minScale, Math.Min(1, tall ? byWidth : Math.Min(byWidth, byHeight)));
        var x = (viewWidth - (imageWidth * scale)) / 2;
        var y = tall ? margin / 2 : (viewHeight - (imageHeight * scale)) / 2;
        return (scale, x, y);
    }
}
