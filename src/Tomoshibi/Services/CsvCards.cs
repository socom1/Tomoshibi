using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Bulk import/export of notes as CSV or TSV. Export writes
/// <c>notetype,field1,field2,tags</c>. Import is forgiving: it auto-detects the
/// delimiter, skips a header row if present, accepts either our own shape or a
/// plain <c>front,back[,tags]</c> export from elsewhere, and infers a Cloze
/// note when the first field carries <c>{{c…}}</c>. Hand-rolled to avoid a CSV
/// dependency, but handles quoting/escaping and embedded newlines.
/// </summary>
public static class CsvCards
{
    public static string Export(IEnumerable<Note> notes)
    {
        var sb = new StringBuilder();
        sb.Append("notetype,field1,field2,tags\n");
        foreach (var note in notes)
        {
            var f0 = note.Fields.Count > 0 ? note.Fields[0] : string.Empty;
            var f1 = note.Fields.Count > 1 ? note.Fields[1] : string.Empty;
            sb.Append(string.Join(",",
                Escape(TypeName(note.Type)), Escape(f0), Escape(f1),
                Escape(string.Join(" ", note.Tags))));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public static IReadOnlyList<Note> Import(string? text)
    {
        var content = text ?? string.Empty;
        var delimiter = DetectDelimiter(content);
        var rows = ParseRows(content, delimiter);
        var notes = new List<Note>();
        var first = true;

        foreach (var row in rows)
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            if (first)
            {
                first = false;
                if (LooksLikeHeader(row[0])) continue;
            }

            notes.Add(RowToNote(row));
        }

        return notes;
    }

    private static Note RowToNote(IReadOnlyList<string> row)
    {
        string type, f0, f1, tags;

        if (row.Count > 0 && TryType(row[0], out var parsed))
        {
            type = parsed;
            f0 = At(row, 1);
            f1 = At(row, 2);
            tags = At(row, 3);
        }
        else
        {
            f0 = At(row, 0);
            f1 = At(row, 1);
            tags = At(row, 2);
            type = ClozeParser.HasCloze(f0) ? "cloze" : "basic";
        }

        var note = new Note
        {
            Type = ParseType(type),
            Fields = { f0, f1 },
            Tags = tags.Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries).ToList()
        };
        CardGenerator.Sync(note);
        return note;
    }

    // ---- delimiter + header heuristics ----

    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;
        return firstLine.Count(c => c == '\t') > firstLine.Count(c => c == ',') ? '\t' : ',';
    }

    private static bool LooksLikeHeader(string firstCell)
        => firstCell.Trim().ToLowerInvariant() is "notetype" or "type" or "front" or "text" or "field1";

    private static bool TryType(string cell, out string type)
    {
        type = cell.Trim().ToLowerInvariant();
        return type is "basic" or "reversed" or "cloze" or "occlusion";
    }

    private static NoteType ParseType(string type) => type switch
    {
        "reversed" => NoteType.BasicReversed,
        "cloze" => NoteType.Cloze,
        "occlusion" => NoteType.ImageOcclusion,
        _ => NoteType.Basic
    };

    private static string TypeName(NoteType type) => type switch
    {
        NoteType.BasicReversed => "reversed",
        NoteType.Cloze => "cloze",
        NoteType.ImageOcclusion => "occlusion",
        _ => "basic"
    };

    private static string At(IReadOnlyList<string> row, int i) => i < row.Count ? row[i] : string.Empty;

    // ---- RFC-4180-ish reader (handles quotes, escapes, embedded newlines) ----

    private static List<List<string>> ParseRows(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void EndField() { row.Add(field.ToString()); field.Clear(); }
        void EndRow() { EndField(); rows.Add(row); row = new List<string>(); }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) EndField();
            else if (c == '\r') { /* handled by \n */ }
            else if (c == '\n') EndRow();
            else field.Append(c);
        }

        // Trailing field/row if the text didn't end on a newline.
        if (field.Length > 0 || row.Count > 0) EndRow();
        return rows;
    }

    private static string Escape(string? field)
    {
        field ??= string.Empty;
        return field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\t')
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;
    }
}
