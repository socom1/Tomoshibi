using System;
using System.Runtime.InteropServices;

namespace Tomoshibi.Services;

/// <summary>
/// Native macOS notifications through UNUserNotificationCenter, hand-rolled
/// over the Objective-C runtime — no binding library for four calls. Posting
/// natively means the banner carries the app's own icon instead of Script
/// Editor's (what the osascript fallback shows, since osascript is the
/// sender there). Only possible from inside a .app bundle: the notification
/// center *throws* for bare executables (an objc exception no managed catch
/// survives), so <see cref="TryNotify"/> refuses first — checked via the
/// bundle identifier — and `dotnet run` keeps the osascript path.
///
/// A small delegate class is registered at runtime so banners still show
/// while the app is frontmost (the system default is to suppress them, but
/// the launch-time deadline reminders usually fire exactly then). The first
/// native post triggers the system's one-time permission prompt.
/// </summary>
internal static class MacNotifications
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string System_ = "/usr/lib/libSystem.dylib";
    private const string UserNotifications =
        "/System/Library/Frameworks/UserNotifications.framework/UserNotifications";

    // ---- objc runtime ----

    [DllImport(ObjC, EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC, EntryPoint = "sel_registerName")]
    private static extern IntPtr Sel([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr sel);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr sel, IntPtr a);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr sel, IntPtr a, IntPtr b);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr sel, IntPtr a, IntPtr b, IntPtr c);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr sel, ulong a, IntPtr b);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendStr(IntPtr receiver, IntPtr sel,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string utf8);

    [DllImport(ObjC)]
    private static extern IntPtr objc_allocateClassPair(IntPtr superclass,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nint extraBytes);

    [DllImport(ObjC)]
    private static extern void objc_registerClassPair(IntPtr cls);

    [DllImport(ObjC)]
    private static extern bool class_addMethod(IntPtr cls, IntPtr sel, IntPtr imp,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(System_)]
    private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);

    [DllImport(System_)]
    private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string symbol);

    private static readonly IntPtr RtldDefault = new(-2);

    // ---- blocks: the C ABI shape of an Objective-C block literal ----

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        public ulong Reserved;
        public ulong Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public IntPtr Isa;
        public int Flags;
        public int Reserved;
        public IntPtr Invoke;
        public IntPtr Descriptor;
    }

    private delegate void AuthHandler(IntPtr block, byte granted, IntPtr error);
    private delegate void WillPresentImp(IntPtr self, IntPtr sel, IntPtr center,
        IntPtr notification, IntPtr completionHandler);
    private delegate void PresentationBlockInvoke(IntPtr block, ulong options);

    // Held as statics so the GC never collects what native code will call.
    private static readonly AuthHandler AuthDone = static (_, _, _) => { };
    private static readonly WillPresentImp WillPresent = OnWillPresent;

    private static bool _initialized;
    private static bool _available;
    private static IntPtr _center;
    private static IntPtr _delegate;

    /// <summary>Post natively if this process can (bundled, center up).
    /// False means "use the fallback" — never throws out.</summary>
    internal static bool TryNotify(string title, string body)
    {
        try
        {
            if (!EnsureCenter())
                return false;

            var content = Send(Send(GetClass("UNMutableNotificationContent"),
                Sel("alloc")), Sel("init"));
            Send(content, Sel("setTitle:"), NSString(title));
            Send(content, Sel("setBody:"), NSString(body));

            // A nil trigger means "deliver now"; the id only needs to be unique.
            var request = Send(GetClass("UNNotificationRequest"),
                Sel("requestWithIdentifier:content:trigger:"),
                NSString(Guid.NewGuid().ToString()), content, IntPtr.Zero);

            Send(_center, Sel("addNotificationRequest:withCompletionHandler:"),
                request, IntPtr.Zero); // handler is documented nullable
            Send(content, Sel("release"));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>One-time setup: refuse outside a bundle (the center throws
    /// there, fatally), load the framework, install the foreground-banner
    /// delegate and ask for permission.</summary>
    private static bool EnsureCenter()
    {
        if (_initialized)
            return _available;
        _initialized = true;

        var bundle = Send(GetClass("NSBundle"), Sel("mainBundle"));
        if (bundle == IntPtr.Zero ||
            Send(bundle, Sel("bundleIdentifier")) == IntPtr.Zero)
            return false;

        if (dlopen(UserNotifications, 2 /* RTLD_NOW */) == IntPtr.Zero)
            return false;

        var cls = GetClass("UNUserNotificationCenter");
        if (cls == IntPtr.Zero)
            return false;

        _center = Send(cls, Sel("currentNotificationCenter"));
        if (_center == IntPtr.Zero)
            return false;

        InstallDelegate();
        RequestAuthorization();

        _available = true;
        return true;
    }

    /// <summary>By default macOS hides banners while the app is frontmost,
    /// but the deadline reminders fire right at launch — exactly then. A
    /// minimal delegate answers willPresent with "banner anyway".</summary>
    private static void InstallDelegate()
    {
        var cls = objc_allocateClassPair(GetClass("NSObject"),
            "TomoshibiNotificationDelegate", 0);
        if (cls == IntPtr.Zero)
            return; // name already registered — can't happen twice per process

        class_addMethod(cls,
            Sel("userNotificationCenter:willPresentNotification:withCompletionHandler:"),
            Marshal.GetFunctionPointerForDelegate(WillPresent),
            "v@:@@@?");
        objc_registerClassPair(cls);

        _delegate = Send(Send(cls, Sel("alloc")), Sel("init"));
        Send(_center, Sel("setDelegate:"), _delegate);
    }

    private static void OnWillPresent(IntPtr self, IntPtr sel, IntPtr center,
        IntPtr notification, IntPtr completionHandler)
    {
        // The handler is a block; its invoke pointer sits after isa+flags+reserved.
        var invoke = Marshal.ReadIntPtr(completionHandler, 16);
        var call = Marshal.GetDelegateForFunctionPointer<PresentationBlockInvoke>(invoke);

        const ulong bannerListSound = (1UL << 4) | (1UL << 3) | (1UL << 1);
        call(completionHandler, bannerListSound);
    }

    /// <summary>requestAuthorization's completion handler is not nullable, so
    /// a do-nothing global block is built by hand. Its few bytes are left to
    /// the process — the system may call into them at any point.</summary>
    private static void RequestAuthorization()
    {
        var descriptor = Marshal.AllocHGlobal(Marshal.SizeOf<BlockDescriptor>());
        Marshal.StructureToPtr(new BlockDescriptor
        {
            Size = (ulong)Marshal.SizeOf<BlockLiteral>()
        }, descriptor, false);

        var block = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
        Marshal.StructureToPtr(new BlockLiteral
        {
            Isa = dlsym(RtldDefault, "_NSConcreteGlobalBlock"),
            Flags = 1 << 28, // BLOCK_IS_GLOBAL
            Invoke = Marshal.GetFunctionPointerForDelegate(AuthDone),
            Descriptor = descriptor
        }, block, false);

        const ulong alertAndSound = (1UL << 2) | (1UL << 1);
        Send(_center, Sel("requestAuthorizationWithOptions:completionHandler:"),
            alertAndSound, block);
    }

    private static IntPtr NSString(string s) =>
        SendStr(GetClass("NSString"), Sel("stringWithUTF8String:"), s);
}
