using System;
using System.IO;
using LibVLCSharp.Shared;

namespace Tomoshibi.Services;

/// <summary>
/// libvlc-backed media playback. The native library is initialised lazily on
/// first use (off the startup path) and wrapped in try/catch, so a machine
/// without libvlc simply reports <see cref="IsSupported"/> = false and every
/// call becomes a no-op. One shared player handles card sounds; video views and
/// the music player get their own players from <see cref="CreatePlayer"/>.
/// </summary>
public sealed class VlcMediaService : IMediaService, IDisposable
{
    private LibVLC? _libVlc;
    private MediaPlayer? _audioPlayer;
    private bool _initialised;
    private bool _ok;

    public bool IsSupported
    {
        get { EnsureInitialised(); return _ok; }
    }

    /// <summary>The shared library handle (for the music backend), or null.</summary>
    public LibVLC? Library
    {
        get { EnsureInitialised(); return _libVlc; }
    }

    private void EnsureInitialised()
    {
        if (_initialised) return;
        _initialised = true;
        try
        {
            Core.Initialize();
            _libVlc = new LibVLC();
            _ok = true;
        }
        catch
        {
            _ok = false; // no libvlc on this machine — degrade quietly
        }
    }

    public void PlayAudio(string path)
    {
        EnsureInitialised();
        if (!_ok || _libVlc is null || !File.Exists(path)) return;
        try
        {
            _audioPlayer ??= new MediaPlayer(_libVlc);
            _audioPlayer.Stop();
            using var media = new Media(_libVlc, path, FromType.FromPath);
            _audioPlayer.Play(media);
        }
        catch { /* best effort */ }
    }

    public void StopAudio()
    {
        try { _audioPlayer?.Stop(); }
        catch { /* already stopped */ }
    }

    public MediaPlayer? CreatePlayer()
    {
        EnsureInitialised();
        if (!_ok || _libVlc is null) return null;
        try { return new MediaPlayer(_libVlc); }
        catch { return null; }
    }

    public Media? CreateMedia(string path)
    {
        EnsureInitialised();
        if (!_ok || _libVlc is null) return null;
        try { return new Media(_libVlc, path, FromType.FromPath); }
        catch { return null; }
    }

    public void Dispose()
    {
        try { _audioPlayer?.Dispose(); } catch { /* ignore */ }
        try { _libVlc?.Dispose(); } catch { /* ignore */ }
    }
}
