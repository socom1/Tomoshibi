using System;
using System.Collections.Generic;
using System.Linq;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>The four review buttons. Values are the grade numbers logged and
/// used by the FSRS formulas (1 Again, 2 Hard, 3 Good, 4 Easy).</summary>
public enum ReviewGrade { Again = 1, Hard = 2, Good = 3, Easy = 4 }

/// <summary>A card selected for a review session, carried with the deck it
/// belongs to (for options + logging) and the note it renders from.</summary>
public sealed record ScheduledCard(Deck Deck, Note Note, Card Card);

/// <summary>
/// The scheduling state machine wrapped around the pure <see cref="Fsrs"/>
/// model: learning/relearning steps for new and lapsed cards, graduation to
/// the long FSRS schedule, interval fuzz, leech detection, and building the
/// due queue for a session. <see cref="Apply"/> mutates a card and returns the
/// log entry to persist; <see cref="Preview"/> computes the four button
/// intervals without touching the card.
/// </summary>
public static class Scheduler
{
    private static readonly double[] W = Fsrs.DefaultWeights;

    // ---- Public entry points ----

    /// <summary>Grade a card: advance its schedule, flag/suspend it if it has
    /// become a leech, and return the (unsaved) review-log entry describing the
    /// review. Mutates <paramref name="card"/> and possibly <paramref name="note"/>.</summary>
    public static ReviewLogEntry Apply(Deck deck, Note note, Card card,
                                       ReviewGrade grade, DateTime now, Random rng)
    {
        var stateBefore = card.State;
        var intervalBefore = card.LastReviewed.HasValue
            ? Math.Max(0, (card.Due - card.LastReviewed.Value).TotalDays)
            : 0;
        var lapsesBefore = card.Lapses;

        Compute(card, grade, deck.Options, now, rng);

        // Leech: once a card has lapsed threshold times (and every half-threshold
        // lapses after), flag it and — by default — suspend it.
        if (card.Lapses > lapsesBefore && card.Lapses >= deck.Options.LeechThreshold)
        {
            var over = card.Lapses - deck.Options.LeechThreshold;
            var every = Math.Max(1, deck.Options.LeechThreshold / 2);
            if (over % every == 0)
            {
                if (!note.Tags.Contains("leech"))
                    note.Tags.Add("leech");
                if (deck.Options.SuspendLeeches)
                    card.Suspended = true;
            }
        }

        return new ReviewLogEntry
        {
            Timestamp = now,
            CardId = card.Id,
            DeckId = deck.Id,
            Grade = (int)grade,
            StateBefore = stateBefore,
            IntervalBefore = intervalBefore,
            Stability = card.Stability,
            Difficulty = card.Difficulty
        };
    }

    /// <summary>The interval each of the four buttons would schedule, in order
    /// Again, Hard, Good, Easy — for the button labels. Deterministic (no fuzz)
    /// and non-destructive; display intervals are clamped non-decreasing.</summary>
    public static IReadOnlyList<(ReviewGrade Grade, TimeSpan Interval)> Preview(
        DeckOptions options, Card card, DateTime now)
    {
        var outcomes = new List<(ReviewGrade Grade, TimeSpan Interval)>();
        foreach (var g in new[] { ReviewGrade.Again, ReviewGrade.Hard, ReviewGrade.Good, ReviewGrade.Easy })
        {
            var clone = Clone(card);
            Compute(clone, g, options, now, rng: null);
            var span = clone.Due - now;
            outcomes.Add((g, span < TimeSpan.Zero ? TimeSpan.Zero : span));
        }

        // A harder button should never advertise a longer interval than an
        // easier one — clamp any inversion up for a sane-looking row.
        for (var i = 1; i < outcomes.Count; i++)
            if (outcomes[i].Interval < outcomes[i - 1].Interval)
                outcomes[i] = (outcomes[i].Grade, outcomes[i - 1].Interval);

        return outcomes;
    }

