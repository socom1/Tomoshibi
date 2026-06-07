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

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonStorageService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tomoshibi");

        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "tomoshibi.json");
    }

    public AppState Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppState();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppState>(json, Options) ?? new AppState();
        }
        catch
        {
            // A corrupt or unreadable file shouldn't crash launch; start fresh.
            return new AppState();
        }
    }

    public void Save(AppState state)
    {
        var json = JsonSerializer.Serialize(state, Options);
        File.WriteAllText(_filePath, json);
    }
}
