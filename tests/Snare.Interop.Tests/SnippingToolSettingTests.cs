namespace Snare.Interop.Tests;

public class SnippingToolSettingTests
{
    [Fact]
    public void MissingValue_CountsAsOn() => Assert.True(SnippingToolSetting.IsEnabled(null));

    [Fact]
    public void One_IsOn() => Assert.True(SnippingToolSetting.IsEnabled(1));

    [Fact]
    public void Zero_IsOff() => Assert.False(SnippingToolSetting.IsEnabled(0));

    [Fact]
    public void UnexpectedType_CountsAsOn() => Assert.True(SnippingToolSetting.IsEnabled("0"));
}
