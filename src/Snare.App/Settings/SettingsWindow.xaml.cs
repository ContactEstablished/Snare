using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;

namespace Snare.App.Settings;

/// <summary>View wiring for Settings: hotkey boxes capture key presses, folder and file dialogs, closing.</summary>
internal sealed partial class SettingsWindow : Window
{
    public static readonly IValueConverter Not = new NotConverter();

    // Modifiers seen while the combination is held: Windows reports PrtSc only on key-up, by
    // which time Ctrl may already be released.
    private ModifierKeys _held;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
        viewModel.PickFolder = initial =>
        {
            var dialog = new OpenFolderDialog { InitialDirectory = initial };
            return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
        };
        viewModel.PickFfmpeg = () =>
        {
            var dialog = new OpenFileDialog { Filter = "ffmpeg.exe|ffmpeg.exe|Programs (*.exe)|*.exe", CheckFileExists = true };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        };
    }

    private void OnHotkeyKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab)
        {
            return; // keep moving between fields
        }

        e.Handled = true; // Esc too: in a hotkey box it keeps the old hotkey rather than closing Settings
        _held = Keyboard.Modifiers; // the latest set held down, kept for a PrtSc that arrives on key-up
        if (key != Key.Escape && HotkeyCapture.FromKey(key, Keyboard.Modifiers) is not null || key is Key.Back or Key.Delete)
        {
            Press(sender, key, Keyboard.Modifiers);
            _held = ModifierKeys.None;
        }
    }

    // Windows delivers PrtSc only as a key-up.
    private void OnHotkeyKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Snapshot)
        {
            e.Handled = true;
            Press(sender, key, Keyboard.Modifiers | _held);
            _held = ModifierKeys.None;
        }
    }

    private static void Press(object sender, Key key, ModifierKeys modifiers)
    {
        if (sender is FrameworkElement { Tag: HotkeyItemViewModel item })
        {
            item.Press(HotkeyCapture.FromKey(key, modifiers), clear: key is Key.Back or Key.Delete);
        }
    }

    private sealed class NotConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    }
}
