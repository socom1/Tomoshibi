using System;
using System.IO;
using System.Linq;
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

    /// <summary>Write the crash where a user can actually find it: one file
    /// per crash next to tomoshibi.json (settings → open folder leads right
    /// there), stamped with the version and OS a bug report needs, keeping
    /// only the five most recent. The old location — a single appended file
    /// in the system temp folder — survived until temp was cleaned and was
    /// discoverable by nobody.</summary>
    private static void LogCrash(Exception? ex)
    {
        if (ex is null)
            return;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Tomoshibi");
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path,
                $"tomoshibi {ReleaseNotes.Version} · {Environment.OSVersion} · " +
                $".NET {Environment.Version}\n{DateTime.Now:O}\n\n{ex}\n");

            // Keep the five newest; a crash loop shouldn't fill the folder.
            var old = Directory.GetFiles(dir, "crash-*.log")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Skip(5);
            foreach (var stale in old)
                File.Delete(stale);
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
