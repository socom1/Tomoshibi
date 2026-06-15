using System;
using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>How well a card was recalled — the three review buttons.</summary>
public enum ReviewGrade { Again, Good, Easy }

/// <summary>
/// SM-2-lite spaced repetition. <c>Good</c> roughly multiplies the interval by
/// the ease factor; <c>Easy</c> stretches it further and lifts the ease;
/// <c>Again</c> lapses the card back to due-now and trims the ease. Kept tiny
/// and deterministic so the schedule is easy to reason about.
/// </summary>
public static class ReviewScheduler
{
    public static void Apply(Flashcard c, ReviewGrade grade, DateOnly today)
    {
        switch (grade)
        {
            case ReviewGrade.Again:
                c.Reps = 0;
                c.Interval = 0;
                c.Ease = Math.Max(1.3, c.Ease - 0.2);
                c.Due = today; // still due — comes back this session / today
                break;

            case ReviewGrade.Good:
                c.Reps++;
                c.Interval = c.Reps switch
                {
                    1 => 1,
                    2 => 3,
                    _ => (int)Math.Round(Math.Max(1, c.Interval) * c.Ease)
                };
                c.Due = today.AddDays(c.Interval);
                break;

            case ReviewGrade.Easy:
                c.Reps++;
                c.Ease += 0.15;
                c.Interval = c.Reps == 1
                    ? 2
                    : (int)Math.Round(Math.Max(1, c.Interval) * c.Ease * 1.3);
                c.Due = today.AddDays(Math.Max(2, c.Interval));
                break;
        }

        c.LastReviewed = today;
    }

    public static bool IsDue(Flashcard c, DateOnly today) => c.Due <= today;
}
