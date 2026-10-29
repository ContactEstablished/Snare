using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Snare.App.Infrastructure;
using Snare.App.Services;
using Snare.Capture;
using Snare.Core.Options;
using Snare.Library;
using Snare.Recording;
using Snare.Rendering;

namespace Snare.App;

/// <summary>
/// Snare runs as a tray app: no main window, shut down only from the tray menu.
/// Startup builds the Generic Host, wires every module, then shows the tray icon.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = SnareLogging.CreateLogger();
        RegisterGlobalExceptionHandlers();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Services.AddSerilog();
        builder.Services.Configure<SnareOptions>(builder.Configuration.GetSection(SnareOptions.SectionName));
        builder.Services
            .AddSnareCapture()
            .AddSnareRendering()
            .AddSnareRecording()
            .AddSnareLibrary();
        builder.Services.AddSingleton<TrayIconService>();
        builder.Services.AddSingleton<HotkeyService>();
        builder.Services.AddSingleton<ShellService>();

        _host = builder.Build();
        _host.Start();

        _host.Services.GetRequiredService<ShellService>().Start();
        Log.Information("Snare started");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        Log.Information("Snare stopped");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");

        // Keep the tray app alive; the log has the details.
        e.Handled = true;
    }
}
