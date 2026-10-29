namespace Snare.Interop.Tests;

/// <summary>Reads the real display configuration; run with <c>Snare.Interop.Tests.exe -explicit only -diagnostics</c>.</summary>
public class AdvancedColorTests
{
    [Fact(Explicit = true)]
    public void CountHdrDisplays_ReadsTheDisplayConfiguration()
    {
        var count = AdvancedColor.CountHdrDisplays();

        TestContext.Current.SendDiagnosticMessage($"{count} display(s) with HDR on");
        Assert.InRange(count, 0, 16);
    }
}
