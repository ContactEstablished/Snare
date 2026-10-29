using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Snare.App.Editor;
using Snare.App.Services;
using Snare.Core.Annotations;
using Snare.Core.Geometry;
using Snare.Core.Options;
using Snare.Library;
using Snare.Rendering;
using Snare.Rendering.Editing;

namespace Snare.App.Tests;

/// <summary>Builds the editor off-screen: XAML loads, the canvas renders, and the view model drives the session.</summary>
public sealed class EditorSmokeTests : IDisposable
{
    private readonly DirectoryInfo _appData = Directory.CreateTempSubdirectory("snare-editor-");

    public void Dispose() => _appData.Delete(recursive: true);

    private EditorViewModel ViewModel(FakeCaptureStore store, params AnnotationObject[] objects)
    {
        var record = FakeCaptureStore.Record(1) with { OriginalPath = @"C:\nowhere\original.png" };
        store.Captures.Add(record);
        var original = new SKBitmap(new SKImageInfo(400, 300, SKColorType.Bgra8888, SKAlphaType.Premul));
        original.Erase(new SKColor(0x33, 0x66, 0x99));
        var paths = new LibraryPaths(Path.Combine(_appData.FullName, "Pictures"), _appData.FullName);
        return new EditorViewModel(
            record,
            original,
            new AnnotationDocument { Canvas = new CanvasInfo(400, 300), Objects = [.. objects] },
            store,
            new ClipboardService(NullLogger<ClipboardService>.Instance),
            new EditorStyleStore(paths, NullLogger<EditorStyleStore>.Instance),
            new AnnotationRenderer(),
            new StaticOptions(),
            NullLogger.Instance);
    }

    [Fact]
    public void EditorWindow_LoadsAndRenders()
    {
        Sta.Run(() =>
        {
            using var viewModel = ViewModel(new FakeCaptureStore(),
                new ArrowAnnotation("a", new PixelPoint(10, 10), new PixelPoint(200, 150), "#E53935", 6));
            var window = new EditorWindow(viewModel);
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(1200, 800));
            root.Arrange(new Rect(0, 0, 1200, 800));
            root.UpdateLayout();

            var target = new RenderTargetBitmap(1200, 800, 96, 96, PixelFormats.Pbgra32);
            target.Render(root);

            Assert.Equal(1200, target.PixelWidth);
            Assert.StartsWith("Snare Editor", window.Title, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void StylePanel_FollowsTheTool()
    {
        using var viewModel = ViewModel(new FakeCaptureStore());

        viewModel.Tool = EditorTool.Text;
        Assert.True(viewModel.ShowsText);
        Assert.False(viewModel.ShowsWidth);

        viewModel.Tool = EditorTool.Blur;
        Assert.True(viewModel.ShowsStrength);
        Assert.False(viewModel.ShowsColor);

        viewModel.Tool = EditorTool.Rectangle;
        viewModel.StrokeWidth = 9;
        viewModel.Filled = true;
        Assert.Equal(9, viewModel.Session.Styles[EditorTool.Rectangle].Width);
        Assert.NotNull(viewModel.Session.Styles[EditorTool.Rectangle].FillColor);
    }

    [Fact]
    public void StylePanel_WithSelection_RestylesTheSelection()
    {
        var rect = new RectangleAnnotation("r", new PixelRect(10, 10, 50, 50), "#E53935", 4);
        using var viewModel = ViewModel(new FakeCaptureStore(), rect);
        viewModel.Session.Select("r");

        viewModel.Color = "#1E88E5";

        Assert.Equal("#1E88E5", ((RectangleAnnotation)viewModel.Session.Objects[0]).Stroke);
        Assert.Equal("#1E88E5", viewModel.Color);
        Assert.True(viewModel.CanUndo);
    }

    [Fact]
    public void RedactionBanners_FollowTheObjects()
    {
        using var viewModel = ViewModel(new FakeCaptureStore(), new RedactAnnotation("b", new PixelRect(0, 0, 10, 10), RedactMode.Blur));
        Assert.True(viewModel.HasObscuringRedaction);
        Assert.False(viewModel.SuggestDiscardOriginal);

        viewModel.Tool = EditorTool.Redact;
        viewModel.Session.PointerDown(new SKPoint(20, 20));
        viewModel.Session.PointerUp(new SKPoint(80, 60));

        Assert.True(viewModel.SuggestDiscardOriginal);
    }

    [Fact]
    public async Task Save_ReexportsOnlyWhenChanged()
    {
        var store = new FakeCaptureStore();
        using var viewModel = ViewModel(store);
        Assert.False(viewModel.NeedsExport);

        viewModel.Tool = EditorTool.Step;
        viewModel.Session.PointerDown(new SKPoint(20, 20));
        viewModel.Session.PointerUp(new SKPoint(20, 20));
        Assert.True(viewModel.NeedsExport);

        await viewModel.SaveAsync();

        Assert.False(viewModel.NeedsExport);
        Assert.Equal("Saved", viewModel.Status);
        Assert.Equal(1, store.ReexportCount);
    }

    private sealed class StaticOptions : IOptionsMonitor<SnareOptions>
    {
        public SnareOptions CurrentValue { get; } = new();

        public SnareOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<SnareOptions, string?> listener) => null;
    }
}
