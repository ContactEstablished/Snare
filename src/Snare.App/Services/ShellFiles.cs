using System.Diagnostics;
using System.IO;

namespace Snare.App.Services;

/// <summary>Hands files to Windows: open in the default app, or show in Explorer.</summary>
public static class ShellFiles
{
    public static void Open(string path)
    {
        if (File.Exists(path))
        {
            using var _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    public static void Reveal(string path)
    {
        var arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{Path.GetDirectoryName(path)}\"";
        using var _ = Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
    }
}
