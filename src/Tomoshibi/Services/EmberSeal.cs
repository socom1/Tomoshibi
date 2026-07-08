using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// A tamper seal over the ember economy. tomoshibi.json is the user's file
/// and stays hand-editable — but embers and the themes they buy are earned,
/// so the wallet carries an HMAC that's stamped on every save and checked on
/// load: a balance that doesn't match its seal is emptied rather than
/// believed. Honest about its strength: the key ships in a public binary, so
/// this is a speed bump for the casual "embers": 999999 edit, not real
/// cryptographic protection — anyone who reads this source can re-seal.
/// That's the right trade for a single-player economy.
/// </summary>
public static class EmberSeal
{
    /// <summary>The release the seal shipped in. Files last saved by an older
    /// build are trusted once and stamped, so no one's wallet resets on
    /// upgrade — see <see cref="StateMigrations"/>.</summary>
    public static readonly Version IntroducedIn = new(2, 0, 0);

    private static readonly byte[] Key = Encoding.UTF8.GetBytes("tomoshibi-ember-seal-v1-灯火");

    /// <summary>The free themes every install owns — kept in step with
    /// <see cref="StateMigrations"/>' theme rules.</summary>
    private static readonly string[] FreeThemes = { "dark", "light" };

    public static string Compute(AppState state)
    {
        var themes = string.Join(",", state.OwnedThemeIds.OrderBy(t => t, StringComparer.Ordinal));
        var payload = $"{state.Embers}|{themes}|{state.ActiveThemeId}";
        return Convert.ToHexString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>Stamp the current wallet — called on every save, so the seal
    /// always matches the last state the app itself wrote.</summary>
    public static void Apply(AppState state) => state.EmberSeal = Compute(state);

    public static bool Verify(AppState state) =>
        !string.IsNullOrEmpty(state.EmberSeal) &&
        string.Equals(state.EmberSeal, Compute(state), StringComparison.OrdinalIgnoreCase);

    /// <summary>A broken seal empties the wallet: balance to zero, bought
    /// themes gone. The free themes stay (they're free) and a free active
    /// theme is kept; a paid one falls back to the default.</summary>
    public static void Reset(AppState state)
    {
        state.Embers = 0;
        state.OwnedThemeIds.Clear();
        state.OwnedThemeIds.AddRange(FreeThemes);

        if (!FreeThemes.Contains(state.ActiveThemeId))
            state.ActiveThemeId = "dark";

        Apply(state);
    }
}
