using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using Snare.App.Settings;
using Snare.Core.Hotkeys;
using Snare.Core.Options;

namespace Snare.App.Tests;

public class SettingsTests
{
    private static SettingsViewModel Settings(SnareOptions current, List<SnareOptions>? saved = null, string backupResult = "Saved") => new(
        current,
        o => { saved?.Add(o); return Task.CompletedTask; },
        () => Task.FromResult(backupResult),
        () => null,
        @"C:\Users\matt\Pictures\Snare",
        @"C:\Users\matt\Documents\Snare Backups");

    [Fact]
    public async Task Save_HandsOverTheEditsAndKeepsEverythingElse()
    {
        var current = new SnareOptions { JpegQuality = 85, FfmpegPath = @"D:\ffmpeg\ffmpeg.exe" };
        var saved = new List<SnareOptions>();
        var settings = Settings(current, saved);
        var closed = false;
        settings.CloseRequested += (_, _) => closed = true;

        settings.Hotkeys.Single(h => h.Action == HotkeyAction.GrabText).Press(new HotkeyGesture(true, true, false, false, "T"), clear: false);
        settings.Hotkeys.Single(h => h.Action == HotkeyAction.RepeatLastRegion).Press(null, clear: true);
        settings.LibraryRoot = @"  E:\Captures ";
        settings.UseJpeg = true;
        settings.OpenEditorAfterCapture = true;
        settings.StartWithWindows = false;
        await settings.SaveCommand.ExecuteAsync(null);

        var options = Assert.Single(saved);
        Assert.True(closed);
        Assert.Equal("Ctrl+Alt+T", options.Hotkeys.GrabText);
        Assert.Equal("", options.Hotkeys.RepeatLastRegion);
        Assert.Equal("PrtSc", options.Hotkeys.CaptureImage);
        Assert.Equal(@"E:\Captures", options.LibraryRoot);
        Assert.Equal(ExportFormat.Jpeg, options.DefaultExportFormat);
        Assert.Equal(AfterCaptureAction.Editor, options.AfterCapture);
        Assert.False(options.StartWithWindows);
        Assert.Equal(85, options.JpegQuality);
        Assert.Equal(@"D:\ffmpeg\ffmpeg.exe", options.FfmpegPath);
        Assert.Null(options.BackupFolder);
        Assert.Equal("Shift+PrtSc", current.Hotkeys.RecordVideo); // the live options are untouched
        Assert.Equal("Ctrl+PrtSc", current.Hotkeys.RepeatLastRegion);
    }