    /// <summary>A compact human label for a scheduled interval ("10m", "3d",
    /// "1.4mo", "2y").</summary>
    public static string FormatInterval(TimeSpan t)
    {
        if (t.TotalHours < 1) return $"{Math.Max(1, Math.Round(t.TotalMinutes))}m";
        if (t.TotalDays < 1) return $"{Math.Round(t.TotalHours)}h";
        var days = t.TotalDays;
        if (days < 30) return $"{Math.Round(days)}d";
        if (days < 365) return $"{days / 30.0:0.#}mo";
        return $"{days / 365.0:0.#}y";
    }

    /// <summary>Is this card available to study now (not suspended, not buried,
    /// and due)?</summary>
    public static bool IsDue(Card c, DateTime now)
    {
        if (c.Suspended) return false;
        var today = DateOnly.FromDateTime(now);
        if (c.BuriedUntil is { } b && b > today) return false;
        return c.State switch
        {
            CardState.New => true,
            CardState.Learning or CardState.Relearning => c.Due <= now,
            _ => DateOnly.FromDateTime(c.Due) <= today
        };
    }

    /// <summary>
    /// Build the study queue across the given decks: due learning/relearning
    /// cards, review cards (capped per deck by <c>ReviewsPerDay</c> minus those
    /// already done today), and new cards (capped by <c>NewPerDay</c> minus
    /// today's intake). Suspended and buried cards are excluded. The result is
    /// shuffled. Daily counts come from the two delegates so the queue builder
    /// stays independent of the log service.
    /// </summary>
    public static List<ScheduledCard> BuildQueue(
        IEnumerable<Deck> decks, DateTime now,
        Func<Guid, int> reviewsDoneToday, Func<Guid, int> newDoneToday, Random rng)
    {
        var today = DateOnly.FromDateTime(now);
        var queue = new List<ScheduledCard>();

        foreach (var deck in decks)
        {
            var live = deck.Notes
                .SelectMany(n => n.Cards.Select(c => (note: n, card: c)))
                .Where(x => !x.card.Suspended)
                .Where(x => x.card.BuriedUntil is not { } b || b <= today)
                .ToList();

            foreach (var x in live.Where(x =>
                         x.card.State is CardState.Learning or CardState.Relearning
                         && x.card.Due <= now))
                queue.Add(new ScheduledCard(deck, x.note, x.card));

            var reviewCap = Math.Max(0, deck.Options.ReviewsPerDay - reviewsDoneToday(deck.Id));
            foreach (var x in live
                         .Where(x => x.card.State == CardState.Review
                                     && DateOnly.FromDateTime(x.card.Due) <= today)
                         .OrderBy(x => x.card.Due)
                         .Take(reviewCap))
                queue.Add(new ScheduledCard(deck, x.note, x.card));

            var newCap = Math.Max(0, deck.Options.NewPerDay - newDoneToday(deck.Id));
            foreach (var x in live.Where(x => x.card.State == CardState.New).Take(newCap))
                queue.Add(new ScheduledCard(deck, x.note, x.card));
        }

        Shuffle(queue, rng);
        return queue;
    }

    // ---- Core state machine ----

    private static void Compute(Card c, ReviewGrade grade, DeckOptions opt, DateTime now, Random? rng)
    {
        var g = (int)grade;
        var elapsedDays = c.LastReviewed.HasValue
            ? Math.Max(0, (now - c.LastReviewed.Value).TotalDays)
            : 0;

        c.Reps++;

        switch (c.State)
        {
            case CardState.New:
                // First sight: seed memory from the grade, then step into
                // learning (or graduate straight away on Easy).
                c.Stability = Fsrs.InitialStability(g, W);
                c.Difficulty = Fsrs.InitialDifficulty(g, W);
                c.State = CardState.Learning;
                AdvanceSteps(c, opt.LearningStepsMinutes, grade, opt, now, rng);
                break;

            case CardState.Learning:
            case CardState.Relearning:
                c.Difficulty = Fsrs.NextDifficulty(c.Difficulty, g, W);
                c.Stability = Fsrs.ShortTermStability(c.Stability, g, W);
                AdvanceSteps(c,
                    c.State == CardState.Relearning ? opt.RelearningStepsMinutes : opt.LearningStepsMinutes,
                    grade, opt, now, rng);
                break;

            case CardState.Review:
                var r = Fsrs.Retrievability(elapsedDays, c.Stability);
                c.Difficulty = Fsrs.NextDifficulty(c.Difficulty, g, W);
                if (grade == ReviewGrade.Again)
                {
                    c.Lapses++;
                    c.Stability = Fsrs.NextStabilityLapse(c.Difficulty, c.Stability, r, W);
                    if (opt.RelearningStepsMinutes.Count > 0)
                    {
                        c.State = CardState.Relearning;
                        SetStep(c, opt.RelearningStepsMinutes, 0, now);
                    }
                    else
                    {
                        ScheduleReview(c, opt, now, rng);
                    }
                }
                else
                {
                    c.Stability = Fsrs.NextStabilitySuccess(c.Difficulty, c.Stability, r, g, W);
                    ScheduleReview(c, opt, now, rng);
                }
                break;
        }

        c.LastReviewed = now;
    }

