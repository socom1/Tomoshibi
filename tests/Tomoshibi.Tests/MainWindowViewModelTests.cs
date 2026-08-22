using System;
using System.Collections.Generic;
using System.IO;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Tomoshibi.ViewModels;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The shell: the one view model nothing covered, because building it
/// needs a dispatcher for its midnight timer and a directory for the review log
/// and media store. The headless session supplies the first and a temp folder
/// the second, so the wiring can be driven directly rather than by hand.
///
/// <para>These pin the things a constructor quietly does — the what's-new modal
/// deciding whether to appear, the version stamp being written, navigation and
/// zen mode agreeing with each other — since every one of those has to be right
/// before the window is ever shown.</para></summary>
[Collection(HeadlessCollection.Name)]
public class MainWindowViewModelTests : IDisposable
{
    /// <summary>State in memory, location on disk: the view model derives the
    /// review log and media directories from Location, so it has to be real.</summary>
    private sealed class TempStorage : IStorageService, IDisposable
    {
        private readonly string _dir =
            Path.Combine(Path.GetTempPath(), "tomoshibi-tests", Guid.NewGuid().ToString("N"));

        public AppState State { get; }
        public int Saves { get; private set; }

        public TempStorage(AppState? state = null)
        {
            State = state ?? new AppState();
            Directory.CreateDirectory(_dir);
        }

        public string Location => Path.Combine(_dir, "tomoshibi.json");
        public AppState Load() => State;
        public void Save(AppState state) => Saves++;

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
        }
    }

    private readonly List<TempStorage> _made = new();

    private MainWindowViewModel Shell(AppState? state = null)
    {
        var storage = new TempStorage(state);
        _made.Add(storage);
        return new MainWindowViewModel(storage);
    }

    public void Dispose()
    {
        foreach (var s in _made) s.Dispose();
    }

    // ---- what the constructor decides ----

    [Fact]
    public void A_fresh_install_is_not_told_whats_new() => Headless.Run(() =>
    {
        // Empty LastSeenVersion means nobody has run this before. There's no
        // "since last time" to describe, so the modal would be showing release
        // notes for a version they've never not had.
        var vm = Shell(new AppState { LastSeenVersion = string.Empty });

        Assert.False(vm.IsWhatsNewOpen);
    });

    [Fact]
    public void An_updated_install_is() => Headless.Run(() =>
    {
        var vm = Shell(new AppState { LastSeenVersion = "0.0.1" });

        Assert.True(vm.IsWhatsNewOpen);
    });

    [Fact]
    public void Relaunching_the_same_build_says_nothing_the_second_time() => Headless.Run(() =>
    {
        var vm = Shell(new AppState { LastSeenVersion = ReleaseNotes.Version });

        Assert.False(vm.IsWhatsNewOpen);
    });

    [Fact]
    public void The_running_version_is_stamped_so_the_modal_only_lands_once() => Headless.Run(() =>
    {
        var state = new AppState { LastSeenVersion = "0.0.1" };

        var vm = Shell(state);

        Assert.True(vm.IsWhatsNewOpen);
        Assert.Equal(ReleaseNotes.Version, state.LastSeenVersion);
    });

    [Fact]
    public void The_greeting_follows_the_saved_preference() => Headless.Run(() =>
    {
        Assert.True(Shell(new AppState { ShowWelcome = true }).IsWelcomeOpen);
        Assert.False(Shell(new AppState { ShowWelcome = false }).IsWelcomeOpen);
    });

    [Fact]
    public void Every_destination_is_wired_up() => Headless.Run(() =>
    {
        // A null here is a page that opens to nothing at runtime.
        var vm = Shell();

        Assert.NotNull(vm.Dashboard);
        Assert.NotNull(vm.Today);
        Assert.NotNull(vm.Timetable);
        Assert.NotNull(vm.Todo);
        Assert.NotNull(vm.Subjects);
        Assert.NotNull(vm.Stats);
        Assert.NotNull(vm.Review);
        Assert.NotNull(vm.Shop);
        Assert.NotNull(vm.SettingsPage);
        Assert.NotNull(vm.Music);
        Assert.NotNull(vm.Wallet);
        Assert.NotNull(vm.CommandPalette);
    });

    // ---- navigation ----

    [Fact]
    public void The_saved_destination_is_where_it_opens() => Headless.Run(() =>
    {
        var vm = Shell(new AppState { ActiveDestination = Destination.Review });

        Assert.Equal(Destination.Review, vm.ActiveDestination);
        Assert.True(vm.IsReviewActive);
        Assert.False(vm.IsDashboardActive);
    });

    [Fact]
    public void Only_one_page_is_active_at_a_time() => Headless.Run(() =>
    {
        var vm = Shell();

        vm.NavigateByIndex(3);   // timetable, third in nav order

        Assert.Equal(Destination.Timetable, vm.ActiveDestination);
        Assert.True(vm.IsTimetableActive);
        Assert.False(vm.IsTodayActive);
        Assert.False(vm.IsDashboardActive);
    });

    [Fact]
    public void An_index_off_the_end_of_the_nav_is_ignored() => Headless.Run(() =>
    {
        var vm = Shell();
        var before = vm.ActiveDestination;

        vm.NavigateByIndex(0);
        vm.NavigateByIndex(99);
        vm.NavigateByIndex(-1);

        Assert.Equal(before, vm.ActiveDestination);
    });

    [Fact]
    public void Zen_mode_takes_the_navigation_away() => Headless.Run(() =>
    {
        // Zen is the whole point: nothing but the timer. A chord that moved
        // the page out from under it would be a way to get stuck.
        var vm = Shell();
        vm.ToggleZenCommand.Execute(null);
        var whereWeWere = vm.ActiveDestination;

        vm.NavigateByIndex(5);

        Assert.True(vm.IsZenMode);
        Assert.Equal(whereWeWere, vm.ActiveDestination);
    });

    [Fact]
    public void Leaving_zen_gives_it_back() => Headless.Run(() =>
    {
        var vm = Shell();
        vm.ToggleZenCommand.Execute(null);

        vm.ExitZenCommand.Execute(null);
        vm.NavigateByIndex(5);

        Assert.False(vm.IsZenMode);
        Assert.Equal(Destination.Subjects, vm.ActiveDestination);
    });

    // ---- modals ----

    [Fact]
    public void A_modal_stands_the_global_shortcuts_down() => Headless.Run(() =>
    {
        // Space toggles the timer from the Today page. While a dialog is up it
        // has to stop, or typing in the dialog drives the timer.
        var vm = Shell(new AppState { ShowWelcome = false });
        Assert.False(vm.AnyModalOpen);

        vm.OpenTourCommand.Execute(null);

        Assert.True(vm.AnyModalOpen);
    });

    [Fact]
    public void The_tour_closes_on_its_last_page_rather_than_running_off_the_end() => Headless.Run(() =>
    {
        var vm = Shell();
        vm.OpenTourCommand.Execute(null);

        for (var i = 0; i < 3; i++)
            vm.NextTourPageCommand.Execute(null);
        Assert.True(vm.IsTourLastPage);

        vm.NextTourPageCommand.Execute(null);

        Assert.False(vm.IsTourOpen);
    });

    [Fact]
    public void The_tour_takes_the_stage_from_the_greeting() => Headless.Run(() =>
    {
        var vm = Shell(new AppState { ShowWelcome = true });
        Assert.True(vm.IsWelcomeOpen);

        vm.OpenTourCommand.Execute(null);

        Assert.True(vm.IsTourOpen);
        Assert.False(vm.IsWelcomeOpen);
    });
}
