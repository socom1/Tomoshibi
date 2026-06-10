using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Parses the user's "task code" into <see cref="TaskBlock"/>s. The grammar:
/// <code>
/// // title for the task          (the comment is the title)
/// study: 25                       (focus minutes)
/// short: 5                        (short break minutes)
/// long: 15                        (long break minutes)
/// course: MATH101                 (course tag)
/// done                            (bare flag — task completed)
///
/// // another task                 (blank line separates blocks)
/// study: 50
/// </code>
/// Blocks come back in source order — no explicit sort key, the user just
/// writes them in the order they want them to appear.
/// </summary>
public static class TaskTemplateParser
{
    public static List<TaskBlock> Parse(string? source)
    {
        var blocks = new List<TaskBlock>();
        if (string.IsNullOrWhiteSpace(source))
            return blocks;

        TaskBlock? current = null;

        foreach (var rawLine in source.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                if (current is not null && IsMeaningful(current))
                    blocks.Add(current);
                current = null;
                continue;
            }

            current ??= new TaskBlock();

            if (line.StartsWith("//"))
            {
                var titleText = line[2..].TrimStart();
                if (string.IsNullOrEmpty(current.Title))
                    current.Title = titleText;
                continue;
            }

            if (line.Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                current.IsDone = true;
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            switch (key)
            {
                case "study":
                case "focus":
                    if (TryParseMinutes(value, out var s)) current.Study = s;
                    break;
                case "short":
                    if (TryParseMinutes(value, out var sh)) current.Short = sh;
                    break;
                case "long":
                    if (TryParseMinutes(value, out var l)) current.Long = l;
                    break;
                case "course":
                    if (!string.IsNullOrWhiteSpace(value))
                        current.Course = value;
                    break;
            }
        }

        if (current is not null && IsMeaningful(current))
            blocks.Add(current);

        return blocks;
    }

    /// <summary>Turn a list of legacy <see cref="TaskItem"/>s into a starter
    /// template string. Used once on load to migrate users from the old
    /// checkbox list to the code editor without losing their tasks.</summary>
    public static string FromTaskItems(IReadOnlyList<TaskItem> items)
    {
        if (items.Count == 0)
            return string.Empty;

        var lines = new List<string>();

        foreach (var task in items)
        {
            lines.Add($"// {task.Title}");

            if (!string.IsNullOrWhiteSpace(task.Course))
                lines.Add($"course: {task.Course}");

            if (task.IsDone)
                lines.Add("done");

            lines.Add(string.Empty);
        }

        return string.Join("\n", lines).TrimEnd();
    }

    /// <summary>
    /// Toggle the <c>done</c> flag on the block whose title matches, editing
    /// the source surgically so the user's own formatting elsewhere survives.
    /// Unknown title → source returned unchanged.
    /// </summary>
    public static string ToggleDone(string source, string title)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrWhiteSpace(title))
            return source;

        var lines = source.Replace("\r\n", "\n").Split('\n').ToList();

        // Find the comment line carrying this title.
        var titleLine = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("//") && line[2..].TrimStart() == title)
            {
                titleLine = i;
                break;
            }
        }
        if (titleLine < 0)
            return source;

        // Walk out to the block's blank-line boundaries.
        var blockStart = titleLine;
        while (blockStart - 1 >= 0 && lines[blockStart - 1].Trim().Length > 0)
            blockStart--;

        var blockEnd = titleLine;
        while (blockEnd + 1 < lines.Count && lines[blockEnd + 1].Trim().Length > 0)
            blockEnd++;

        // Existing done line? Remove it. Otherwise append one to the block.
        for (var i = blockStart; i <= blockEnd; i++)
        {
            if (lines[i].Trim().Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                lines.RemoveAt(i);
                return string.Join("\n", lines);
            }
        }

        lines.Insert(blockEnd + 1, "done");
        return string.Join("\n", lines);
    }

    /// <summary>Accept either a bare integer ("25") or a "25m" form so the
    /// user can type either.</summary>
    private static bool TryParseMinutes(string value, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.TrimEnd('m', 'M', 'i', 'n', ' ', '\t');
        if (int.TryParse(trimmed, out var n) && n > 0)
        {
            minutes = n;
            return true;
        }
        return false;
    }

    private static bool IsMeaningful(TaskBlock b) =>
        !string.IsNullOrWhiteSpace(b.Title) ||
        b.Study is not null ||
        b.Short is not null ||
        b.Long is not null ||
        b.Course is not null ||
        b.IsDone;
}
