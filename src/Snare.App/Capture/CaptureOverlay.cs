using System.Windows;
using Snare.Capture.Selection;
using Snare.Core.Captures;

namespace Snare.App.Capture;

/// <summary>Shows the capture overlay on every monitor and returns what Matt selected.</summary>
public sealed class CaptureOverlay
{
    private bool _open;

    public bool IsOpen => _open;

    /// <summary>Returns the accepted selection, or null if cancelled. Call on the UI thread.</summary>
    public async Task<SelectionResult?> SelectAsync(FrozenDesktop desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        if (_open)
        {
            return null;
        }

        _open = true;
        var viewModel = new CaptureOverlayViewModel(desktop);
        var windows = desktop.Monitors.Select(m => new CaptureOverlayWindow(viewModel, m)).ToList();
        try
        {
            foreach (var window in windows)
            {
                // Closing any overlay (Alt+F4) cancels the whole capture.
                window.Closed += (_, _) => viewModel.CancelCommand.Execute(null);
                window.Show();
            }

            // Keyboard focus goes to the overlay under the cursor.
            var underCursor = desktop.MonitorAt(desktop.Cursor);
            var index = underCursor is null ? 0 : Math.Max(0, desktop.Monitors.ToList().IndexOf(underCursor));
            var focused = windows[index];
            focused.Activate();
            focused.Focus();

            return await viewModel.Completion;
        }
        finally
        {
            foreach (var window in windows)
            {
                if (window.IsLoaded)
                {
                    window.Close();
                }
            }

            _open = false;
        }
    }
}
