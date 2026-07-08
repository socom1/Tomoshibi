using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Win32;

namespace Tomoshibi.Services;

/// <summary>
/// The Windows hotkey: Win32 RegisterHotKey against the main window's
/// handle, with the WM_HOTKEY press caught through Avalonia's wndproc hook.
/// Both arrive on the UI thread, so the callback runs there for free.
/// </summary>
public class WindowsHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x544F; // arbitrary, unique within the app

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000; // holding the chord fires once
    private const uint VkP = 0x50;
    private const uint WmHotkey = 0x0312;

    private readonly Window _window;
    private Action? _onPressed;
    private bool _hooked;
    private bool _registered;

    public bool IsSupported => true;
    public string ChordLabel => "ctrl+alt+P";

    public WindowsHotkeyService(Window window) => _window = window;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public bool Register(Action onPressed)
    {
        var handle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return false;

        Unregister();
        _onPressed = onPressed;

        if (!_hooked)
        {
            Win32Properties.AddWndProcHookCallback(_window, WndProcHook);
            _hooked = true;
        }

        _registered = RegisterHotKey(handle, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkP);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
            return;

        if (_window.TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero)
            UnregisterHotKey(handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt64() == HotkeyId)
        {
            _onPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
