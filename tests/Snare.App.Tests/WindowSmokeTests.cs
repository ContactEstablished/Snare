using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Snare.App.Capture;
using Snare.App.Editor;
using Snare.App.Library;
using Snare.App.Messages;
using Snare.App.QuickBar;
using Snare.App.Services;
using Snare.Capture.Selection;
using Snare.Core.Captures;
using Snare.Core.Geometry;
using Snare.Core.Imaging;

namespace Snare.App.Tests;

/// <summary>Constructs and renders windows off-screen: catches XAML, binding-setup and OnRender errors.</summary>
public class WindowSmokeTests
{
    private static EditorService Editors(ICaptureStore store) => new(
        store, new TrayIconService(NullLogger<TrayIconService>.Instance), new ServiceCollection().BuildServiceProvider(), NullLogger<EditorService>.Instance);

    private static FrozenDesktop Desktop()
    {
        var bounds = new PixelRect(0, 0, 400, 200);
        var monitor = new MonitorInfo("primary", bounds, bounds, 96, true);
        var window = new WindowInfo(1, "Notes", "notepad", new PixelRect(20, 20, 100, 80));
        return new FrozenDesktop(
            [new FrozenMonitor(monitor, PixelBuffer.Filled(400, 200, 0xFF336699))],
            DateTimeOffset.UnixEpoch, [window], new PixelPoint(50, 50));
    }

    [Fact]
    public void LibraryWindow_LoadsItsXaml()
    {
        Sta.Run(() =>
        {
            var store = new FakeCaptureStore();
            var viewModel = new LibraryViewModel(store, new ClipboardService(NullLogger<ClipboardService>.Instance), Editors(store), NullLogger<LibraryViewModel>.Instance);

            var window = new LibraryWindow(viewModel);

            Assert.Equal("Snare Library", window.Title);
            viewModel.Detach();
        });
    }

    [Fact]
    public void LibraryViewModel_ListsNewestFirstAndFollowsMessages()
    {
        Sta.Run(() =>
        {
            var store = new FakeCaptureStore();
            store.Captures.AddRange([FakeCaptureStore.Record(1), FakeCaptureStore.Record(2)]);
            var viewModel = new LibraryViewModel(store, new ClipboardService(NullLogger<ClipboardService>.Instance), Editors(store), NullLogger<LibraryViewModel>.Instance);

            viewModel.LoadAsync().GetAwaiter().GetResult();
            Assert.Equal([2, 1], viewModel.Tiles.Select(t => t.Capture.CreatedUtc.Minute));
            Assert.Equal("2 captures", viewModel.Summary);

            var newest = FakeCaptureStore.Record(3);
            WeakReferenceMessenger.Default.Send(new CaptureSavedMessage(newest));
            Assert.Equal(newest.Id, viewModel.Tiles[0].Capture.Id);

            WeakReferenceMessenger.Default.Send(new CaptureTrashedMessage(newest.Id));
            Assert.Equal(2, viewModel.Tiles.Count);

            viewModel.Detach();
        });
    }

    [Fact]
    public void QuickBar_LoadsItsXaml()
    {
        Sta.Run(() =>
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var viewModel = new QuickBarViewModel(
                FakeCaptureStore.Record(1),
                new ClipboardService(NullLogger<ClipboardService>.Instance),
                new FakeCaptureStore(),
                new LibraryWindowService(services),
                Editors(new FakeCaptureStore()),
                NullLogger.Instance);
            var bounds = new PixelRect(0, 0, 1920, 1080);

            var window = new QuickBarWindow(viewModel, new PixelRect(100, 100, 300, 200), new MonitorInfo("m", bounds, bounds, 96, true));

            Assert.Same(viewModel, window.DataContext);
            Assert.Equal("800 × 600 · copied", viewModel.Summary);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureOverlay_RendersHoveringAndAdjusting(bool withRegion)
    {
        Sta.Run(() =>
        {
            var desktop = Desktop();
            var viewModel = new CaptureOverlayViewModel(desktop);
            if (withRegion)
            {
                viewModel.PointerDown(new PixelPoint(30, 30));
                viewModel.PointerMove(new PixelPoint(200, 150));
                viewModel.PointerUp(new PixelPoint(200, 150));
                Assert.Equal(SelectionPhase.Adjusting, viewModel.Session.Phase);
            }

            var window = new CaptureOverlayWindow(viewModel, desktop.Monitors[0]);
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(400, 200));
            root.Arrange(new Rect(0, 0, 400, 200));
            var target = new RenderTargetBitmap(400, 200, 96, 96, PixelFormats.Pbgra32);
            target.Render(root);

            Assert.Equal(400, target.PixelWidth);
            Assert.Equal(withRegion ? "171 × 121" : "100 × 80", viewModel.SizeText);
        });
    }

    [Fact]
    public async Task CaptureOverlayViewModel_CompletesWhenAccepted()
    {
        var viewModel = new CaptureOverlayViewModel(Desktop());

        viewModel.TakeAllMonitorsCommand.Execute(null);

        Assert.True(viewModel.Completion.IsCompletedSuccessfully);
        Assert.Equal(new SelectionResult(new PixelRect(0, 0, 400, 200), CaptureTarget.AllMonitors), await viewModel.Completion);
    }

    [Fact]
    public async Task CaptureOverlayViewModel_CompletesWithNullWhenCancelled()
    {
        var viewModel = new CaptureOverlayViewModel(Desktop());

        viewModel.CancelCommand.Execute(null);

        Assert.Null(await viewModel.Completion);
    }
}
