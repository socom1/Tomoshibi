using System;
using System.Collections.Generic;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>
/// Append-only store of every review, kept beside the main state file. Backs
/// the review heatmap, true-retention stats, and the daily new/review caps.
/// </summary>
public interface IReviewLogService
{
    /// <summary>Append one review; flushes to disk immediately.</summary>
    void Append(ReviewLogEntry entry);

    /// <summary>Every logged review, oldest first (lazily loaded, cached).</summary>
    IReadOnlyList<ReviewLogEntry> All();

    /// <summary>How many cards of a given prior state were graded in a deck on a
    /// given day — <see cref="CardState.New"/> for the new-cards-introduced
    /// count, <see cref="CardState.Review"/> for reviews done.</summary>
    int CountToday(Guid deckId, CardState stateBefore, DateOnly today);
}
