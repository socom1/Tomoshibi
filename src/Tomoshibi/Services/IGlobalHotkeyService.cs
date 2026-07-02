using System;

namespace Tomoshibi.Services;

/// <summary>
/// A system-wide start/pause hotkey — one chord that toggles the timer from
/// any app, even with tomoshibi tucked away in the tray. Platform
/// implementations: Win32 RegisterHotKey on Windows, Carbon
/// RegisterEventHotKey on macOS (the one global-hotkey API there that needs
/// no accessibility permission). Linux stays behind the interface for now —
/// X11 grabs don't survive Wayland, so it ships as unsupported.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>Whether this build/OS can claim a system-wide hotkey at all.</summary>
    bool IsSupported { get; }

    /// <summary>The chord, written the way the OS writes it — for settings copy.</summary>
    string ChordLabel { get; }

    /// <summary>Try to claim the chord; <paramref name="onPressed"/> fires on
    /// the UI thread. False when another app already holds it.</summary>
    bool Register(Action onPressed);

    /// <summary>Release the chord so other apps can have it back.</summary>
    void Unregister();
}

/// <summary>The no-op stand-in for platforms without an implementation.</summary>
public class NullHotkeyService : IGlobalHotkeyService
{
    public bool IsSupported => false;
    public string ChordLabel => string.Empty;
    public bool Register(Action onPressed) => false;
    public void Unregister() { }
    public void Dispose() { }
}
