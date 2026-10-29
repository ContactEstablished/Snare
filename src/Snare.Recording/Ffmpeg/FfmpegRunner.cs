using System.Diagnostics;
using System.Text;

namespace Snare.Recording.Ffmpeg;

/// <summary>Runs ffmpeg.exe with an argument list (no shell), off the UI thread, and collects stderr.</summary>
public static class FfmpegRunner
{
    /// <param name="allowFailure">Probe runs exit non-zero by design; return their output instead of throwing.</param>
    public static async Task<string> RunAsync(string ffmpeg, IReadOnlyList<string> arguments, bool allowFailure = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpeg);
        ArgumentNullException.ThrowIfNull(arguments);

        var start = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stderr)
                {
                    stderr.AppendLine(e.Data);
                }
            }
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // A broken path or a dangling winget link: report it like any other FFmpeg failure.
            throw new FfmpegException($"Could not start FFmpeg at {ffmpeg}: {ex.Message}", ex);
        }

        process.StandardInput.Close(); // never wait for a keypress
        process.BeginErrorReadLine();
        _ = process.StandardOutput.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // already exited
            }

            throw;
        }

        string output;
        lock (stderr)
        {
            output = stderr.ToString();
        }

        if (process.ExitCode != 0 && !allowFailure)
        {
            var tail = string.Join('\n', output.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(6));
            throw new FfmpegException($"FFmpeg exited with code {process.ExitCode}: {tail}");
        }

        return output;
    }
}
