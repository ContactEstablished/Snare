using Snare.Core.Hotkeys;
using Snare.Core.Options;

namespace Snare.Core.Tests;

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("PrtSc", "PrtSc")]
    [InlineData("ctrl+shift+prtsc", "Ctrl+Shift+PrtSc")]
    [InlineData("Shift + Ctrl + PrintScreen", "Ctrl+Shift+PrtSc")]
    [InlineData("Alt+Win+F12", "Alt+Win+F12")]
    [InlineData("Ctrl+s", "Ctrl+S")]
    [InlineData("Control+Alt+7", "Ctrl+Alt+7")]
    public void Parses_AndFormatsCanonically(string text, string expected)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture));
        Assert.Equal(expected, gesture.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Space")]
    [InlineData("F25")]
    [InlineData("F0")]
    [InlineData("Hyper+PrtSc")]
    public void RejectsWhatIsntAHotkey(string text)
    {
        Assert.False(HotkeyGesture.TryParse(text, out _));
    }

    [Fact]
    public void LettersAndDigitsNeedCtrlOrAlt()
    {
        Assert.NotNull(Parse("Shift+S").Problem());
        Assert.NotNull(Parse("Win+5").Problem());
        Assert.Null(Parse("Alt+S").Problem());
        Assert.Null(Parse("F9").Problem());
        Assert.Null(Parse("PrtSc").Problem());
        Assert.True(Parse("PrtSc").IsPlainPrintScreen);
        Assert.False(Parse("Ctrl+PrtSc").IsPlainPrintScreen);
    }

    [Fact]
    public void Defaults_AreTheDecidedKeys_AndValid()
    {
        var options = new HotkeyOptions();

        Assert.Empty(options.Problems());
        Assert.Equal(
            ["PrtSc", "Shift+PrtSc", "Ctrl+Shift+PrtSc", "Ctrl+PrtSc", "Ctrl+Alt+PrtSc"],
            options.Bindings().Select(b => b.Gesture.ToString()));
    }

    [Fact]
    public void Problems_FindDuplicatesTypingKeysAndNonsense()
    {
        var options = new HotkeyOptions { RecordVideo = "prtsc", GrabText = "T", RepeatLastRegion = "Ctrl+Nope" };

        var problems = options.Problems();

        Assert.Contains(problems, p => p.Contains("both Capture and Record video", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("fire while typing", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("Ctrl+Nope", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEmptyGesture_TurnsThatHotkeyOff()
    {
        var options = new HotkeyOptions();
        options.Set(HotkeyAction.GrabText, "");

        Assert.DoesNotContain(options.Bindings(), b => b.Action == HotkeyAction.GrabText);
        Assert.Empty(options.Problems());
    }

    private static HotkeyGesture Parse(string text) =>
        HotkeyGesture.TryParse(text, out var gesture) ? gesture : throw new ArgumentException(text);
}
