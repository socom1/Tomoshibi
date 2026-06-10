using System;
using System.Diagnostics;

namespace Tomoshibi.Services;

/// <summary>
/// Desktop notifications by shelling out to what each OS provides:
/// osascript on macOS, notify-send on Linux. Windows toast notifications
/// need either a packaged app identity or a COM dance that isn't worth a
/// dependency yet, so Windows is a silent no-op for now — the chime still
/// covers the audible cue there.
/// </summary>
public class NotificationService : INotificationService
{
    public void Notify(string title, string body)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var script = $"display notification \"{Escape(body)}\" with title \"{Escape(title)}\"";
                Run("osascript", "-e", script);
            }
            else if (OperatingSystem.IsLinux())
            {
                Run("notify-send", title, body);
            }
        }
        catch
        {
            // A notification is never worth surfacing an error over.
        }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void Run(string command, params string[] args)
    {
        var psi = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        Process.Start(psi);
    }
}
