using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The .apkg importer against a hand-built schema-11 collection:
/// note types, media rewriting, scheduling approximation, and the newest-format
/// rejection.</summary>
public class ApkgImporterTests : IDisposable
{
    private readonly string _dir;

    public ApkgImporterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tomoshibi-apkg-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private MediaStore NewMedia() => new(Path.Combine(_dir, "app"));

    [Fact]
    public void Imports_decks_notes_types_and_media()
    {
        var apkg = BuildApkg();
        var result = ApkgImporter.Import(apkg, NewMedia(), keepSchedule: true);

        Assert.True(result.Ok);
        Assert.Equal(4, result.Notes);

        // Two decks: Default + the flattened subdeck.
        Assert.Contains(result.Decks, d => d.Name == "Default");
        Assert.Contains(result.Decks, d => d.Name == "Spanish / Verbs");

        var allNotes = result.Decks.SelectMany(d => d.Notes).ToList();

        // Cloze note → Cloze type, card ord mapped from Anki's 0-based to our 1-based.
        var cloze = allNotes.Single(n => n.Type == NoteType.Cloze);
        Assert.Equal(1, cloze.Cards.Single().Ord);

        // Reversed model (2 templates) → two cards.
        var reversed = allNotes.Single(n => n.Type == NoteType.BasicReversed);
        Assert.Equal(2, reversed.Cards.Count);

        Assert.Equal(2, result.Media);
    }

    [Fact]
    public void Review_cards_keep_an_approximate_schedule()
    {
        var apkg = BuildApkg();
        var result = ApkgImporter.Import(apkg, NewMedia(), keepSchedule: true);

        var basic = result.Decks.SelectMany(d => d.Notes)
            .First(n => n.Type == NoteType.Basic && n.Fields[0] == "Front1");
        var card = basic.Cards.Single();

        Assert.Equal(CardState.Review, card.State);
        Assert.Equal(12, card.Stability);          // interval → stability
        Assert.Equal(5.0, card.Difficulty, 3);     // factor 2500 (ease 2.5) → mid
    }

    [Fact]
    public void Media_references_are_rewritten_to_stored_tokens()
    {
        var apkg = BuildApkg();
        var result = ApkgImporter.Import(apkg, NewMedia(), keepSchedule: true);

        var mediaNote = result.Decks.SelectMany(d => d.Notes)
            .First(n => n.Fields.Any(f => f.Contains("[img:")));

        Assert.Contains("[img:", mediaNote.Fields[0]);
        Assert.DoesNotContain("[img:pic.jpg]", mediaNote.Fields[0]); // rewritten to a token
    }

    [Fact]
    public void Suspended_cards_import_suspended()
    {
        var apkg = BuildApkg();
        var result = ApkgImporter.Import(apkg, NewMedia(), keepSchedule: true);

        var suspended = result.Decks.SelectMany(d => d.Notes).SelectMany(n => n.Cards)
            .Where(c => c.Suspended).ToList();
        Assert.Single(suspended);
    }

    [Fact]
    public void Import_all_as_new_ignores_scheduling()
    {
        var apkg = BuildApkg();
        var result = ApkgImporter.Import(apkg, NewMedia(), keepSchedule: false);

        Assert.All(result.Decks.SelectMany(d => d.Notes).SelectMany(n => n.Cards),
            c => Assert.Equal(CardState.New, c.State));
    }

    [Fact]
    public void The_newest_format_is_rejected_with_guidance()
    {
        var apkg = Path.Combine(_dir, "new.apkg");
        using (var zs = File.Create(apkg))
        using (var zip = new ZipArchive(zs, ZipArchiveMode.Create))
            AddText(zip, "collection.anki21b", "zstd-bytes");

        var result = ApkgImporter.Import(apkg, NewMedia(), keepSchedule: true);

        Assert.False(result.Ok);
        Assert.Contains("older Anki versions", result.Message);
    }

    // ---- fixture ----

