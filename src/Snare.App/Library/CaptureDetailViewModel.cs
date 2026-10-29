using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Snare.Core.Captures;
using Snare.Core.Search;

namespace Snare.App.Library;

/// <summary>The detail pane for one selected capture: editable title, description and tags; read-only facts.</summary>
public sealed partial class CaptureDetailViewModel : ObservableObject
{
    private readonly ICaptureCatalog _catalog;
    private readonly ILogger _logger;
    private readonly Action<CaptureRecord> _updated;
    private string? _savedTitle;
    private string? _savedDescription;
    private int _loadVersion;

    public CaptureDetailViewModel(CaptureRecord capture, ICaptureCatalog catalog, ILogger logger, Action<CaptureRecord> updated)
    {
        Capture = capture;
        _catalog = catalog;
        _logger = logger;
        _updated = updated;
        _savedTitle = Title = capture.Title;
        _savedDescription = Description = capture.Description;
    }

    public CaptureRecord Capture { get; private set; }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial string NewTag { get; set; } = "";

    /// <summary>Typing a comma adds what came before it, like Enter.</summary>
    partial void OnNewTagChanged(string value)
    {
        if (value.TrimEnd().EndsWith(','))
        {
            AddTagCommand.Execute(null);
        }
    }

    public ObservableCollection<TagChipViewModel> Tags { get; } = [];

    /// <summary>Every existing tag name, for autocomplete.</summary>
    public ObservableCollection<string> Suggestions { get; } = [];

    public string Source => string.Join(" · ", new[] { Capture.SourceApp, Capture.SourceWindowTitle }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string Facts => string.Create(CultureInfo.CurrentCulture,
        $"{Capture.Width} × {Capture.Height} · {Capture.CreatedUtc.ToLocalTime():MMM d, yyyy h:mm tt}");

    public string FilePath => Capture.FilePath;

    public string OcrText => Capture.OcrText switch
    {
        null => "Not read yet.",
        "" => "No text found.",
        var text => text,
    };

    public async Task LoadAsync()
    {
        // Two loads can overlap (select, then add a tag quickly); only the newest may fill the lists.
        var version = ++_loadVersion;
        IReadOnlyList<TagInfo> tags;
        IReadOnlyList<string> names;
        try
        {
            tags = await _catalog.ListTagsAsync();
            names = await _catalog.GetTagsAsync(Capture.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load the tags of capture {Id}", Capture.Id);
            return;
        }

        if (version != _loadVersion)
        {
            return;
        }

        var colors = tags.ToDictionary(t => t.Name, t => t.Color, StringComparer.OrdinalIgnoreCase);
        Tags.Clear();
        foreach (var name in names)
        {
            Tags.Add(new TagChipViewModel(name, colors.GetValueOrDefault(name, TagPalette.ForIndex(0))));
        }

        Suggestions.Clear();
        foreach (var tag in tags)
        {
            Suggestions.Add(tag.Name);
        }
    }

    /// <summary>Title and description are saved when the field loses focus (or Enter in the title).</summary>
    [RelayCommand]
    public async Task SaveDetailsAsync()
    {
        if (Title == _savedTitle && Description == _savedDescription)
        {
            return;
        }

        // Remember what is being saved, not what is in the box when the save finishes.
        var title = Title;
        var description = Description;
        try
        {
            Capture = await _catalog.UpdateDetailsAsync(Capture.Id, title, description);
            _savedTitle = title;
            _savedDescription = description;
            _updated(Capture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save the details of capture {Id}", Capture.Id);
        }
    }

    /// <summary>Adds the typed tag(s); commas separate several.</summary>
    [RelayCommand]
    private async Task AddTagAsync()
    {
        var names = SplitTags(NewTag);
        NewTag = "";
        if (names.Count == 0)
        {
            return;
        }

        await SaveTagsAsync(Tags.Select(t => t.Name).Concat(names));
    }

    [RelayCommand]
    private async Task RemoveTagAsync(TagChipViewModel? tag)
    {
        if (tag is not null)
        {
            await SaveTagsAsync(Tags.Where(t => t != tag).Select(t => t.Name));
        }
    }

    /// <summary>Clicking a chip's dot moves the tag to the next palette color (for every capture).</summary>
    [RelayCommand]
    private async Task CycleColorAsync(TagChipViewModel? tag)
    {
        if (tag is null)
        {
            return;
        }

        var next = TagPalette.Next(tag.Color);
        try
        {
            await _catalog.SetTagColorAsync(tag.Name, next);
            tag.Color = next;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not recolor tag {Tag}", tag.Name);
        }
    }

    public static IReadOnlyList<string> SplitTags(string? text) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task SaveTagsAsync(IEnumerable<string> names)
    {
        try
        {
            await _catalog.SetTagsAsync(Capture.Id, names.ToList());
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save the tags of capture {Id}", Capture.Id);
        }
    }
}