    /// <summary>Move a New/Learning/Relearning card through its step list for
    /// the given grade, graduating when it walks past the last step.</summary>
    private static void AdvanceSteps(Card c, List<int> steps, ReviewGrade grade,
                                     DeckOptions opt, DateTime now, Random? rng)
    {
        if (steps.Count == 0)
        {
            Graduate(c, opt, now, rng);
            return;
        }

        switch (grade)
        {
            case ReviewGrade.Again:
                SetStep(c, steps, 0, now);
                break;
            case ReviewGrade.Hard:
                SetStepHard(c, steps, c.StepIndex, now);
                break;
            case ReviewGrade.Good:
                if (c.StepIndex + 1 >= steps.Count) Graduate(c, opt, now, rng);
                else SetStep(c, steps, c.StepIndex + 1, now);
                break;
            case ReviewGrade.Easy:
                Graduate(c, opt, now, rng);
                break;
        }
    }

    private static void SetStep(Card c, List<int> steps, int index, DateTime now)
    {
        c.StepIndex = Math.Clamp(index, 0, steps.Count - 1);
        c.Due = now.AddMinutes(steps[c.StepIndex]);
    }

    /// <summary>Hard on a step waits between this step and the next (or 1.5× the
    /// step if it's the last one), staying on the same step.</summary>
    private static void SetStepHard(Card c, List<int> steps, int index, DateTime now)
    {
        var i = Math.Clamp(index, 0, steps.Count - 1);
        c.StepIndex = i;
        var minutes = i + 1 < steps.Count ? (steps[i] + steps[i + 1]) / 2.0 : steps[i] * 1.5;
        c.Due = now.AddMinutes(minutes);
    }

    private static void Graduate(Card c, DeckOptions opt, DateTime now, Random? rng)
    {
        c.State = CardState.Review;
        c.StepIndex = 0;
        ScheduleReview(c, opt, now, rng);
    }

    private static void ScheduleReview(Card c, DeckOptions opt, DateTime now, Random? rng)
    {
        var ivl = Fsrs.Interval(opt.DesiredRetention, c.Stability);
        ivl = Fuzz(ivl, rng);
        var days = (int)Math.Round(Math.Clamp(ivl, 1, opt.MaxIntervalDays));
        days = Math.Clamp(days, 1, opt.MaxIntervalDays);
        c.Due = now.Date.AddDays(days);
    }

    /// <summary>Spread review intervals a little so cards learned together don't
    /// clump on the same future day. No fuzz under ~2.5 days or when
    /// <paramref name="rng"/> is null (preview).</summary>
    private static double Fuzz(double ivl, Random? rng)
    {
        if (rng is null || ivl < 2.5) return ivl;
        var pct = ivl < 7 ? 0.15 : ivl < 20 ? 0.10 : 0.05;
        var delta = ivl * pct;
        if (ivl >= 20) delta = Math.Min(delta, 4);
        var min = ivl - delta;
        var max = ivl + delta;
        return min + rng.NextDouble() * (max - min);
    }

    private static Card Clone(Card c) => new()
    {
        Id = c.Id, Ord = c.Ord, State = c.State, StepIndex = c.StepIndex,
        Stability = c.Stability, Difficulty = c.Difficulty, Due = c.Due,
        LastReviewed = c.LastReviewed, Reps = c.Reps, Lapses = c.Lapses,
        Suspended = c.Suspended, BuriedUntil = c.BuriedUntil
    };

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
