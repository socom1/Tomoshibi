using System;
using System.IO;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The storage layer is the one place a crash can lose everything, so
/// its save-to-temp-then-swap and .bak fallback are worth proving: a corrupt
/// main file must recover the last good save rather than start blank.</summary>
public class JsonStorageServiceTests : IDisposable
{
    private readonly string _dir;

    public JsonStorageServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tomoshibi-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_returns_defaults_when_nothing_has_been_saved()
    {
        var store = new JsonStorageService(_dir);

        var state = store.Load();

        Assert.NotNull(state);
        Assert.Equal(string.Empty, state.DailyIntention);
    }

    [Fact]
    public void Save_then_load_round_trips_state()
    {
        var store = new JsonStorageService(_dir);
        store.Save(new() { DailyIntention = "ship the tests", Embers = 42 });

        var loaded = new JsonStorageService(_dir).Load();

        Assert.Equal("ship the tests", loaded.DailyIntention);
        Assert.Equal(42, loaded.Embers);
    }

    [Fact]
    public void A_corrupt_main_file_recovers_the_previous_good_save_from_backup()
    {
        var store = new JsonStorageService(_dir);
        store.Save(new() { DailyIntention = "first" });   // becomes the .bak on the next save
        store.Save(new() { DailyIntention = "second" });  // current good file

        // Simulate a half-written / corrupted main file.
        File.WriteAllText(Path.Combine(_dir, "tomoshibi.json"), "{ this is not valid json");

        var recovered = store.Load();

        Assert.Equal("first", recovered.DailyIntention);
    }
}
