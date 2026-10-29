using SkiaSharp;
using Snare.Core.Annotations;
using Snare.Core.Geometry;

namespace Snare.Rendering.Editing;

/// <summary>
/// Everything the editor does, with no UI types: tools, selection, handles, undo and redo.
/// Pointer positions are original-image pixels; the view converts from screen coordinates.
/// </summary>
public sealed class EditorSession
{
    public const int RecentColorLimit = 8;
    private const float MinimumShapeSize = 2;

    private readonly SKBitmap _original;
    private readonly Dictionary<EditorTool, ToolStyle> _styles;
    private readonly List<string> _recentColors = [];
    private readonly HashSet<string> _selection = [];

    private EditorTool _tool = EditorTool.Select;
    private EditorTool _toolBeforeEyedropper = EditorTool.Select;
    private Gesture _gesture = Gesture.None;
    private SKPoint _start;
    private EditorHandle _activeHandle;
    private List<AnnotationObject> _dragOriginals = [];
    private List<PixelPoint> _strokePoints = [];

    public EditorSession(SKBitmap original, AnnotationDocument document, IReadOnlyDictionary<EditorTool, ToolStyle>? styles = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(document);
        _original = original;
        Document = new EditorDocument(document);
        History = new UndoStack(Document);
        _styles = Enum.GetValues<EditorTool>().ToDictionary(t => t, t => styles?.GetValueOrDefault(t) ?? ToolStyle.DefaultFor(t));
    }

    private enum Gesture
    {
        None,
        Moving,
        Resizing,
        Marquee,
        Drawing,
        Cropping,
    }

    public EditorDocument Document { get; }

    public UndoStack History { get; }

    public IReadOnlyList<AnnotationObject> Objects => Document.Objects;

    public IReadOnlySet<string> Selection => _selection;

    public IReadOnlyList<string> RecentColors => _recentColors;

    public IReadOnlyDictionary<EditorTool, ToolStyle> Styles => _styles;

    /// <summary>The object being drawn but not yet added (shown on top by the view).</summary>
    public AnnotationObject? Preview { get; private set; }

    /// <summary>The marquee rectangle while selecting by dragging.</summary>
    public SKRect? Marquee { get; private set; }

    /// <summary>The crop frame being drawn with the Crop tool; Enter applies it.</summary>
    public PixelRect? PendingCrop { get; private set; }

    /// <summary>Hit tolerance in image pixels; the view sets it from the zoom (a few screen pixels).</summary>
    public float HitTolerance { get; set; } = 4;

    /// <summary>Raised after anything changes that the view should redraw.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when a text object should be opened for typing (new or existing).</summary>
    public event EventHandler<TextAnnotation>? TextEditRequested;

    public EditorTool Tool
    {
        get => _tool;
        set
        {
            if (value == _tool)
            {
                return;
            }

            if (value == EditorTool.Eyedropper)
            {
                _toolBeforeEyedropper = _tool;
            }

            CancelGesture();
            _tool = value;
            PendingCrop = value == EditorTool.Crop ? Document.Canvas.Crop : null;
            if (value != EditorTool.Select)
            {
                _selection.Clear();
            }

            OnChanged();
        }
    }

    public bool HasUnsavedChanges { get; private set; }

    /// <summary>The style the current tool draws with.</summary>
    public ToolStyle CurrentStyle => _styles[_tool];

    public AnnotationDocument ToDocument() => Document.ToAnnotationDocument();

    /// <summary>Call after saving so <see cref="HasUnsavedChanges"/> tracks the next edit.</summary>
    public void MarkSaved() => HasUnsavedChanges = false;

    // ---- Styles and colors -------------------------------------------------------------

    /// <summary>
    /// Sets a tool's style. With the Select tool and a selection, the style's color and sizes are
    /// also applied to the selected objects, as one undoable change.
    /// </summary>
    public void SetStyle(EditorTool tool, ToolStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _styles[tool] = style;
        RememberColor(style.Color);
        OnChanged();
    }

