using Windows.ApplicationModel;

namespace Snare.Interop;

/// <summary>
/// Start with Windows for the MSIX install, through the package's startup task (declared in
/// tools/msix/AppxManifest.xml). Packaged apps don't use the Run key.
/// </summary>
public static class PackagedStartup
{
    public const string TaskId = "SnareStartup";

    /// <summary>True when Snare runs from its MSIX package.</summary>
    public static bool IsPackaged
    {
        get
        {
            try
            {
                _ = Package.Current.Id;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    /// <summary>Turns the startup task on or off. Returns whether it's on now: Windows keeps it off if Matt turned it off in Task Manager.</summary>
    public static async Task<bool> SetAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync(TaskId);
        if (!enabled)
        {
            task.Disable();
            return false;
        }

        var state = task.State == StartupTaskState.Disabled ? await task.RequestEnableAsync() : task.State;
        return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }
}
