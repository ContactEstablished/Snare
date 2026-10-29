using Snare.Core.Hotkeys;

namespace Snare.Core.Tests;

public class HotkeyConflictDiagnosisTests
{
    [Fact]
    public void RunningSnagit_IsBlamed()
    {
        var conflict = HotkeyConflictDiagnosis.Diagnose(
            "PrtSc", isPlainPrintScreen: true, ["explorer", "SnagitCapture"], snippingToolOwnsPrintScreen: true);

        Assert.Equal(HotkeyOwner.Snagit, conflict.LikelyOwner);
        Assert.Equal("PrtSc is taken by Snagit", conflict.Summary);
        Assert.Contains("Snagit", conflict.Advice);
    }

    [Fact]
    public void SnippingToolSetting_IsBlamedForPlainPrintScreen()
    {
        var conflict = HotkeyConflictDiagnosis.Diagnose(
            "PrtSc", isPlainPrintScreen: true, ["explorer"], snippingToolOwnsPrintScreen: true);

        Assert.Equal(HotkeyOwner.SnippingTool, conflict.LikelyOwner);
        Assert.Contains("Use the Print screen key to open screen capture", conflict.Advice);
    }

    [Fact]
    public void SnippingToolSetting_IsNotBlamedForModifiedPrintScreen()
    {
        var conflict = HotkeyConflictDiagnosis.Diagnose(
            "Shift+PrtSc", isPlainPrintScreen: false, ["explorer"], snippingToolOwnsPrintScreen: true);

        Assert.Equal(HotkeyOwner.Unknown, conflict.LikelyOwner);
        Assert.Equal("Shift+PrtSc is taken by another app", conflict.Summary);
    }

    [Fact]
    public void ProcessNameMatch_IgnoresCase()
    {
        var conflict = HotkeyConflictDiagnosis.Diagnose(
            "PrtSc", isPlainPrintScreen: true, ["snagiteditor"], snippingToolOwnsPrintScreen: false);

        Assert.Equal(HotkeyOwner.Snagit, conflict.LikelyOwner);
    }
}
