using ScreenRecorderLib;
using Snare.Core.Geometry;
using Snare.Interop;

namespace Snare.Recording.Tests;

/// <summary>
/// Records the real screen, so it only runs when asked for explicitly
/// (<c>Snare.Recording.Tests.exe -explicit only</c>). No mic, no system audio, no webcam.
/// </summary>
public class RecorderSmokeTests
{
    [Fact(Explicit = true)]
    public async Task RecordsTwoSecondsOfThePrimaryMonitorAtTheFittedSize()
    {
        var primary = DisplayMonitors.GetAll().First(m => m.IsPrimary);
        var size = RecordingFrameSize.Fit(new PixelSize(primary.Bounds.Width, primary.Bounds.Height));
        var folder = Directory.CreateTempSubdirectory("snare-recorder-smoke-");
        var path = Path.Combine(folder.FullName, "smoke.mp4");

        try
        {
            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions { RecordingSources = [new DisplayRecordingSource(primary.DeviceName)] },
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    OutputFrameSize = new ScreenSize(size.Width, size.Height),
                    Stretch = StretchMode.Uniform,
                },
                AudioOptions = new AudioOptions { IsAudioEnabled = false },
                LogOptions = new LogOptions { IsLogEnabled = true, LogFilePath = Path.Combine(folder.FullName, "recorder.log"), LogSeverityLevel = ScreenRecorderLib.LogLevel.Debug },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Framerate = 30,
                    IsFixedFramerate = true,
                    IsHardwareEncodingEnabled = true,
                    Encoder = new H264VideoEncoder { BitrateMode = H264BitrateControlMode.Quality },
                },
            };

            var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var recorder = Recorder.CreateRecorder(options);
            recorder.OnRecordingComplete += (_, e) => done.TrySetResult(e.FilePath);
            recorder.OnRecordingFailed += (_, e) => done.TrySetException(new InvalidOperationException(e.Error));

            recorder.Record(path);
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            recorder.Stop();
            var saved = await done.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            var info = new FileInfo(saved);
            Assert.True(info.Exists);
            // The recorder keeps its log open until it is disposed.
            using var logStream = new FileStream(
                Path.Combine(folder.FullName, "recorder.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var log = new StreamReader(logStream).ReadToEnd();
            var notable = log.Split('\n').Where(l => l.Contains("ERROR") || l.Contains("WARN")).TakeLast(15).ToList();
            if (notable.Count > 0)
            {
                TestContext.Current.SendDiagnosticMessage(string.Join('\n', notable));
            }
            // A static screen compresses to a few KB, so check the stream, not the file size.
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(saved);
            var video = await file.Properties.GetVideoPropertiesAsync();
            TestContext.Current.SendDiagnosticMessage(
                $"Recorded {video.Width}x{video.Height}, {video.Duration.TotalSeconds:F1} s, {info.Length:N0} bytes");
            Assert.Equal((uint)size.Width, video.Width);
            Assert.Equal((uint)size.Height, video.Height);
            // The recorder takes up to a second to start, so 2 s of wall time can be ~1 s of video.
            Assert.InRange(video.Duration.TotalSeconds, 0.5, 4.0);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