    /// <summary>Restyles the selection (color, width, fill, font size) from a style.</summary>
    public void ApplyStyleToSelection(ToolStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        var changes = SelectedObjects()
            .Select(o => (Before: o, After: Restyle(o, style)))
            .Where(c => !Equals(c.Before, c.After))
            .ToList();
        if (changes.Count > 0)
        {
            Execute(new ReplaceObjectsCommand("Restyle", changes));
        }

        RememberColor(style.Color);
    }

    private void RememberColor(string color)
    {
        _recentColors.RemoveAll(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
        _recentColors.Insert(0, color);
        if (_recentColors.Count > RecentColorLimit)
        {
            _recentColors.RemoveAt(_recentColors.Count - 1);
        }
    }

    // ---- Pointer input -----------------------------------------------------------------

    public void PointerDown(SKPoint p, bool shift = false)
    {
        _start = p;
        switch (_tool)
        {
            case EditorTool.Select:
                BeginSelect(p, shift);
                break;
            case EditorTool.Pen or EditorTool.Highlighter:
                _gesture = Gesture.Drawing;
                _strokePoints = [Round(p)];
                Preview = BuildStroke(_strokePoints);
                break;
            case EditorTool.Text:
                BeginText(p);
                return;
            case EditorTool.Step:
                var style = _styles[EditorTool.Step];
                Execute(new AddObjectCommand(new StepAnnotation(NewId(), Round(p), style.Color, Math.Max(6, style.Width))));
                return;
            case EditorTool.Eyedropper:
                PickColor(p);
                return;
            case EditorTool.Crop:
                _gesture = Gesture.Cropping;
                PendingCrop = null;
                break;
            default:
                _gesture = Gesture.Drawing;
                Preview = null;
                break;
        }

        OnChanged();
    }

    public void PointerMove(SKPoint p, bool shift = false)
    {
        switch (_gesture)
        {
            case Gesture.Moving:
                MoveSelection(Round(p).X - Round(_start).X, Round(p).Y - Round(_start).Y);
                break;
            case Gesture.Resizing:
                ResizeSelected(Round(p));
                break;
            case Gesture.Marquee:
                Marquee = SKRect.Create(Math.Min(_start.X, p.X), Math.Min(_start.Y, p.Y), Math.Abs(p.X - _start.X), Math.Abs(p.Y - _start.Y));
                break;
            case Gesture.Drawing when _tool is EditorTool.Pen or EditorTool.Highlighter:
                var point = Round(shift && _tool == EditorTool.Highlighter ? new SKPoint(p.X, _start.Y) : p);
                if (_strokePoints.Count == 0 || Distance(_strokePoints[^1], point) >= 1.5f)
                {
                    _strokePoints.Add(point);
                }

                Preview = BuildStroke(_strokePoints);
                break;
            case Gesture.Drawing:
                Preview = BuildShape(_start, p, shift);
                break;
            case Gesture.Cropping:
                PendingCrop = ClipToImage(PixelRect.FromCorners(Round(_start), Round(p)));
                break;
            default:
                return;
        }

        OnChanged();
    }

    public void PointerUp(SKPoint p, bool shift = false)
    {
        switch (_gesture)
        {
            case Gesture.Moving or Gesture.Resizing:
                FinishDrag();
                break;
            case Gesture.Marquee:
                FinishMarquee(shift);
                break;
            case Gesture.Drawing when _tool is EditorTool.Pen or EditorTool.Highlighter:
                if (_strokePoints.Count > 0)
                {
                    Execute(new AddObjectCommand(BuildStroke(_strokePoints)));
                }

                break;
            case Gesture.Drawing:
                if (BuildShape(_start, p, shift) is { } shape && IsBigEnough(shape))
                {
                    Execute(new AddObjectCommand(shape));
                    _selection.Clear();
                    _selection.Add(shape.Id);
                }

                break;
            case Gesture.Cropping:
                PendingCrop = ClipToImage(PixelRect.FromCorners(Round(_start), Round(p)));
                if (PendingCrop is { IsEmpty: true } || PendingCrop is { Width: < 2 } or { Height: < 2 })
                {
                    PendingCrop = null;
                }

                break;
        }

        _gesture = Gesture.None;
        Preview = null;
        Marquee = null;
        OnChanged();
    }

    /// <summary>Double-click: with the Select tool, opens a text object for editing.</summary>
    public void DoubleClick(SKPoint p)
    {
        if (TopmostAt(p) is TextAnnotation text)
        {
            TextEditRequested?.Invoke(this, text);
        }
    }

    /// <summary>Which handle (or the body) of the selection is under a point, for cursors.</summary>
    public EditorHandle HandleAt(SKPoint p)
    {
        if (_tool != EditorTool.Select)
        {
            return EditorHandle.None;
        }

        if (SingleSelected() is { } single)
        {
            foreach (var (handle, position) in AnnotationGeometry.HandlesOf(single))
            {
                if (SKPoint.Distance(p, position) <= HitTolerance * 2)
                {
                    return handle;
                }
            }
        }

        return SelectedObjects().Any(o => AnnotationGeometry.HitTest(o, p, HitTolerance)) ? EditorHandle.Body : EditorHandle.None;
    }

    // ---- Selection and arrangement -------------------------------------------------------

    public void SelectAll()
    {
        Tool = EditorTool.Select;
        _selection.Clear();
        _selection.UnionWith(Document.Objects.Select(o => o.Id));
        OnChanged();
    }

    public void ClearSelection()
    {
        _selection.Clear();
        OnChanged();
    }

    public void Select(params string[] ids)
    {
        _selection.Clear();
        _selection.UnionWith(ids.Where(id => Document.Find(id) is not null));
        OnChanged();
    }

    public void DeleteSelection()
    {
        if (_selection.Count == 0)
        {
            return;
        }

        Execute(new RemoveObjectsCommand(_selection.ToList()));
        _selection.Clear();
        OnChanged();
    }

    /// <summary>Arrow keys move the selection 1 px, or 10 px with Shift.</summary>
    public void Nudge(int dx, int dy)
    {
        var changes = SelectedObjects().Select(o => (Before: o, After: AnnotationGeometry.Translate(o, dx, dy))).ToList();
        if (changes.Count > 0)
        {
            Execute(new ReplaceObjectsCommand("Move", changes));
        }
    }

    public void BringToFront() => Arrange(ids => [.. ids.Where(id => !_selection.Contains(id)), .. ids.Where(_selection.Contains)]);

    public void SendToBack() => Arrange(ids => [.. ids.Where(_selection.Contains), .. ids.Where(id => !_selection.Contains(id))]);

    public void BringForward() => Arrange(ids => Step(ids, forward: true));

    public void SendBackward() => Arrange(ids => Step(ids, forward: false));

    // ---- Text, crop, resize ---------------------------------------------------------------

    /// <summary>
    /// Finishes typing into a text object. A new object is added; an existing one is replaced;
    /// empty text removes it (or adds nothing).
    /// </summary>
    public void CommitText(TextAnnotation target, string text)
    {
        ArgumentNullException.ThrowIfNull(target);
        text ??= "";
        var existing = Document.Find(target.Id);
        var blank = string.IsNullOrWhiteSpace(text);

        if (existing is null)
        {
            if (!blank)
            {
                Execute(new AddObjectCommand(target with { Text = text }));
                Select(target.Id);
            }
        }
        else if (blank)
        {
            Execute(new RemoveObjectsCommand([target.Id]));
            _selection.Remove(target.Id);
        }
        else if (existing is TextAnnotation old && old.Text != text)
        {
            Execute(new ReplaceObjectsCommand("Edit text", [(old, old with { Text = text })]));
        }

        OnChanged();
    }

    /// <summary>Enter with the Crop tool: applies the frame drawn, then returns to Select.</summary>
    public void ApplyCrop()
    {
        if (PendingCrop is not { } crop)
        {
            return;
        }

        var before = Document.Canvas;
        var isWhole = crop == new PixelRect(0, 0, before.Width, before.Height);
        Execute(new CanvasCommand("Crop", before, before with { Crop = isWhole ? null : crop, Resize = null }));
        PendingCrop = null;
        Tool = EditorTool.Select;
    }

    public void ClearCrop()
    {
        var before = Document.Canvas;
        if (before.Crop is not null)
        {
            Execute(new CanvasCommand("Remove crop", before, before with { Crop = null, Resize = null }));
        }

        PendingCrop = null;
        OnChanged();
    }

    /// <summary>Sets the exported size (after the crop); null restores the natural size.</summary>
    public void Resize(PixelSize? size)
    {
        if (size is { } s && (s.Width < 1 || s.Height < 1))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "The output size must be at least 1x1.");
        }

