using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>The outcome of an .apkg import.</summary>
public sealed class ApkgResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<Deck> Decks { get; } = new();
    public int Notes { get; set; }
    public int Cards { get; set; }
    public int Media { get; set; }
    public int Skipped { get; set; }
}

/// <summary>
/// Imports a legacy Anki <c>.apkg</c> (schema 11: <c>collection.anki2</c> /
/// <c>.anki21</c>) into Tomoshibi decks — notes, media, tags and an approximate
/// FSRS schedule. The newest <c>collection.anki21b</c> format is detected and
/// rejected with guidance rather than parsed. Defensive throughout: a note that
/// can't be read is skipped and counted, never fatal.
/// </summary>
public static class ApkgImporter
{
    private readonly record struct ModelInfo(int Type, int TemplateCount);
    private readonly record struct AnkiCard(int Ord, long Did, int Type, int Queue, long Due, int Ivl, int Factor, int Reps);

    public static ApkgResult Import(string apkgPath, MediaStore media, bool keepSchedule)
    {
        string? tempDb = null;
        try
        {
            using var zip = ZipFile.OpenRead(apkgPath);

            var dbEntry = zip.GetEntry("collection.anki21") ?? zip.GetEntry("collection.anki2");
            if (dbEntry is null)
            {
                return zip.GetEntry("collection.anki21b") is not null
                    ? Fail("This deck uses Anki's newest format. In Anki, re-export it with " +
                           "“Support older Anki versions (slower/larger files)” ticked, then import again.")
                    : Fail("No Anki collection was found inside this file.");
            }

            var mediaMap = ImportMedia(zip, media);

            tempDb = Path.Combine(Path.GetTempPath(), "tomoshibi-apkg-" + Guid.NewGuid().ToString("N") + ".db");
            dbEntry.ExtractToFile(tempDb, overwrite: true);

            var result = new ApkgResult { Ok = true, Message = "imported" };

            using (var conn = new SqliteConnection($"Data Source={tempDb};Mode=ReadOnly"))
            {
                conn.Open();
                var (models, deckNames, crt) = ReadCol(conn);
                var cardsByNote = ReadCards(conn);
                var decksByName = new Dictionary<string, Deck>();

                Deck DeckFor(long did)
                {
                    var name = (deckNames.TryGetValue(did, out var n) ? n : "Imported").Replace("::", " / ");
                    if (!decksByName.TryGetValue(name, out var deck))
                    {
                        deck = new Deck { Name = name };
                        decksByName[name] = deck;
                        result.Decks.Add(deck);
                    }
                    return deck;
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT id, mid, flds, tags FROM notes";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    try
                    {
                        var nid = reader.GetInt64(0);
                        var mid = reader.GetInt64(1);
                        if (!models.TryGetValue(mid, out var model)) { result.Skipped++; continue; }

                        var fields = reader.GetString(2).Split('\x1f');
                        var tags = reader.GetString(3);

                        var note = BuildNote(model, fields, tags, mediaMap);
                        CardGenerator.Sync(note);

                        cardsByNote.TryGetValue(nid, out var ankiCards);
                        if (ankiCards is not null)
                            foreach (var ac in ankiCards)
                                ApplySchedule(note, ac, crt, keepSchedule);

                        var did = ankiCards is { Count: > 0 } ? ankiCards[0].Did : 1;
                        DeckFor(did).Notes.Add(note);
                        result.Notes++;
                        result.Cards += note.Cards.Count;
                    }
                    catch
                    {
                        result.Skipped++;
                    }
                }

                result.Media = mediaMap.Count;
            }

            return result;
        }
        catch (Exception ex)
        {
            return Fail("Couldn't read this file: " + ex.Message);
        }
        finally
        {
            if (tempDb is not null)
                try { File.Delete(tempDb); } catch { /* temp file */ }
        }
    }

    private static ApkgResult Fail(string message) => new() { Ok = false, Message = message };

    // ---- notes → our model ----

