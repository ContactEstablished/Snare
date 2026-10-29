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
        if (key == Key.Tab || key == Key.Escape)
        {
            return; // keep moving between fields; Esc closes as everywhere else
        }

        e.Handled = true;
        Press(sender, key);
    }

    // Windows delivers PrtSc only as a key-up.
    private void OnHotkeyKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Snapshot)
        {
            e.Handled = true;
            Press(sender, key);
        }
    }

    private static void Press(object sender, Key key)
    {
        if (sender is FrameworkElement { Tag: HotkeyItemViewModel item })
        {
            item.Press(HotkeyCapture.FromKey(key, Keyboard.Modifiers), clear: key is Key.Back or Key.Delete);
        }
    }

    private sealed class NotConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    }
}
