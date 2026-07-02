using System;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace Tomoshibi.Services;

/// <summary>
/// The macOS hotkey: Carbon's RegisterEventHotKey — venerable, but the one
/// global-hotkey API that needs no accessibility permission. The handler is
/// installed on the application event target and fires on the main run loop
/// Avalonia already drives; the press is posted to the dispatcher anyway as
/// belt-and-braces. The chord is ⌃⌥P (control+option+P), chosen to stay
/// clear of the system's input-source shortcuts on ⌃Space.
/// </summary>
public class MacHotkeyService : IGlobalHotkeyService
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    private const uint EventClassKeyboard = 0x6B657962; // 'keyb'
    private const uint EventHotKeyPressed = 5;
    private const uint ControlKey = 0x1000;
    private const uint OptionKey = 0x0800;
    private const uint KeyCodeP = 0x23; // kVK_ANSI_P — a position, not a letter
    private const uint Signature = 0x544F4D4F; // 'TOMO'

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint SignatureCode;
        public uint Id;
    }

    private delegate int EventHandlerProc(IntPtr nextHandler, IntPtr theEvent, IntPtr userData);

    [DllImport(Carbon)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(Carbon)]
    private static extern int InstallEventHandler(IntPtr target, EventHandlerProc handler,
        uint count, ref EventTypeSpec types, IntPtr userData, out IntPtr handlerRef);

    [DllImport(Carbon)]
    private static extern int RemoveEventHandler(IntPtr handlerRef);

    [DllImport(Carbon)]
    private static extern int RegisterEventHotKey(uint keyCode, uint modifiers,
        EventHotKeyID hotKeyId, IntPtr target, uint options, out IntPtr hotKeyRef);

    [DllImport(Carbon)]
    private static extern int UnregisterEventHotKey(IntPtr hotKeyRef);

    // Held as a field so the GC can't collect the delegate Carbon calls into.
    private EventHandlerProc? _handlerProc;
    private IntPtr _handlerRef;
    private IntPtr _hotKeyRef;
    private Action? _onPressed;

    public bool IsSupported => true;
    public string ChordLabel => "⌃⌥P";

    public bool Register(Action onPressed)
    {
        Unregister();
        _onPressed = onPressed;

        if (_handlerRef == IntPtr.Zero)
        {
            _handlerProc ??= HandleHotkey;
            var type = new EventTypeSpec
            {
                EventClass = EventClassKeyboard,
                EventKind = EventHotKeyPressed
            };

            if (InstallEventHandler(GetApplicationEventTarget(), _handlerProc, 1,
                    ref type, IntPtr.Zero, out _handlerRef) != 0)
                return false;
        }

        var id = new EventHotKeyID { SignatureCode = Signature, Id = 1 };
        return RegisterEventHotKey(KeyCodeP, ControlKey | OptionKey, id,
                   GetApplicationEventTarget(), 0, out _hotKeyRef) == 0;
    }

    public void Unregister()
    {
        if (_hotKeyRef == IntPtr.Zero)
            return;

        UnregisterEventHotKey(_hotKeyRef);
        _hotKeyRef = IntPtr.Zero;
    }

    private int HandleHotkey(IntPtr nextHandler, IntPtr theEvent, IntPtr userData)
    {
        if (_onPressed is { } pressed)
            Dispatcher.UIThread.Post(pressed);
        return 0; // noErr — the press is ours
    }

    public void Dispose()
    {
        Unregister();

        if (_handlerRef != IntPtr.Zero)
        {
            RemoveEventHandler(_handlerRef);
            _handlerRef = IntPtr.Zero;
        }
    }
}
