using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace Tomoshibi.Services;

/// <summary>
/// Local-file playback. On macOS (afplay) and Linux with mpv/ffplay it shells
/// out, pausing via SIGSTOP/SIGCONT — crude, dependency-free, good enough for a
/// lofi folder. Everywhere else (notably Windows, and Linux without those
/// players) it uses the shared libvlc backend, which finally gives Windows a
/// working player.
/// </summary>
public class MusicService : IMusicService
{
    private readonly VlcMediaService? _vlc;

    private Process? _player;
    private bool _stopRequested;

    // libvlc path
    private MediaPlayer? _vlcPlayer;
    private Media? _vlcMedia;

    public MusicService(VlcMediaService? vlc = null) => _vlc = vlc;

    public event Action? TrackEnded;

    /// <summary>Use libvlc when there's no native shell-out player for this OS
    /// (Windows always; Linux without mpv/ffplay) and libvlc is available.</summary>
    private bool UseVlc => !OperatingSystem.IsMacOS()
                           && FindLinuxPlayer() is null
                           && _vlc is { IsSupported: true };

    public bool IsSupported => OperatingSystem.IsMacOS()
                               || FindLinuxPlayer() is not null
                               || _vlc is { IsSupported: true };

    public void Play(string path, double volume)
    {
        if (UseVlc)
        {
            PlayVlc(path, volume);
            return;
        }

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

    public void Pause()
    {
        if (UseVlc) { try { _vlcPlayer?.SetPause(true); } catch { } return; }
        Signal("-STOP");
    }

    public void Resume()
    {
        if (UseVlc) { try { _vlcPlayer?.SetPause(false); } catch { } return; }
        Signal("-CONT");
    }

    public void Stop()
    {
        if (UseVlc) { StopVlc(); return; }

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

    // ---- libvlc backend ----

    private void PlayVlc(string path, double volume)
    {
        StopVlc();
        if (!File.Exists(path)) return;

        try
        {
            _vlcPlayer ??= _vlc!.CreatePlayer();
            if (_vlcPlayer is null) return;

            _vlcPlayer.EndReached -= OnVlcEndReached;
            _vlcPlayer.EndReached += OnVlcEndReached;
            _vlcPlayer.Volume = (int)Math.Clamp(volume, 0, 100);

            _vlcMedia?.Dispose();
            _vlcMedia = _vlc!.CreateMedia(path);
            if (_vlcMedia is null) return;

            _stopRequested = false;
            _vlcPlayer.Play(_vlcMedia);
        }
        catch { /* best effort */ }
    }

    private void StopVlc()
    {
        _stopRequested = true;
        try { _vlcPlayer?.Stop(); } catch { }
        try { _vlcMedia?.Dispose(); } catch { }
        _vlcMedia = null;
    }

    /// <summary>Fires on a libvlc thread — hop off it before advancing (calling
    /// back into the player from its own callback can deadlock).</summary>
    private void OnVlcEndReached(object? sender, EventArgs e)
    {
        if (_stopRequested) return;
        Task.Run(() => TrackEnded?.Invoke());
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