        var before = Document.Canvas;
        var natural = new PixelSize(before.ExportArea.Width, before.ExportArea.Height);
        var after = before with { Resize = size == natural ? null : size };
        if (after != before)
        {
            Execute(new CanvasCommand("Resize", before, after));
        }
    }

    // ---- Undo ------------------------------------------------------------------------------

    public void Undo()
    {
        if (History.Undo() is not null)
        {
            AfterHistoryChange();
        }
    }

    public void Redo()
    {
        if (History.Redo() is not null)
        {
            AfterHistoryChange();
        }
    }

    /// <summary>Esc: cancels an in-progress gesture or crop, else clears the selection.</summary>
    public void Escape()
    {
        if (_gesture != Gesture.None || PendingCrop is not null)
        {
            CancelGesture();
            PendingCrop = null;
        }
        else
        {
            _selection.Clear();
        }

        OnChanged();
    }

    // ---- Internals --------------------------------------------------------------------------

    private void BeginSelect(SKPoint p, bool shift)
    {
        var handle = HandleAt(p);
        if (!shift && SingleSelected() is { } single && handle is not EditorHandle.None and not EditorHandle.Body)
        {
            _gesture = Gesture.Resizing;
            _activeHandle = handle;
            _dragOriginals = [single];
            return;
        }

        var hit = TopmostAt(p);
        if (hit is null)
        {
            if (!shift)
            {
                _selection.Clear();
            }

            _gesture = Gesture.Marquee;
            Marquee = SKRect.Create(p, SKSize.Empty);
            return;
        }

        if (shift)
        {
            if (!_selection.Remove(hit.Id))
            {
                _selection.Add(hit.Id);
            }

            return;
        }

        if (!_selection.Contains(hit.Id))
        {
            _selection.Clear();
            _selection.Add(hit.Id);
        }

        _gesture = Gesture.Moving;
        _dragOriginals = SelectedObjects().ToList();
    }

    private void BeginText(SKPoint p)
    {
        if (TopmostAt(p) is TextAnnotation existing)
        {
            TextEditRequested?.Invoke(this, existing);
            return;
        }

        var style = _styles[EditorTool.Text];
        var text = new TextAnnotation(NewId(), Round(p), "", style.Color, style.FontSize, Background: style.TextBackground, Bold: style.Bold);
        TextEditRequested?.Invoke(this, text);
    }

    private void PickColor(SKPoint p)
    {
        var point = Round(p);
        if (point.X >= 0 && point.Y >= 0 && point.X < _original.Width && point.Y < _original.Height)
        {
            var color = SkiaColors.ToHex(_original.GetPixel(point.X, point.Y).WithAlpha(0xFF));
            var target = _toolBeforeEyedropper is EditorTool.Select or EditorTool.Eyedropper or EditorTool.Crop
                ? EditorTool.Rectangle
                : _toolBeforeEyedropper;
            _styles[target] = _styles[target] with { Color = color };
            RememberColor(color);
            Tool = _toolBeforeEyedropper == EditorTool.Eyedropper ? EditorTool.Select : _toolBeforeEyedropper;
        }

        OnChanged();
    }

    private void MoveSelection(int dx, int dy)
    {
        foreach (var original in _dragOriginals)
        {
            Document.Objects[Document.IndexOf(original.Id)] = AnnotationGeometry.Translate(original, dx, dy);
        }
    }

    private void ResizeSelected(PixelPoint p)
    {
        var original = _dragOriginals[0];
        AnnotationObject resized;
        if (AnnotationGeometry.EndpointsOf(original) is { } ends)
        {
            resized = _activeHandle == EditorHandle.Start
                ? AnnotationGeometry.WithEndpoints(original, p, ends.To)
                : AnnotationGeometry.WithEndpoints(original, ends.From, p);
        }
        else if (AnnotationGeometry.BoxOf(original) is { } box)
        {
            var next = AnnotationGeometry.ResizeBox(box, _activeHandle, p);
            resized = AnnotationGeometry.WithBox(original, next with { Width = Math.Max(1, next.Width), Height = Math.Max(1, next.Height) });
        }
        else
        {
            return;
        }

        Document.Objects[Document.IndexOf(original.Id)] = resized;
    }

    /// <summary>The drag already changed the document; record it as one undoable command.</summary>
    private void FinishDrag()
    {
        var changes = _dragOriginals
            .Select(before => (Before: before, After: Document.Find(before.Id)!))
            .Where(c => !Equals(c.Before, c.After))
            .ToList();
        if (changes.Count > 0)
        {
            History.Record(new ReplaceObjectsCommand(_gesture == Gesture.Moving ? "Move" : "Resize", changes));
            HasUnsavedChanges = true;
        }

        _dragOriginals = [];
    }

    private void FinishMarquee(bool shift)
    {
        if (Marquee is not { } marquee || marquee.Width < 2 || marquee.Height < 2)
        {
            return;
        }

        if (!shift)
        {
            _selection.Clear();
        }

        foreach (var item in Document.Objects.Where(o => marquee.IntersectsWith(AnnotationGeometry.Bounds(o))))
        {
            _selection.Add(item.Id);
        }
    }

    private void CancelGesture()
    {
        if (_gesture is Gesture.Moving or Gesture.Resizing)
        {
            foreach (var original in _dragOriginals)
            {
                Document.Objects[Document.IndexOf(original.Id)] = original;
            }
        }

        _gesture = Gesture.None;
        _dragOriginals = [];
        Preview = null;
        Marquee = null;
    }

    private void Arrange(Func<List<string>, List<string>> reorder)
    {
        if (_selection.Count == 0)
        {
            return;
        }

        var before = Document.Objects.Select(o => o.Id).ToList();
        var after = reorder(before);
        if (!before.SequenceEqual(after))
        {
            Execute(new ReorderCommand(before, after));
        }
    }

    /// <summary>Moves each selected object one place up or down, past an unselected neighbour.</summary>
    private List<string> Step(List<string> ids, bool forward)
    {
        var order = new List<string>(ids);
        var indices = Enumerable.Range(0, order.Count);
        foreach (var i in forward ? indices.Reverse() : indices)
        {
            var j = forward ? i + 1 : i - 1;
            if (j >= 0 && j < order.Count && _selection.Contains(order[i]) && !_selection.Contains(order[j]))
            {
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        return order;
    }

    private void Execute(IEditCommand command)
    {
        History.Execute(command);
        HasUnsavedChanges = true;
        OnChanged();
    }

    private void AfterHistoryChange()
    {
        _selection.RemoveWhere(id => Document.Find(id) is null);
        HasUnsavedChanges = true;
        OnChanged();
    }

    private AnnotationObject? TopmostAt(SKPoint p)
    {
        for (var i = Document.Objects.Count - 1; i >= 0; i--)
        {
            if (AnnotationGeometry.HitTest(Document.Objects[i], p, HitTolerance))
            {
                return Document.Objects[i];
            }
        }

        return null;
    }

    private IEnumerable<AnnotationObject> SelectedObjects() => Document.Objects.Where(o => _selection.Contains(o.Id));

    private AnnotationObject? SingleSelected() => _selection.Count == 1 ? Document.Find(_selection.First()) : null;

    private AnnotationObject BuildStroke(List<PixelPoint> points)
    {
        var style = _styles[_tool];
        return _tool == EditorTool.Highlighter
            ? new HighlighterAnnotation(PreviewOrNewId(), [.. points], style.Color, style.Width)
            : new PenAnnotation(PreviewOrNewId(), [.. points], style.Color, style.Width);
    }

    private AnnotationObject? BuildShape(SKPoint a, SKPoint b, bool shift)
    {
        var style = _styles[_tool];
        var from = Round(a);
        var to = Round(shift ? Constrain(a, b) : b);
        var box = PixelRect.FromCorners(from, to);
        var id = PreviewOrNewId();
        return _tool switch
        {
            EditorTool.Arrow => new ArrowAnnotation(id, from, to, style.Color, style.Width, style.HeadStyle),
            EditorTool.Line => new LineAnnotation(id, from, to, style.Color, style.Width, style.Dashed),
            EditorTool.Rectangle => new RectangleAnnotation(id, box, style.Color, style.Width, style.FillColor),
            EditorTool.Ellipse => new EllipseAnnotation(id, box, style.Color, style.Width, style.FillColor),
            EditorTool.Redact => new RedactAnnotation(id, box, RedactMode.Solid, style.Color),
            EditorTool.Blur => new RedactAnnotation(id, box, RedactMode.Blur, Strength: style.Strength),
            EditorTool.Pixelate => new RedactAnnotation(id, box, RedactMode.Pixelate, Strength: style.Strength),
            _ => null,
        };
    }

    /// <summary>Shift: squares and circles for boxes, 45° steps for lines and arrows.</summary>
    private SKPoint Constrain(SKPoint a, SKPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (_tool is EditorTool.Arrow or EditorTool.Line)
        {
            var angle = MathF.Round(MathF.Atan2(dy, dx) / (MathF.PI / 4)) * (MathF.PI / 4);
            var length = MathF.Sqrt((dx * dx) + (dy * dy));
            return new SKPoint(a.X + (MathF.Cos(angle) * length), a.Y + (MathF.Sin(angle) * length));
        }

        var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return new SKPoint(a.X + (Math.Sign(dx) * side), a.Y + (Math.Sign(dy) * side));
    }

    private static bool IsBigEnough(AnnotationObject shape) => AnnotationGeometry.EndpointsOf(shape) is { } ends
        ? Distance(ends.From, ends.To) >= MinimumShapeSize * 2
        : AnnotationGeometry.BoxOf(shape) is { } box && box.Width >= MinimumShapeSize && box.Height >= MinimumShapeSize;

    private static AnnotationObject Restyle(AnnotationObject item, ToolStyle s) => item switch
    {
        ArrowAnnotation a => a with { Stroke = s.Color, StrokeWidth = s.Width, HeadStyle = s.HeadStyle },
        LineAnnotation l => l with { Stroke = s.Color, StrokeWidth = s.Width, Dashed = s.Dashed },
        RectangleAnnotation r => r with { Stroke = s.Color, StrokeWidth = s.Width, Fill = s.FillColor },
        EllipseAnnotation e => e with { Stroke = s.Color, StrokeWidth = s.Width, Fill = s.FillColor },
        PenAnnotation p => p with { Stroke = s.Color, StrokeWidth = s.Width },
        HighlighterAnnotation h => h with { Color = s.Color },
        TextAnnotation t => t with { Color = s.Color, FontSize = s.FontSize, Bold = s.Bold, Background = s.TextBackground },
        RedactAnnotation { Mode: RedactMode.Solid } x => x with { Fill = s.Color },
        RedactAnnotation x => x with { Strength = s.Strength },
        StepAnnotation st => st with { Fill = s.Color },
        _ => item,
    };

    private PixelRect? ClipToImage(PixelRect rect)
    {
        var clipped = rect.Intersect(new PixelRect(0, 0, Document.Canvas.Width, Document.Canvas.Height));
        return clipped.IsEmpty ? null : clipped;
    }

    private string PreviewOrNewId() => Preview?.Id ?? NewId();

    private static string NewId() => Guid.NewGuid().ToString("N")[..12];

    private static PixelPoint Round(SKPoint p) => new((int)MathF.Round(p.X), (int)MathF.Round(p.Y));

    private static float Distance(PixelPoint a, PixelPoint b) => SKPoint.Distance(new SKPoint(a.X, a.Y), new SKPoint(b.X, b.Y));

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
