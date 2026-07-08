using System;

namespace Tomoshibi.Services;

/// <summary>
/// The FSRS-5 memory model — pure, deterministic arithmetic with the default
/// published parameters. Given a card's current stability/difficulty and how
/// well it was just recalled (grade 1–4), it returns the updated memory state
/// and the interval to the next review. Kept dependency-free and static (like
/// <see cref="GradeScale"/>) so it's trivially testable against reference
/// implementations (ts-fsrs / py-fsrs).
/// </summary>
public static class Fsrs
{
    /// <summary>Default FSRS-5 weights (19 parameters).</summary>
    public static readonly double[] DefaultWeights =
    {
        0.40255, 1.18385, 3.173, 15.69105, 7.1949, 0.5345, 1.4604, 0.0046,
        1.54575, 0.1192, 1.01925, 1.9395, 0.11, 0.29605, 2.2698, 0.2315,
        2.9898, 0.51655, 0.6621
    };

    private const double Decay = -0.5;

    /// <summary>Constant in the forgetting curve: 0.9^(1/DECAY) − 1 = 19/81.</summary>
    private static readonly double Factor = Math.Pow(0.9, 1.0 / Decay) - 1.0;

    /// <summary>Recall probability of a memory with stability <paramref name="s"/>
    /// after <paramref name="elapsedDays"/> days.</summary>
    public static double Retrievability(double elapsedDays, double s)
    {
        if (s <= 0) return 0;
        var t = Math.Max(0, elapsedDays);
        return Math.Pow(1 + Factor * t / s, Decay);
    }

    /// <summary>Days until recall probability decays to
    /// <paramref name="desiredRetention"/> for a memory of stability
    /// <paramref name="s"/>. At r = 0.9 this equals the stability.</summary>
    public static double Interval(double desiredRetention, double s)
    {
        var r = Math.Clamp(desiredRetention, 0.01, 0.99);
        return s / Factor * (Math.Pow(r, 1.0 / Decay) - 1.0);
    }

    /// <summary>Initial stability for a never-seen card, from the first grade.</summary>
    public static double InitialStability(int grade, double[] w)
        => Math.Max(0.1, w[grade - 1]);

    /// <summary>Initial difficulty (1–10) for a never-seen card.</summary>
    public static double InitialDifficulty(int grade, double[] w)
        => ClampDifficulty(w[4] - Math.Exp(w[5] * (grade - 1)) + 1);

    /// <summary>Difficulty after a review: linear damping toward the grade,
    /// then mean-reversion toward the "easy" initial difficulty.</summary>
    public static double NextDifficulty(double d, int grade, double[] w)
    {
        var delta = -w[6] * (grade - 3);
        var damped = d + delta * (10 - d) / 9.0;
        var reverted = w[7] * InitialDifficulty(4, w) + (1 - w[7]) * damped;
        return ClampDifficulty(reverted);
    }

    /// <summary>Stability after a successful review (grade ≥ 2).
    /// <paramref name="r"/> is the retrievability at review time.</summary>
    public static double NextStabilitySuccess(double d, double s, double r, int grade, double[] w)
    {
        var hard = grade == 2 ? w[15] : 1.0;
        var easy = grade == 4 ? w[16] : 1.0;
        var inc = Math.Exp(w[8]) * (11 - d) * Math.Pow(s, -w[9])
                  * (Math.Exp(w[10] * (1 - r)) - 1) * hard * easy + 1;
        return s * inc;
    }

    /// <summary>Stability after a lapse (grade = 1). Never exceeds the old
    /// stability.</summary>
    public static double NextStabilityLapse(double d, double s, double r, double[] w)
    {
        var post = w[11] * Math.Pow(d, -w[12]) * (Math.Pow(s + 1, w[13]) - 1)
                   * Math.Exp(w[14] * (1 - r));
        return Math.Min(post, s);
    }

    /// <summary>Short-term stability change for a same-day / learning-step
    /// review, where the classic curve doesn't apply.</summary>
    public static double ShortTermStability(double s, int grade, double[] w)
        => s * Math.Exp(w[17] * (grade - 3 + w[18]));

    private static double ClampDifficulty(double d) => Math.Clamp(d, 1.0, 10.0);
}
