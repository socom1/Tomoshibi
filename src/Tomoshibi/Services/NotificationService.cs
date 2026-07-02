using System;
using System.Diagnostics;
using System.Linq;
#if WINDOWS10_0_17763_0_OR_GREATER
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace Tomoshibi.Services;

/// <summary>
/// Desktop notifications, per platform: native toasts on Windows (via the
/// notifications toolkit, which registers the app identity an unpackaged
/// EXE otherwise lacks), and shelling out to what the other OSes provide —
/// osascript on macOS, notify-send on Linux. The Windows path only exists
/// in the Windows-flavoured build; see the csproj.
/// </summary>
public class NotificationService : INotificationService
{
    public void Notify(string title, string body)
    {
        try
        {
#if WINDOWS10_0_17763_0_OR_GREATER
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show();
#else
            if (OperatingSystem.IsMacOS())
            {
                var script = $"display notification \"{Escape(body)}\" with title \"{Escape(title)}\"";
                Run("osascript", "-e", script);
            }
            else if (OperatingSystem.IsLinux())
            {
                Run("notify-send", title, body);
            }
#endif
        }
        catch
        {
            // A notification is never worth surfacing an error over.
        }
    }

#if !WINDOWS10_0_17763_0_OR_GREATER
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
#endif
}
