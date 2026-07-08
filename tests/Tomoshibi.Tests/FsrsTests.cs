using System;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The FSRS-5 formulas are the heart of scheduling; a transcription
/// slip would quietly warp every interval. These pin the exact anchors that are
/// hand-computable and the monotonic relationships the model must always
/// satisfy.</summary>
public class FsrsTests
{
    private static readonly double[] W = Fsrs.DefaultWeights;

    [Fact]
    public void Retrievability_is_one_at_zero_elapsed_and_falls_over_time()
    {
        Assert.Equal(1.0, Fsrs.Retrievability(0, 10), 9);

        var day1 = Fsrs.Retrievability(1, 10);
        var day5 = Fsrs.Retrievability(5, 10);
        var day20 = Fsrs.Retrievability(20, 10);
        Assert.True(day1 > day5 && day5 > day20);
    }

    [Fact]
    public void Retrievability_equals_desired_retention_when_elapsed_matches_stability()
    {
        // By construction, a memory reviewed exactly `stability` days later sits
        // at 0.9 recall — the default target.
        Assert.Equal(0.9, Fsrs.Retrievability(10, 10), 6);
    }

    [Fact]
    public void Interval_at_ninety_percent_retention_equals_stability()
    {
        foreach (var s in new[] { 1.0, 5.0, 37.5, 400.0 })
            Assert.Equal(s, Fsrs.Interval(0.9, s), 9);
    }

    [Fact]
    public void Higher_desired_retention_gives_shorter_intervals()
    {
        Assert.True(Fsrs.Interval(0.95, 100) < Fsrs.Interval(0.9, 100));
        Assert.True(Fsrs.Interval(0.9, 100) < Fsrs.Interval(0.8, 100));
    }

    [Fact]
    public void Initial_stability_is_the_weight_for_the_grade()
    {
        Assert.Equal(W[0], Fsrs.InitialStability(1, W), 9);
        Assert.Equal(W[1], Fsrs.InitialStability(2, W), 9);
        Assert.Equal(W[2], Fsrs.InitialStability(3, W), 9);
        Assert.Equal(W[3], Fsrs.InitialStability(4, W), 9);
    }

    [Fact]
    public void Initial_difficulty_for_again_is_w4_and_stays_in_range()
    {
        // grade 1 → exp(0) = 1, so D0 = w[4] - 1 + 1 = w[4].
        Assert.Equal(W[4], Fsrs.InitialDifficulty(1, W), 9);

        for (var g = 1; g <= 4; g++)
        {
            var d = Fsrs.InitialDifficulty(g, W);
            Assert.InRange(d, 1.0, 10.0);
        }
        // Easier grades start easier (lower difficulty).
        Assert.True(Fsrs.InitialDifficulty(4, W) < Fsrs.InitialDifficulty(1, W));
    }

    [Fact]
    public void Success_stability_grows_with_an_easier_grade()
    {
        var hard = Fsrs.NextStabilitySuccess(5, 5, 0.9, 2, W);
        var good = Fsrs.NextStabilitySuccess(5, 5, 0.9, 3, W);
        var easy = Fsrs.NextStabilitySuccess(5, 5, 0.9, 4, W);

        Assert.True(hard < good && good < easy);
        // A successful review never shrinks stability.
        Assert.True(good >= 5);
    }

    [Fact]
    public void Lapse_stability_never_exceeds_the_old_stability()
    {
        var lapse = Fsrs.NextStabilityLapse(5, 20, 0.85, W);
        Assert.True(lapse <= 20);
        Assert.True(lapse > 0);
    }

    [Fact]
    public void Difficulty_rises_on_again_and_falls_on_easy()
    {
        const double d = 5;
        Assert.True(Fsrs.NextDifficulty(d, 1, W) > d);
        Assert.True(Fsrs.NextDifficulty(d, 4, W) < d);

        // Stays clamped even from an extreme.
        Assert.InRange(Fsrs.NextDifficulty(10, 1, W), 1.0, 10.0);
        Assert.InRange(Fsrs.NextDifficulty(1, 4, W), 1.0, 10.0);
    }

    [Fact]
    public void Short_term_stability_orders_by_grade()
    {
        var again = Fsrs.ShortTermStability(5, 1, W);
        var hard = Fsrs.ShortTermStability(5, 2, W);
        var good = Fsrs.ShortTermStability(5, 3, W);
        var easy = Fsrs.ShortTermStability(5, 4, W);

        Assert.True(again < hard && hard < good && good < easy);
        // Good short-term multiplier = e^(w17*w18).
        Assert.Equal(5 * Math.Exp(W[17] * W[18]), good, 6);
    }
}
