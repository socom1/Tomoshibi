using System;
using System.IO;
using System.Text.Json;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The wallet seal has two jobs that pull against each other: never
/// empty an honest wallet (fresh installs, upgrades from pre-seal builds,
/// ordinary saves) and always empty an edited one (a changed balance, a
/// granted theme, a deleted seal on a sealed-era file).</summary>
public class EmberSealTests
{
    /// <summary>A wallet as a sealed-era build would have written it.</summary>
    private static AppState SealedState(int embers, params string[] paidThemes)
    {
        var state = new AppState { Embers = embers, LastSeenVersion = "2.0.0" };
        StateMigrations.Apply(state); // owns the free themes, stamps a seal
        foreach (var theme in paidThemes)
            state.OwnedThemeIds.Add(theme);
        state.Embers = embers;
        EmberSeal.Apply(state);
        return state;
    }

    [Fact]
    public void A_fresh_install_keeps_its_empty_wallet_across_launches()
    {
        var state = new AppState();
        StateMigrations.Apply(state);

        Assert.Equal(0, state.Embers);
        Assert.Contains("dark", state.OwnedThemeIds);

        // Next launch: the save stamps, the reload verifies — nothing lost.
        EmberSeal.Apply(state);
        StateMigrations.Apply(state);
        Assert.Equal(0, state.Embers);
    }

    [Fact]
    public void A_pre_seal_file_is_trusted_once_and_survives_the_next_launch()
    {
        var state = new AppState { Embers = 540, LastSeenVersion = "1.9.0" };
        state.OwnedThemeIds.Add("matcha");

        StateMigrations.Apply(state);

        Assert.Equal(540, state.Embers);
        Assert.Contains("matcha", state.OwnedThemeIds);

        // The save that follows every launch stamps the migrated state; a
        // second load must then pass verification untouched.
        EmberSeal.Apply(state);
        state.LastSeenVersion = "2.0.0";
        StateMigrations.Apply(state);

        Assert.Equal(540, state.Embers);
        Assert.Contains("matcha", state.OwnedThemeIds);
    }

    [Fact]
    public void An_edited_balance_resets_the_wallet()
    {
        var state = SealedState(120, "matcha");
        state.Embers = 999_999; // the classic hand edit

        StateMigrations.Apply(state);

        Assert.Equal(0, state.Embers);
        Assert.DoesNotContain("matcha", state.OwnedThemeIds);
        Assert.True(EmberSeal.Verify(state));
    }

    [Fact]
    public void A_granted_theme_resets_the_wallet_too()
    {
        var state = SealedState(120);
        state.OwnedThemeIds.Add("sakura");
        state.ActiveThemeId = "sakura";

        StateMigrations.Apply(state);

        Assert.Equal(0, state.Embers);
        Assert.DoesNotContain("sakura", state.OwnedThemeIds);
        Assert.Equal("dark", state.ActiveThemeId); // paid active falls back
    }

    [Fact]
    public void A_deleted_seal_on_a_sealed_era_file_resets()
    {
        var state = SealedState(300);
        state.EmberSeal = string.Empty;

        StateMigrations.Apply(state);

        Assert.Equal(0, state.Embers);
    }

    [Fact]
    public void The_free_themes_survive_a_reset()
    {
        var state = SealedState(50, "matcha");
        state.ActiveThemeId = "light"; // free choice, kept through the reset
        EmberSeal.Apply(state);
        state.Embers = 5000;

        StateMigrations.Apply(state);

        Assert.Contains("dark", state.OwnedThemeIds);
        Assert.Contains("light", state.OwnedThemeIds);
        Assert.Equal("light", state.ActiveThemeId);
    }

    [Fact]
    public void An_untouched_wallet_migrates_unchanged()
    {
        var state = SealedState(875, "matcha", "sunset");

        StateMigrations.Apply(state);

        Assert.Equal(875, state.Embers);
        Assert.Contains("matcha", state.OwnedThemeIds);
        Assert.Contains("sunset", state.OwnedThemeIds);
    }

    [Fact]
    public void Every_save_stamps_a_seal_that_loads_clean()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tomoshibi-seal-{Guid.NewGuid():N}");
        try
        {
            var storage = new JsonStorageService(dir);
            var state = new AppState { Embers = 240, LastSeenVersion = "2.0.0" };
            state.OwnedThemeIds.AddRange(new[] { "dark", "light", "matcha" });

            storage.Save(state);
            var loaded = storage.Load();
            StateMigrations.Apply(loaded);

            Assert.Equal(240, loaded.Embers);
            Assert.Contains("matcha", loaded.OwnedThemeIds);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void A_tampered_backup_is_neutralised_on_restore()
    {
        var honest = SealedState(150, "matcha");
        var json = JsonSerializer.Serialize(honest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var tampered = json.Replace("\"embers\": 150", "\"embers\": 888888")
                           .Replace("\"embers\":150", "\"embers\":888888");

        var restored = BackupRestore.Parse(tampered);

        Assert.NotNull(restored);
        Assert.Equal(0, restored.Embers);
    }
}
