namespace Tomoshibi.ViewModels;

/// <summary>One line in the subjects "insights" card — a short read on how the
/// term is going, tinted matcha / amber / sakura by tone.</summary>
public class GradeInsightViewModel : ViewModelBase
{
    public string Text { get; }
    public bool IsGood { get; }
    public bool IsWarn { get; }
    public bool IsBad { get; }

    private GradeInsightViewModel(string text, bool good, bool warn, bool bad)
    {
        Text = text;
        IsGood = good;
        IsWarn = warn;
        IsBad = bad;
    }

    public static GradeInsightViewModel Good(string text) => new(text, true, false, false);
    public static GradeInsightViewModel Warn(string text) => new(text, false, true, false);
    public static GradeInsightViewModel Bad(string text) => new(text, false, false, true);
    public static GradeInsightViewModel Neutral(string text) => new(text, false, false, false);
}
