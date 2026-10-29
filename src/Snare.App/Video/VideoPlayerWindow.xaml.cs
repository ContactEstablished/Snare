using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Snare.App.Video;

/// <summary>
/// View wiring for the player: MediaElement playback and seeking (not bindable), keyboard
/// shortcuts, dialogs, and releasing the file while FFmpeg replaces it.
/// </summary>
internal sealed partial class VideoPlayerWindow : Window
{
    private readonly VideoPlayerViewModel _viewModel;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool _playing;
    private bool _seeking;

    public VideoPlayerWindow(VideoPlayerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.CurrentPosition = () => Player.Position;
        viewModel.Confirm = message => MessageBox.Show(this, message, "Trim video", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK;
        viewModel.PickGifPath = PickGifPath;
        viewModel.WithMediaReleased = WithMediaReleasedAsync;

        Player.MediaOpened += (_, _) =>
        {
            Seek.Maximum = Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan.TotalSeconds : 0;
            UpdateTime();
        };
        Player.MediaEnded += (_, _) => SetPlaying(false);
        PlayButton.Click += (_, _) => SetPlaying(!_playing);
        Seek.PreviewMouseLeftButtonDown += (_, _) => _seeking = true;
        Seek.PreviewMouseLeftButtonUp += (_, _) =>
        {
            Player.Position = TimeSpan.FromSeconds(Seek.Value);
            _seeking = false;
        };
        Seek.ValueChanged += (_, _) =>
        {
            if (_seeking)
            {
                Player.Position = TimeSpan.FromSeconds(Seek.Value);
                UpdateTime();
            }
        };
        _timer.Tick += (_, _) =>
        {
            if (!_seeking)
            {
                Seek.Value = Player.Position.TotalSeconds;
            }

            UpdateTime();
        };

        Loaded += (_, _) =>
        {
            Open();
            SetPlaying(true);
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            Player.Close();
        };
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        switch (e.Key)
        {
            case Key.Space:
                SetPlaying(!_playing);
                break;
            case Key.OemOpenBrackets:
                _viewModel.SetStartCommand.Execute(null);
                break;
            case Key.OemCloseBrackets:
                _viewModel.SetEndCommand.Execute(null);
                break;
            case Key.Left:
                Player.Position -= TimeSpan.FromSeconds(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5 : 1);
                break;
            case Key.Right:
                Player.Position += TimeSpan.FromSeconds(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5 : 1);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Open() => Player.Source = new Uri(_viewModel.Capture.FilePath);

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        if (playing)
        {
            Player.Play();
        }
        else
        {
            Player.Pause();
        }

        PlayButton.Content = playing ? "❚❚" : "▶";
    }

    private void UpdateTime() =>
        TimeText.Text = $"{VideoPlayerViewModel.Format(Player.Position)} / {VideoPlayerViewModel.Format(_viewModel.Duration)}";

    /// <summary>MediaElement keeps the file open; close it while FFmpeg replaces the file, then reopen.</summary>
    private async Task WithMediaReleasedAsync(Func<Task> work)
    {
        SetPlaying(false);
        Player.Close();
        Player.Source = null;
        try
        {
            await work();
        }
        finally
        {
            Open();
            Player.Position = TimeSpan.Zero;
            Player.Pause();
        }
    }

    private string? PickGifPath(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            Filter = "GIF animation (*.gif)|*.gif",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }
}
