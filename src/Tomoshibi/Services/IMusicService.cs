using System;

namespace Tomoshibi.Services;

/// <summary>
/// Plays local audio files behind an interface. The implementation shells
/// out to the OS player and pauses with process signals, so the app stays
/// dependency-free; the trade-off is that volume only applies when a track
/// (re)starts.
/// </summary>
public interface IMusicService
{
    /// <summary>True when this OS has a player the service knows how to drive.</summary>
    bool IsSupported { get; }

    /// <summary>Raised when a track finishes on its own (not stopped).</summary>
    event Action? TrackEnded;

    /// <summary>Start a file from the beginning at volume 0–100.</summary>
    void Play(string path, double volume);

    /// <summary>Suspend / resume the current track in place.</summary>
    void Pause();
    void Resume();

    /// <summary>Stop and forget the current track.</summary>
    void Stop();
}
