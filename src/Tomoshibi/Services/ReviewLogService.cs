using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// JSONL review log at <c>reviewlog.jsonl</c> in the app-data folder — one
/// compact JSON object per line. Appending is O(1) (no full rewrite), which is
/// why the log lives here rather than inside the debounced state file that a
/// review session would otherwise thrash. A crash mid-append can leave a
/// truncated final line; loading simply skips any line that won't parse.
/// </summary>
public class ReviewLogService : IReviewLogService
{
    private readonly string _path;
    private List<ReviewLogEntry>? _cache;

    private static readonly JsonSerializerOptions Options = new()
    {
        // Entry properties already carry explicit short [JsonPropertyName]s.
        WriteIndented = false
    };

    /// <summary>Real app path: same folder as tomoshibi.json.</summary>
    public ReviewLogService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tomoshibi"))
    {
    }

    /// <summary>Explicit directory — lets tests point at a throwaway folder.</summary>
    public ReviewLogService(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "reviewlog.jsonl");
    }

    public string Location => _path;

    public void Append(ReviewLogEntry entry)
    {
        EnsureLoaded();
        var line = JsonSerializer.Serialize(entry, Options);
        File.AppendAllText(_path, line + "\n");
        _cache!.Add(entry);
    }

    public IReadOnlyList<ReviewLogEntry> All()
    {
        EnsureLoaded();
        return _cache!;
    }

    public int CountToday(Guid deckId, CardState stateBefore, DateOnly today)
    {
        EnsureLoaded();
        var n = 0;
        foreach (var e in _cache!)
            if (e.DeckId == deckId && e.StateBefore == stateBefore
                && DateOnly.FromDateTime(e.Timestamp) == today)
                n++;
        return n;
    }

    private void EnsureLoaded()
    {
        if (_cache is not null) return;
        _cache = new List<ReviewLogEntry>();

        if (!File.Exists(_path)) return;

        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<ReviewLogEntry>(line, Options);
                if (entry is not null) _cache.Add(entry);
            }
            catch (JsonException)
            {
                // Truncated / corrupt line (e.g. a crash mid-append) — skip it.
            }
        }
    }
}