    [Fact]
    public void Save_IsOffWhileSomethingIsWrong_AndSaysWhy()
    {
        var settings = Settings(new SnareOptions());

        settings.Hotkeys.Single(h => h.Action == HotkeyAction.RecordVideo).Gesture = "PrtSc";
        Assert.False(settings.SaveCommand.CanExecute(null));
        Assert.Contains("both Capture and Record video", settings.Problem, StringComparison.Ordinal);

        settings.Hotkeys.Single(h => h.Action == HotkeyAction.RecordVideo).Gesture = "Shift+PrtSc";
        settings.JpegQuality = 20;
        Assert.Contains("JPG quality", settings.Problem, StringComparison.Ordinal);

        settings.JpegQuality = 90;
        settings.BackupFolder = "relative\\folder";
        Assert.Contains("backup folder", settings.Problem, StringComparison.Ordinal);

        settings.BackupFolder = "";
        Assert.Null(settings.Problem);
        Assert.True(settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task BackUpNow_ShowsTheResult()
    {
        var settings = Settings(new SnareOptions(), backupResult: "Saved Snare-backup.zip (1.0 MB)");

        await settings.BackUpNowCommand.ExecuteAsync(null);

        Assert.Equal("Saved Snare-backup.zip (1.0 MB)", settings.BackupStatus);
        Assert.Equal("No backups yet.", settings.LastBackupText);
        Assert.False(settings.IsBackingUp);
    }

    [Fact]
    public async Task Store_RoundTripsThroughConfiguration()
    {
        var folder = Directory.CreateTempSubdirectory("snare-settings-");
        try
        {
            var store = new UserSettingsStore(Path.Combine(folder.FullName, "settings.json"));
            var written = new SnareOptions
            {
                LibraryRoot = @"E:\Captures",
                DefaultExportFormat = ExportFormat.Jpeg,
                AfterCapture = AfterCaptureAction.Editor,
                StartWithWindows = false,
            };
            written.Hotkeys.GrabText = "";
            written.Hotkeys.CaptureImage = "Ctrl+Alt+S";

            await store.SaveAsync(written, TestContext.Current.CancellationToken);
            await store.SaveAsync(written, TestContext.Current.CancellationToken); // replacing an existing file works too

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Snare:Hotkeys:GrabText"] = "Ctrl+Alt+PrtSc", ["Snare:JpegQuality"] = "70" })
                .AddJsonFile(store.FilePath)
                .Build();
            var read = new SnareOptions();
            configuration.GetSection(SnareOptions.SectionName).Bind(read);

            Assert.Equal(@"E:\Captures", read.LibraryRoot);
            Assert.Equal(ExportFormat.Jpeg, read.DefaultExportFormat);
            Assert.Equal(AfterCaptureAction.Editor, read.AfterCapture);
            Assert.False(read.StartWithWindows);
            Assert.Equal("Ctrl+Alt+S", read.Hotkeys.CaptureImage);
            Assert.Equal("", read.Hotkeys.GrabText); // the user file wins over the defaults below it
            Assert.Equal(90, read.JpegQuality);
            Assert.False(File.Exists(store.FilePath + ".tmp"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(Key.Snapshot, ModifierKeys.None, "PrtSc")]
    [InlineData(Key.S, ModifierKeys.Control | ModifierKeys.Alt, "Ctrl+Alt+S")]
    [InlineData(Key.D4, ModifierKeys.Control, "Ctrl+4")]
    [InlineData(Key.F9, ModifierKeys.Shift | ModifierKeys.Windows, "Shift+Win+F9")]
    public void HotkeyBox_TurnsKeysIntoGestures(Key key, ModifierKeys modifiers, string expected)
    {
        Assert.Equal(expected, HotkeyCapture.FromKey(key, modifiers)?.ToString());
    }

    [Fact]
    public void HotkeyBox_IgnoresAModifierOnItsOwn_AndOtherKeys()
    {
        Assert.Null(HotkeyCapture.FromKey(Key.LeftCtrl, ModifierKeys.Control));
        Assert.Null(HotkeyCapture.FromKey(Key.Space, ModifierKeys.Control));
        Assert.Null(HotkeyCapture.FromKey(Key.NumPad7, ModifierKeys.Alt));
        var item = new HotkeyItemViewModel(HotkeyAction.CaptureImage, "PrtSc");
        item.Press(null, clear: false);
        Assert.Equal("PrtSc", item.DisplayText);
        item.Press(null, clear: true);
        Assert.Equal("Off", item.DisplayText);
    }

    [Theory]
    [InlineData(@"C:\Users\matt\AppData\Local\Programs\Snare\Snare.exe", true)]
    [InlineData(@"c:\users\matt\appdata\local\programs\snare\SNARE.EXE", true)]
    [InlineData(@"C:\Projects\Snare\.worktrees\phase-6\src\Snare.App\bin\x64\Release\Snare.exe", false)]
    [InlineData(@"C:\Users\matt\AppData\Local\Programs\Snare\old\Snare.exe", false)]
    public void StartWithWindows_IsOnlyWrittenByTheInstalledCopy(string exe, bool installed)
    {
        Assert.Equal(installed, SettingsService.IsInstalledCopy(exe, @"C:\Users\matt\AppData\Local\Programs\Snare\"));
    }

    [Fact]
    public async Task Save_FailureStaysOpenAndSaysWhy()
    {
        var settings = new SettingsViewModel(
            new SnareOptions(), _ => throw new IOException("The file is in use."), () => Task.FromResult(""), () => null, "a", "b");
        var closed = false;
        settings.CloseRequested += (_, _) => closed = true;

        await settings.SaveCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Contains("The file is in use.", settings.SaveError, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_LoadsItsXaml()
    {
        Sta.Run(() =>
        {
            var settings = Settings(new SnareOptions());
            var window = new SettingsWindow(settings);
            Assert.Same(settings, window.DataContext);
            Assert.NotNull(settings.PickFolder);
            Assert.Equal("Snare settings", window.Title);
        });
    }
}
