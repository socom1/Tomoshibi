using LibVLCSharp.Shared;

namespace Tomoshibi.Services;

/// <summary>
/// Playback of arbitrary audio/video files via libvlc — card sounds and clips,
/// and (on Windows) the music player. All members no-op safely when libvlc
/// isn't available, so callers can stay oblivious to platform support.
/// </summary>
public interface IMediaService
{
    /// <summary>True once libvlc initialised — false means playback is
    /// unavailable (e.g. Linux without the system package) and every call is a
    /// no-op.</summary>
    bool IsSupported { get; }

    /// <summary>Play a sound file, replacing whatever sound was playing.</summary>
    void PlayAudio(string path);

    /// <summary>Stop the current card sound.</summary>
    void StopAudio();

    /// <summary>A fresh player for a video view (caller owns/ disposes it), or
    /// null when unsupported.</summary>
    MediaPlayer? CreatePlayer();

    /// <summary>A media handle for a local file, or null when unsupported.</summary>
    Media? CreateMedia(string path);
}