    private string BuildApkg()
    {
        var dbPath = Path.Combine(_dir, "collection.anki2");

        const string models =
            "{\"1\":{\"name\":\"Basic\",\"type\":0,\"tmpls\":[{}]}," +
            "\"2\":{\"name\":\"Cloze\",\"type\":1,\"tmpls\":[{}]}," +
            "\"3\":{\"name\":\"Reversed\",\"type\":0,\"tmpls\":[{},{}]}}";
        const string decks = "{\"1\":{\"name\":\"Default\"},\"55\":{\"name\":\"Spanish::Verbs\"}}";
        const long crt = 1600000000;

        using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
        {
            conn.Open();
            Exec(conn, "CREATE TABLE col (models TEXT, decks TEXT, crt INTEGER)");
            Exec(conn, "CREATE TABLE notes (id INTEGER, mid INTEGER, flds TEXT, tags TEXT)");
            Exec(conn, "CREATE TABLE cards (id INTEGER, nid INTEGER, did INTEGER, ord INTEGER, " +
                       "type INTEGER, queue INTEGER, due INTEGER, ivl INTEGER, factor INTEGER, reps INTEGER)");

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO col VALUES ($m,$d,$c)";
                cmd.Parameters.AddWithValue("$m", models);
                cmd.Parameters.AddWithValue("$d", decks);
                cmd.Parameters.AddWithValue("$c", crt);
                cmd.ExecuteNonQuery();
            }

            const string us = ""; // Anki field separator
            InsertNote(conn, 1, 1, "Front1" + us + "Back1", " geo ");
            InsertNote(conn, 2, 2, "{{c1::hidden}} sentence" + us + "extra", "");
            InsertNote(conn, 3, 3, "hola" + us + "hello", "");
            InsertNote(conn, 4, 1, "<img src=\"pic.jpg\"> look" + us + "[sound:s.mp3]", "");

            InsertCard(conn, 10, 1, 1, 0, type: 2, queue: 0, due: 100, ivl: 12, factor: 2500, reps: 3);
            InsertCard(conn, 20, 2, 55, 0, type: 0, queue: 0, due: 1, ivl: 0, factor: 0, reps: 0);
            InsertCard(conn, 30, 3, 1, 0, type: 0, queue: 0, due: 2, ivl: 0, factor: 0, reps: 0);
            InsertCard(conn, 31, 3, 1, 1, type: 0, queue: 0, due: 3, ivl: 0, factor: 0, reps: 0);
            InsertCard(conn, 40, 4, 1, 0, type: 0, queue: -1, due: 4, ivl: 0, factor: 0, reps: 0);
        }
        SqliteConnection.ClearAllPools();

        var apkg = Path.Combine(_dir, "deck.apkg");
        using (var zs = File.Create(apkg))
        using (var zip = new ZipArchive(zs, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(dbPath, "collection.anki2");
            AddText(zip, "media", "{\"0\":\"pic.jpg\",\"1\":\"s.mp3\"}");
            AddText(zip, "0", "fake-image-bytes");
            AddText(zip, "1", "fake-sound-bytes");
        }
        return apkg;
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void InsertNote(SqliteConnection conn, long id, long mid, string flds, string tags)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO notes VALUES ($id,$mid,$flds,$tags)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$mid", mid);
        cmd.Parameters.AddWithValue("$flds", flds);
        cmd.Parameters.AddWithValue("$tags", tags);
        cmd.ExecuteNonQuery();
    }

    private static void InsertCard(SqliteConnection conn, long id, long nid, long did, int ord,
        int type, int queue, int due, int ivl, int factor, int reps)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO cards VALUES ($id,$nid,$did,$ord,$type,$queue,$due,$ivl,$factor,$reps)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$nid", nid);
        cmd.Parameters.AddWithValue("$did", did);
        cmd.Parameters.AddWithValue("$ord", ord);
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$queue", queue);
        cmd.Parameters.AddWithValue("$due", due);
        cmd.Parameters.AddWithValue("$ivl", ivl);
        cmd.Parameters.AddWithValue("$factor", factor);
        cmd.Parameters.AddWithValue("$reps", reps);
        cmd.ExecuteNonQuery();
    }

    private static void AddText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var s = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        s.Write(bytes, 0, bytes.Length);
    }
}
