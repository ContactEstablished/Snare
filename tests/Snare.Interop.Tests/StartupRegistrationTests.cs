using Microsoft.Win32;

namespace Snare.Interop.Tests;

public sealed class StartupRegistrationTests : IDisposable
{
    // A throwaway key, never the real Run key.
    private readonly string _keyPath = $@"Software\Snare.Tests\Run-{Guid.NewGuid():N}";

    [Fact]
    public void EnableDisable_WritesAndRemovesTheQuotedExePath()
    {
        var startup = new StartupRegistration(_keyPath);
        Assert.Null(startup.Current());

        startup.Enable(@"C:\Apps\Snare\Snare.exe");
        Assert.Equal(@"""C:\Apps\Snare\Snare.exe""", startup.Current());
        Assert.True(startup.IsEnabledFor(@"c:\apps\snare\snare.exe"));
        Assert.False(startup.IsEnabledFor(@"C:\Elsewhere\Snare.exe"));

        startup.Disable();
        Assert.Null(startup.Current());
        startup.Disable(); // already gone: no error
    }

    [Theory]
    [InlineData("PrtSc", 0x2Cu)]
    [InlineData("A", 0x41u)]
    [InlineData("Z", 0x5Au)]
    [InlineData("0", 0x30u)]
    [InlineData("F1", 0x70u)]
    [InlineData("F24", 0x87u)]
    public void KeyNames_MapToVirtualKeys(string key, uint expected)
    {
        Assert.Equal(expected, GlobalHotkeys.VirtualKeyFor(key));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("F25")]
    [InlineData("Space")]
    public void UnknownKeyNames_HaveNoVirtualKey(string key)
    {
        Assert.Null(GlobalHotkeys.VirtualKeyFor(key));
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\Snare.Tests", throwOnMissingSubKey: false);
}
