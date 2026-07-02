using System.Text.Json;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Reads a backup file back into a usable <see cref="AppState"/> — the other
/// half of the settings-page backup button. The JSON shape matches what the
/// storage service and the backup export both write (camelCase); anything
/// that doesn't parse comes back null rather than half-restored. Migrations
/// run on the way in, so a backup taken by an older build lands in today's
/// shape, exactly as if it had been loaded from disk.
/// </summary>
public static class BackupRestore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppState? Parse(string json)
    {
        AppState? state;
        try
        {
            state = JsonSerializer.Deserialize<AppState>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (state is null)
            return null;

        StateMigrations.Apply(state);
        return state;
    }
}
