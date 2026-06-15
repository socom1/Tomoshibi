using System;
using System.Diagnostics;
using System.Linq;

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

    /// <summary>
    /// Make a string safe to embed in an osascript double-quoted literal.
    /// Control characters (newlines especially) can break out of the literal,
    /// so they're stripped before escaping the backslash and quote. Length is
    /// capped as belt-and-braces — today only fixed strings reach here, but
    /// this hardens it should user content (a task title) ever be notified.
    /// </summary>
    private static string Escape(string s)
    {
        var cleaned = new string((s ?? string.Empty).Where(c => !char.IsControl(c)).ToArray());
        if (cleaned.Length > 200)
            cleaned = cleaned[..200];
        return cleaned.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

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
