namespace Snare.Interop.Tests;

public sealed class PackagedStartupTests
{
    [Fact]
    public void TheTestRunner_IsNotPackaged()
    {
        // Unpackaged copies (dotnet run, publish.ps1) fall back to the Run key.
        Assert.False(PackagedStartup.IsPackaged);
    }
}
