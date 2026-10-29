using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Snare.Core.Annotations;
using Snare.Rendering.Editing;

namespace Snare.App.Editor;

/// <summary>
/// View wiring for the editor: keyboard shortcuts, the inline text box, dialogs, drag-out, and
/// saving on close. Editing state lives in <see cref="EditorViewModel"/> and its session.
/// </summary>
internal sealed partial class EditorWindow : Window
{
    private readonly EditorViewModel _viewModel;
    private readonly EditorCanvas _canvas;
    private TextAnnotation? _editingText;
    private bool _closingAfterSave;

    public EditorWindow(EditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _canvas = new EditorCanvas(viewModel);
        CanvasHost.Children.Insert(0, _canvas);

        viewModel.PickSaveAsPath = PickSaveAsPath;
        viewModel.PickSize = (current, natural) =>
        {
            var dialog = new ResizeWindow(new ResizeViewModel(current, natural)) { Owner = this };
            dialog.ShowDialog();
            return dialog.Result;
        };
        viewModel.ConfirmDiscardOriginal = ConfirmDiscardOriginal;
        viewModel.TextEditRequested += (_, text) => BeginTextEdit(text);
        viewModel.CloseRequested += (_, _) => Close();
        viewModel.PropertyChanged += (_, _) => SyncToolButtons();
        SyncToolButtons();

        TextEditor.LostKeyboardFocus += (_, _) => CommitTextEdit();
        TextEditor.PreviewKeyDown += OnTextEditorKey;
        DragHandle.MouseLeftButtonDown += async (_, _) => await DragOutAsync();
        Loaded += (_, _) => _canvas.Focus();
        Closing += OnClosing;
        Closed += (_, _) => viewModel.Dispose();
    }

    private void SyncToolButtons()
    {
        foreach (var button in ToolButtons.Children.OfType<RadioButton>())
        {
            button.IsChecked = Equals(button.Tag, _viewModel.Tool);
        }
    }

    // ---- Keyboard -----------------------------------------------------------------------------

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (TextEditor.IsKeyboardFocused)
        {
            return;
        }

        var session = _viewModel.Session;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        e.Handled = true;

        switch (e.Key)
        {
            case Key.Space:
                _canvas.SpaceHeld = true;
                break;
            case Key.Z when ctrl && shift:
            case Key.Y when ctrl:
                session.Redo();
                break;
            case Key.Z when ctrl:
                session.Undo();
                break;
            case Key.A when ctrl:
                session.SelectAll();
                break;
            case Key.C when ctrl:
                // Ctrl+C (or Ctrl+Shift+C) copies the flattened image; there is no object clipboard.
                _viewModel.CopyImageCommand.Execute(null);
                break;
            case Key.S when ctrl && shift:
                _viewModel.SaveAsCommand.Execute(null);
                break;
            case Key.S when ctrl:
                _viewModel.SaveCommand.Execute(null);
                break;
            case Key.D0 or Key.NumPad0 when ctrl:
                _canvas.ZoomToFit();
                break;
            case Key.D1 or Key.NumPad1 when ctrl:
                _canvas.ZoomToActualSize();
                break;
            case Key.OemCloseBrackets when ctrl:
                if (shift)
                {
                    session.BringToFront();
                }
                else
                {
                    session.BringForward();
                }

                break;
            case Key.OemOpenBrackets when ctrl:
                if (shift)
                {
                    session.SendToBack();
                }
                else
                {
                    session.SendBackward();
                }

                break;
            case Key.Delete or Key.Back:
                session.DeleteSelection();
                break;
            case Key.Escape:
                session.Escape();
                break;
            case Key.Enter when session.Tool == EditorTool.Crop:
                session.ApplyCrop();
                break;
            case Key.Left or Key.Right or Key.Up or Key.Down:
                var step = shift ? 10 : 1;
                session.Nudge(
                    e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0,
                    e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0);
                break;
            default:
                e.Handled = !ctrl && TrySelectToolByLetter(e.Key);
                break;
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (e.Key == Key.Space)
        {
            _canvas.SpaceHeld = false;
        }
    }

