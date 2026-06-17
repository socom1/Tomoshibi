using System;
using System.IO;
using Avalonia;

namespace Tomoshibi;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by the visual designer.
    [STAThread]
    public static void Main(string[] args)
    {
        // Last-ditch crash logging so an unhandled UI exception leaves a trace
        // on disk instead of just vanishing with the process.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
    }

    private static void LogCrash(Exception? ex)
    {
        if (ex is null)
            return;
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "tomoshibi-crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:O}]\n{ex}\n\n");
        }
        catch
        {
            // Logging a crash must never throw on top of it.
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
