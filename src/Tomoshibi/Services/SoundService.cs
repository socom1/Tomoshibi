using System;
using System.Diagnostics;
using System.IO;

namespace Tomoshibi.Services;

/// <summary>
/// Plays the bundled chime by shelling out to the OS audio player —
/// afplay on macOS, paplay/aplay on Linux, PowerShell's SoundPlayer on
/// Windows. No audio NuGet dependency, and a missing player or file just
/// means silence: sound is never worth crashing over.
/// </summary>
public class SoundService : ISoundService
{
    private readonly string _chimePath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "chime.wav");

    public void PlayPhaseChime()
    {
        if (!File.Exists(_chimePath))
            return;

        try
        {
            if (OperatingSystem.IsMacOS())
            {
                Run("afplay", _chimePath);
            }
            else if (OperatingSystem.IsWindows())
            {
                Run("powershell",
                    "-NoProfile", "-Command",
                    $"(New-Object Media.SoundPlayer '{_chimePath}').PlaySync()");
            }
            else
            {
                try { Run("paplay", _chimePath); }
                catch { Run("aplay", _chimePath); }
            }
        }
        catch
        {
            // No player available — stay silent rather than surface an error.
        }
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