    private bool TrySelectToolByLetter(Key key)
    {
        var letter = key.ToString();
        if (letter.Length == 1 && EditorShortcuts.ByKey.TryGetValue(letter[0], out var tool))
        {
            _viewModel.Tool = tool;
            return true;
        }

        return false;
    }

    // ---- Inline text editing ----------------------------------------------------------------------

    private void BeginTextEdit(TextAnnotation text)
    {
        CommitTextEdit();
        _editingText = text;

        var at = _canvas.ImageToDip(text.Position);
        Canvas.SetLeft(TextEditor, at.X);
        Canvas.SetTop(TextEditor, at.Y);
        TextEditor.FontFamily = new FontFamily(text.FontFamily);
        TextEditor.FontSize = Math.Max(8, text.FontSize * _canvas.DipsPerImagePixel);
        TextEditor.FontWeight = text.Bold ? FontWeights.Bold : FontWeights.Normal;
        TextEditor.Foreground = (Brush?)new BrushConverter().ConvertFromString(text.Color) ?? Brushes.White;
        TextEditor.Text = text.Text;
        TextEditor.Visibility = Visibility.Visible;
        TextEditor.Focus();
        TextEditor.SelectAll();
    }

    private void OnTextEditorKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)))
        {
            e.Handled = true;
            _canvas.Focus(); // losing focus commits
        }
    }

    private void CommitTextEdit()
    {
        if (_editingText is not { } target)
        {
            return;
        }

        _editingText = null;
        TextEditor.Visibility = Visibility.Collapsed;
        _viewModel.Session.CommitText(target, TextEditor.Text);
    }

    // ---- Dialogs, drag-out, closing ---------------------------------------------------------------

    private string? PickSaveAsPath(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private bool ConfirmDiscardOriginal() => MessageBox.Show(
        this,
        "Discard the original? The image will be saved with your edits baked in, and the untouched original " +
        "will be deleted. Redactions become permanent, and this capture can't be edited again.",
        "Discard original",
        MessageBoxButton.OKCancel,
        MessageBoxImage.Warning,
        MessageBoxResult.Cancel) == MessageBoxResult.OK;

    /// <summary>Saves pending edits, then drags the export file out, if the button is still held.</summary>
    private async Task DragOutAsync()
    {
        if (_viewModel.NeedsExport && !await _viewModel.SaveAsync())
        {
            return; // don't drag out a stale image
        }

        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            var data = new DataObject(DataFormats.FileDrop, new[] { _viewModel.Capture.FilePath });
            DragDrop.DoDragDrop(DragHandle, data, DragDropEffects.Copy);
        }
    }

    /// <summary>
    /// Tray → Exit: finishes typing, saves unexported edits (which also keeps them), then lets the
    /// window close without asking. Shutdown ignores a cancelled Closing, so this runs first.
    /// </summary>
    public async Task SaveBeforeExitAsync()
    {
        CommitTextEdit();
        if (_viewModel.NeedsExport)
        {
            await _viewModel.SaveAsync();
        }

        _closingAfterSave = true;
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        // async void: a window event; SaveAsync reports its own failures in the status bar.
        CommitTextEdit();
        if (_closingAfterSave || !_viewModel.NeedsExport)
        {
            return;
        }

        e.Cancel = true;
        if (await _viewModel.SaveAsync())
        {
            _closingAfterSave = true;
            Close();
            return;
        }

        // The image couldn't be updated: stay open so nothing is lost. A second close asks.
        var answer = MessageBox.Show(
            this,
            $"{_viewModel.Status}\n\nYour edits are kept and will be there next time you open this capture, " +
            "but the library image wasn't updated. Close anyway?",
            "Couldn't save the image",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            _closingAfterSave = true;
            Close();
        }
    }
}
