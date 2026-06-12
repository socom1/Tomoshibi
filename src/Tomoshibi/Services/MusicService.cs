using System;
using System.Diagnostics;
using System.IO;

namespace Tomoshibi.Services;

/// <summary>
/// Local-file playback by shelling out: afplay on macOS, mpv/ffplay on
/// Linux when present. Pause/resume is SIGSTOP/SIGCONT on the player
/// process — crude, dependency-free, and good enough for a lofi folder.
/// Windows has no equivalent signal trick, so it reports unsupported
/// until a real audio backend earns its place.
/// </summary>
public class MusicService : IMusicService
{
    private Process? _player;
    private bool _stopRequested;

    public event Action? TrackEnded;

    public bool IsSupported => OperatingSystem.IsMacOS() ||
                               (OperatingSystem.IsLinux() && FindLinuxPlayer() is not null);

    public void Play(string path, double volume)
    {
        Stop();

        if (!File.Exists(path))
            return;

        try
        {
            ProcessStartInfo psi;
            var clamped = Math.Clamp(volume, 0, 100);

            if (OperatingSystem.IsMacOS())
            {
                psi = new ProcessStartInfo("afplay");
                psi.ArgumentList.Add("-v");
                psi.ArgumentList.Add((clamped / 100.0).ToString("0.00",
                    System.Globalization.CultureInfo.InvariantCulture));
                psi.ArgumentList.Add(path);
            }
            else if (FindLinuxPlayer() is { } player)
            {
                psi = new ProcessStartInfo(player);
                if (player == "mpv")
                {
                    psi.ArgumentList.Add("--no-video");
                    psi.ArgumentList.Add($"--volume={clamped:0}");
                }
                else // ffplay
                {
                    psi.ArgumentList.Add("-nodisp");
                    psi.ArgumentList.Add("-autoexit");
                    psi.ArgumentList.Add("-volume");
                    psi.ArgumentList.Add($"{clamped:0}");
                }
                psi.ArgumentList.Add(path);
            }
            else
            {
                return;
            }

            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            _stopRequested = false;
            _player = Process.Start(psi);
            if (_player is not null)
            {
                _player.EnableRaisingEvents = true;
                _player.Exited += (_, _) =>
                {
                    if (!_stopRequested)
                        TrackEnded?.Invoke();
                };
            }
        }
        catch
        {
            _player = null;
        }
    }

    public void Pause() => Signal("-STOP");
    public void Resume() => Signal("-CONT");

    public void Stop()
    {
        if (_player is null)
            return;

        _stopRequested = true;
        try
        {
            // A paused (SIGSTOPped) process can't die until it's resumed.
            Signal("-CONT");
            if (!_player.HasExited)
                _player.Kill();
        }
        catch
        {
            // Already gone is fine.
        }
        _player = null;
    }

    private void Signal(string signal)
    {
        if (_player is null || _player.HasExited)
            return;

        try
        {
            Process.Start(new ProcessStartInfo("/bin/kill")
            {
                ArgumentList = { signal, _player.Id.ToString() },
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            // Best effort.
        }
    }

    private static string? FindLinuxPlayer()
    {
        foreach (var candidate in new[] { "mpv", "ffplay" })
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
            {
                if (File.Exists(Path.Combine(dir, candidate)))
                    return candidate;
            }
        }
        return null;
    }
}
