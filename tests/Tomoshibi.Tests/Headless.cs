using System;
using Avalonia;
using Avalonia.Headless;
using Tomoshibi;
using Xunit;

namespace Tomoshibi.Tests;

public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>One Avalonia for the whole test run.
///
/// <para>There can only be one: <c>Application.Current</c> is a singleton and
/// the UI thread is a single thread, so a second session would fight the
/// first. Everything that needs a dispatcher, a style stack or a control goes
/// through here.</para></summary>
public static class Headless
{
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));

    /// <summary>Run a body on Avalonia's UI thread and surface anything it
    /// throws to the calling test.</summary>
    public static void Run(Action body) =>
        Session.Dispatch(body, default).GetAwaiter().GetResult();
}

/// <summary>Holds the Avalonia-backed tests to one at a time.
///
/// <para>They already serialise on the single UI thread, but sharing a
/// collection keeps xUnit from interleaving them around it — and they share
/// more than a thread: application resources, the active theme, and whatever
/// the last test left in them.</para></summary>
[CollectionDefinition(Name)]
public class HeadlessCollection
{
    public const string Name = "avalonia headless";
}
