using System;
using System.Text.Json.Serialization;

namespace Tomoshibi.Models;

/// <summary>
/// One line in the append-only review log (reviewlog.jsonl, beside the state
/// file). Every grade writes one entry; the log drives the review heatmap,
/// true-retention stats and daily new/review counts, and is kept out of the
/// main state file so a heavy review session doesn't rewrite megabytes of JSON
/// on every card. Property names are terse to keep the file small.
/// </summary>
public class ReviewLogEntry
{
    [JsonPropertyName("ts")] public DateTime Timestamp { get; set; }

    [JsonPropertyName("c")] public Guid CardId { get; set; }

    [JsonPropertyName("d")] public Guid DeckId { get; set; }

    /// <summary>The grade pressed: 1 Again, 2 Hard, 3 Good, 4 Easy.</summary>
    [JsonPropertyName("g")] public int Grade { get; set; }

    /// <summary>The card's state <em>before</em> this review.</summary>
    [JsonPropertyName("st")] public CardState StateBefore { get; set; }

    /// <summary>Scheduled interval in days <em>before</em> this review — for the
    /// young/mature retention split.</summary>
    [JsonPropertyName("iv")] public double IntervalBefore { get; set; }

    /// <summary>Stability after this review.</summary>
    [JsonPropertyName("s")] public double Stability { get; set; }

    /// <summary>Difficulty after this review.</summary>
    [JsonPropertyName("df")] public double Difficulty { get; set; }

    /// <summary>Milliseconds spent on the card, if measured.</summary>
    [JsonPropertyName("ms")] public int? ElapsedMs { get; set; }
}
