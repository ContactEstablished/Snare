using Microsoft.Extensions.DependencyInjection;

namespace Snare.App.Library;

/// <summary>Opens the library window, or brings the open one to the front.</summary>
public sealed class LibraryWindowService(IServiceProvider services)
{
    private LibraryWindow? _window;

    public void Show()
    {
        if (_window is { IsLoaded: true })
        {
            if (_window.WindowState == System.Windows.WindowState.Minimized)
            {
                _window.WindowState = System.Windows.WindowState.Normal;
            }

            _window.Activate();
            return;
        }

        _window = new LibraryWindow(ActivatorUtilities.CreateInstance<LibraryViewModel>(services));
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }
}
