using System;
using System.IO;
using System.Text.Json;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Persists <see cref="AppState"/> to a single JSON file under the OS
/// application-data folder (so it survives reinstalls and stays out of the repo).
/// </summary>
public class JsonStorageService : IStorageService
{
    private readonly string _filePath;
    private readonly string _tmpPath;
    private readonly string _bakPath;

    public string Location => _filePath;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Stores under the OS application-data folder — the real app path.</summary>
    public JsonStorageService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tomoshibi"))
    {
    }

    /// <summary>Stores under an explicit directory. Lets tests point the service
    /// at a throwaway folder instead of the user's real app-data.</summary>
    public JsonStorageService(string directory)
    {
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "tomoshibi.json");
        _tmpPath = _filePath + ".tmp";
        _bakPath = _filePath + ".bak";
    }

    public AppState Load()
    {
        // Main file first; if it's missing or corrupt (e.g. a write was cut
        // short by a crash), fall back to the backup of the last good save
        // before giving up and starting fresh.
        return TryLoad(_filePath) ?? TryLoad(_bakPath) ?? new AppState();
    }

    private static AppState? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppState>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public void Save(AppState state)
    {
        var json = JsonSerializer.Serialize(state, Options);

        // Never write over the live file in place: serialise to a temp file,
        // then swap it in atomically, rotating the previous good state to
        // .bak in the same call. A crash at any point leaves either the old
        // file or the backup intact instead of a truncated half-write.
        File.WriteAllText(_tmpPath, json);

        if (File.Exists(_filePath))
            File.Replace(_tmpPath, _filePath, _bakPath);
        else
            File.Move(_tmpPath, _filePath);
    }
}