    private static Note BuildNote(ModelInfo model, string[] fields, string tags, IReadOnlyDictionary<string, string> mediaMap)
    {
        var type = model.Type == 1 ? NoteType.Cloze
                 : model.TemplateCount >= 2 ? NoteType.BasicReversed
                 : NoteType.Basic;

        string Field(int i) => Rewrite(AnkiHtmlConverter.Convert(i < fields.Length ? fields[i] : string.Empty), mediaMap);

        var note = new Note { Type = type };
        note.Fields.Add(Field(0));
        note.Fields.Add(Field(1));

        foreach (var tag in tags.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (!note.Tags.Contains(tag))
                note.Tags.Add(tag);

        return note;
    }

    private static string Rewrite(string text, IReadOnlyDictionary<string, string> mediaMap)
    {
        foreach (var (name, token) in mediaMap)
            if (text.Contains(name, StringComparison.Ordinal))
                text = text.Replace($"[img:{name}]", $"[img:{token}]")
                           .Replace($"[sound:{name}]", $"[sound:{token}]");
        return text;
    }

    private static void ApplySchedule(Note note, AnkiCard ac, long crt, bool keepSchedule)
    {
        var ourOrd = note.Type == NoteType.Cloze ? ac.Ord + 1 : ac.Ord;
        var card = note.Cards.FirstOrDefault(c => c.Ord == ourOrd);
        if (card is null) return;

        if (ac.Queue == -1)
            card.Suspended = true;

        if (keepSchedule && ac.Type == 2 && ac.Ivl > 0)
        {
            card.State = CardState.Review;
            card.Stability = Math.Max(1, ac.Ivl);
            card.Difficulty = Math.Clamp(5 + (2.5 - ac.Factor / 1000.0) * 4.5, 1, 10);
            card.Reps = ac.Reps;

            var crtDate = DateTimeOffset.FromUnixTimeSeconds(crt).LocalDateTime.Date;
            var due = crtDate.AddDays(Math.Min(ac.Due, 3650));
            card.Due = due < DateTime.Now ? DateTime.Now : due;
            card.LastReviewed = card.Due.AddDays(-ac.Ivl);
        }
    }

    // ---- SQLite reads ----

    private static (Dictionary<long, ModelInfo> Models, Dictionary<long, string> Decks, long Crt) ReadCol(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT models, decks, crt FROM col LIMIT 1";
        using var r = cmd.ExecuteReader();
        r.Read();

        var models = new Dictionary<long, ModelInfo>();
        using (var doc = JsonDocument.Parse(r.GetString(0)))
            foreach (var m in doc.RootElement.EnumerateObject())
            {
                if (!long.TryParse(m.Name, out var mid)) continue;
                var type = m.Value.TryGetProperty("type", out var t) ? t.GetInt32() : 0;
                var tmpls = m.Value.TryGetProperty("tmpls", out var tm) ? tm.GetArrayLength() : 1;
                models[mid] = new ModelInfo(type, tmpls);
            }

        var decks = new Dictionary<long, string>();
        using (var doc = JsonDocument.Parse(r.GetString(1)))
            foreach (var d in doc.RootElement.EnumerateObject())
            {
                if (!long.TryParse(d.Name, out var did)) continue;
                decks[did] = d.Value.TryGetProperty("name", out var nm) ? nm.GetString() ?? "Imported" : "Imported";
            }

        var crt = r.GetInt64(2);
        return (models, decks, crt);
    }

    private static Dictionary<long, List<AnkiCard>> ReadCards(SqliteConnection conn)
    {
        var map = new Dictionary<long, List<AnkiCard>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT nid, did, ord, type, queue, due, ivl, factor, reps FROM cards";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var nid = r.GetInt64(0);
            var card = new AnkiCard(
                Ord: r.GetInt32(2), Did: r.GetInt64(1), Type: r.GetInt32(3), Queue: r.GetInt32(4),
                Due: r.GetInt64(5), Ivl: r.GetInt32(6), Factor: r.GetInt32(7), Reps: r.GetInt32(8));
            if (!map.TryGetValue(nid, out var list))
                map[nid] = list = new List<AnkiCard>();
            list.Add(card);
        }
        return map;
    }

    private static Dictionary<string, string> ImportMedia(ZipArchive zip, MediaStore media)
    {
        var map = new Dictionary<string, string>();
        var mediaEntry = zip.GetEntry("media");
        if (mediaEntry is null) return map;

        try
        {
            using var stream = mediaEntry.Open();
            using var doc = JsonDocument.Parse(stream);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var filename = prop.Value.GetString();
                var fileEntry = zip.GetEntry(prop.Name);
                if (filename is null || fileEntry is null) continue;

                using var fs = fileEntry.Open();
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                map[filename] = media.Import(ms.ToArray(), filename);
            }
        }
        catch { /* malformed media map — skip, cards still import with broken refs */ }

        return map;
    }
}
