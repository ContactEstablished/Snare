namespace Snare.Interop.Tests;

public class ScrollTargetTests
{
    /// <summary>
    /// The UI Automation spike, run by hand: open a long page in Chrome or Edge, start this, and
    /// within 5 seconds rest the mouse over the page. It scrolls three steps and prints the method
    /// ("scroll pattern" means UI Automation worked; "mouse wheel" means the fallback was used).
    ///   Snare.Interop.Tests.exe -explicit only -method "*ScrollTarget*" -diagnostics
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Spike_ScrollsWhatIsUnderTheMouse()
    {
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var (x, y) = WindowPlacement.GetCursorPosition();
        var target = ScrollTarget.AtPoint(x, y, areaHeight: 800);
        TestContext.Current.SendDiagnosticMessage($"Under ({x}, {y}): {target.Method}");

        for (var i = 0; i < 3; i++)
        {
            target.ScrollDown();
            await Task.Delay(700, TestContext.Current.CancellationToken);
        }

        TestContext.Current.SendDiagnosticMessage($"After three steps: {target.Method}");
    }
}
