using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The ダッシュボード · dashboard — the morning landing page that pulls a
/// glance of everything together: today's focus, the week's momentum, what's
/// next and due, the standing, the subjects that need work, and quick links.
/// It owns no data of its own; it reads the other destinations' view models
/// and snapshots the derived figures on <see cref="Refresh"/>.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly Action _save;
    private readonly Action<SubjectViewModel> _openSubject;
    private readonly Action _goToday;
    private readonly Action _goReview;
    private readonly Action<string> _openUrl;
    private readonly Action<Destination> _navigate;
    private readonly WalletViewModel _wallet;

    /// <summary>Exposed so the dashboard can bind their live labels directly
    /// (greeting, intention, stats, ticket counts, GPA, due cards).</summary>
    public TodayViewModel Today { get; }
    public TodoViewModel Todo { get; }
    public SubjectsViewModel Subjects { get; }
    public ReviewViewModel Review { get; }

    public ObservableCollection<WeakSpotViewModel> WeakSpots { get; } = new();
    public ObservableCollection<StudyLinkViewModel> Links { get; } = new();
    public ObservableCollection<AgendaDayViewModel> Agenda { get; } = new();

    [ObservableProperty] private string _dateLabel = string.Empty;
    [ObservableProperty] private int _sessionsThisWeek;
    [ObservableProperty] private string _momentumLabel = string.Empty;
    [ObservableProperty] private bool _hasWeakSpots;
    [ObservableProperty] private string _weakSpotsCaption = string.Empty;
    [ObservableProperty] private bool _hasAgenda;
    [ObservableProperty] private bool _hasLinks;

    // ---- Daily focus goal ----
    /// <summary>The editable target in minutes; writes through to state and
    /// re-derives the bar. 0 hides the goal.</summary>
    [ObservableProperty] private decimal _focusGoalMinutes;
    [ObservableProperty] private bool _hasFocusGoal;
    [ObservableProperty] private double _focusGoalFraction;
    [ObservableProperty] private string _focusGoalLabel = string.Empty;
    [ObservableProperty] private bool _isFocusGoalMet;

    // ---- First-run getting-started checklist ----
    public ObservableCollection<FirstStepViewModel> FirstSteps { get; } = new();

    /// <summary>Shown until the checklist is complete or skipped.</summary>
    [ObservableProperty] private bool _showGettingStarted;

    // ---- New-link form ----
    [ObservableProperty] private string _newLinkTitle = string.Empty;
    [ObservableProperty] private string _newLinkUrl = string.Empty;

    public DashboardViewModel(AppState state, Action save,
                              TodayViewModel today, TodoViewModel todo, SubjectsViewModel subjects,
                              ReviewViewModel review,
                              Action<SubjectViewModel> openSubject, Action goToday,
                              Action goReview, Action<string> openUrl,
                              Action<Destination> navigate, WalletViewModel wallet)
    {
        _state = state;
        _save = save;
        Today = today;
        Todo = todo;
        Subjects = subjects;
        Review = review;
        _openSubject = openSubject;
        _goToday = goToday;
        _goReview = goReview;
        _openUrl = openUrl;
        _navigate = navigate;
        _wallet = wallet;

        foreach (var link in _state.StudyLinks)
            Links.Add(WrapLink(link));
        HasLinks = Links.Count > 0;

        _focusGoalMinutes = _state.FocusGoalMinutes;

        Refresh();
    }

    /// <summary>Recompute the glance — called when the user lands here.</summary>
    public void Refresh()
    {
        Today.RefreshScheduleInfo();
        Review.Refresh();
        DateLabel = $"{DateTime.Now:dddd, MMMM d}".ToLowerInvariant();

        RecomputeMomentum();
        RecomputeFocusGoal();
        RebuildWeakSpots();
        RebuildAgenda();

        RebuildFirstSteps();
    }

    /// <summary>Build the first-run checklist, pay embers for each newly-done
    /// step, and retire the card once every step is done.</summary>
    private void RebuildFirstSteps()
    {
        if (_state.OnboardingDone)
        {
            ShowGettingStarted = false;
            FirstSteps.Clear();
            return;
        }

        var steps = new (string Key, string Label, bool Done, int Reward, Action Go)[]
        {
            ("subjects", "add your first subject", _state.Subjects.Any(), 10,
                () => _navigate(Destination.Subjects)),
            ("timetable", "set up your timetable", _state.ClassSlots.Any(), 10,
                () => _navigate(Destination.Timetable)),
            ("focus", "log a focus session",
                _state.History.Any() || _state.Today.CompletedSessions > 0, 10,
                () => _navigate(Destination.Today)),
            ("deck", "make a flashcard deck", _state.Decks.Any(), 10,
                () => _navigate(Destination.Review)),
        };

        // Pay out each step the first time it's complete (the wallet saves).
        foreach (var s in steps)
        {
            if (s.Done && !_state.OnboardingRewarded.Contains(s.Key))
            {
                _state.OnboardingRewarded.Add(s.Key);
                _wallet.Add(s.Reward);
            }
        }

        FirstSteps.Clear();
        foreach (var s in steps)
            FirstSteps.Add(new FirstStepViewModel(s.Label, s.Done, s.Reward, s.Go));

        if (steps.All(s => s.Done))
        {
            _state.OnboardingDone = true;
            _save();
        }

        ShowGettingStarted = !_state.OnboardingDone;
    }

    /// <summary>Dismiss the first-run checklist for good.</summary>
    [RelayCommand]
    private void SkipOnboarding()
    {
        _state.OnboardingDone = true;
        _save();
        ShowGettingStarted = false;
        FirstSteps.Clear();
    }

    /// <summary>Fill the grades tool with an example term so a first-timer sees
    /// it working straight away, then refresh so the checklist ticks over.</summary>
    [RelayCommand]
    private void LoadExample()
    {
        Subjects.LoadSampleCommand.Execute(null);
        Refresh();
    }

    partial void OnFocusGoalMinutesChanged(decimal value)
    {
        _state.FocusGoalMinutes = Math.Clamp((int)value, 0, 600);
        _save();
        RecomputeFocusGoal();
    }

    /// <summary>Today's focused minutes against the daily goal — the dashboard's
    /// quiet progress bar. Hidden when no goal is set.</summary>
    private void RecomputeFocusGoal()
    {
        var goal = _state.FocusGoalMinutes;
        HasFocusGoal = goal > 0;
        if (!HasFocusGoal)
        {
            FocusGoalLabel = string.Empty;
            FocusGoalFraction = 0;
            IsFocusGoalMet = false;
            return;
        }

        var done = _state.Today.FocusedMinutes;
        FocusGoalFraction = Math.Clamp(done / goal, 0.0, 1.0);
        IsFocusGoalMet = done >= goal;
        FocusGoalLabel = IsFocusGoalMet
            ? $"focus goal met — {Services.FocusLog.HoursLabel(done)} today"
            : $"{Services.FocusLog.HoursLabel(done)} of {Services.FocusLog.HoursLabel(goal)} · daily focus goal";
    }

    /// <summary>Jump to the review page and start working through the due
    /// cards — the dashboard's one-click into recall practice.</summary>
    [RelayCommand]
    private void StartReview() => _goReview();

    /// <summary>The next 7 days, each with its classes, due tickets and exams,
    /// in date order. Empty days are skipped so the card stays tight.</summary>
    private void RebuildAgenda()
    {
        Agenda.Clear();
        var today = DateOnly.FromDateTime(DateTime.Now);

        for (var offset = 0; offset < 7; offset++)
        {
            var date = today.AddDays(offset);
            var weekday = (Models.WeekDay)(((int)date.DayOfWeek + 6) % 7);

            var day = new AgendaDayViewModel
            {
                IsToday = offset == 0,
                DayLabel = offset == 0 ? "today"
                         : offset == 1 ? "tomorrow"
                         : $"{date:ddd MMM d}".ToLowerInvariant()
            };

            foreach (var slot in _state.ClassSlots
                         .Where(s => s.Day == weekday)
                         .OrderBy(s => s.Start))
            {
                var course = string.IsNullOrWhiteSpace(slot.Course) ? "" : $" ({slot.Course})";
                day.Entries.Add(new AgendaEntryViewModel
                {
                    IsClass = true,
                    Text = $"{slot.Start:HH\\:mm}  {slot.Title}{course}"
                });
            }

            foreach (var t in _state.Todos
                         .Where(t => t.Status != Models.TodoStatus.Done && t.Due == date)
                         .OrderBy(t => t.Title))
            {
                day.Entries.Add(new AgendaEntryViewModel { IsDue = true, Text = $"due · {t.Title}" });
            }

            foreach (var (subject, exam) in _state.Subjects
                         .SelectMany(s => s.Assessments
                             .Where(a => a.Grade is null && a.Date == date)
                             .Select(a => (s, a))))
            {
                var code = subject.Code ?? subject.Name;
                day.Entries.Add(new AgendaEntryViewModel { IsExam = true, Text = $"exam · {exam.Title} ({code})" });
            }

            if (day.Entries.Count > 0)
                Agenda.Add(day);
        }

        HasAgenda = Agenda.Count > 0;
    }

    private void RecomputeMomentum()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        int sessionsBetween(DateOnly from, DateOnly to)
        {
            var n = _state.History
                .Where(d => d.Date >= from && d.Date <= to)
                .Sum(d => d.CompletedSessions);
            if (_state.Today.Date >= from && _state.Today.Date <= to)
                n += _state.Today.CompletedSessions;
            return n;
        }

        SessionsThisWeek = sessionsBetween(today.AddDays(-6), today);
        var lastWeek = sessionsBetween(today.AddDays(-13), today.AddDays(-7));

        MomentumLabel = (SessionsThisWeek, lastWeek) switch
        {
            (0, 0) => "no focus sessions yet — start one",
            var (t, l) when t > l => $"↑ up from {l} last week",
            var (t, l) when t < l => $"↓ down from {l} last week",
            _ => "same as last week"
        };
    }

    private void RebuildWeakSpots()
    {
        WeakSpots.Clear();

        var graded = Subjects.Items.Where(s => s.CurrentPercent is not null).ToList();
        if (graded.Count == 0)
        {
            HasWeakSpots = false;
            WeakSpotsCaption = "grade some assessments and the weak spots surface here";
            return;
        }

        var credits = graded.Sum(s => s.Model.Credits);
        var avg = graded.Sum(s => s.CurrentPercent!.Value * s.Model.Credits) / credits;

        var spots = new List<(double score, WeakSpotViewModel vm)>();

        foreach (var subject in graded)
        {
            var pct = subject.CurrentPercent!.Value;
            string? message = null;
            double score = 0;
            var severe = false;

            if (subject.Model.TargetPercent is { } target && pct < target)
            {
                var gap = target - pct;
                message = $"{gap:0.#}% below your {target:0.#}% target";
                score = gap + 100; // targets outrank generic under-average
                severe = gap > 10;
            }
            else if (pct < avg - 5)
            {
                var gap = avg - pct;
                message = $"{gap:0.#}% below your {avg:0.#}% average";
                score = gap;
                severe = gap > 15;
            }

            if (message is null)
                continue;

            // Ground it in effort: how long has this course been studied this
            // week? Low time on a weak subject is the real call to action.
            if (subject.HasCode)
            {
                var mins = Services.FocusLog.MinutesForCourse(_state, subject.Code, 7);
                message += $" · {Services.FocusLog.HoursLabel(mins)} studied this week";
                if (mins < 30) score += 5; // nudge the neglected ones up
            }

            var captured = subject;
            spots.Add((score, new WeakSpotViewModel(
                subject.Name, subject.Code, message, severe,
                open: () => _openSubject(captured),
                sendToToday: () => SendSubjectToToday(captured))));
        }

        foreach (var spot in spots.OrderByDescending(x => x.score).Take(5))
            WeakSpots.Add(spot.vm);

        HasWeakSpots = WeakSpots.Count > 0;
        WeakSpotsCaption = HasWeakSpots
            ? "click to open · → drops a focus task on today"
            : "nothing's lagging — you're on top of it";
    }

    private void SendSubjectToToday(SubjectViewModel subject)
    {
        var block = $"// work on {subject.Name}";
        if (subject.HasCode)
            block += $"\ncourse: {subject.Code}";

        var existing = (Today.Tasks.Source ?? string.Empty).TrimEnd();
        Today.Tasks.Source = string.IsNullOrEmpty(existing) ? block : $"{existing}\n\n{block}";
        _goToday();
    }

    [RelayCommand]
    private void AddLink()
    {
        var url = NewLinkUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;

        // Be forgiving — a bare youtube.com/… still opens fine once schemed.
        if (!url.Contains("://"))
            url = "https://" + url;

        var link = new StudyLink { Title = NewLinkTitle?.Trim() ?? string.Empty, Url = url };
        _state.StudyLinks.Add(link);
        Links.Add(WrapLink(link));
        HasLinks = true;

        NewLinkTitle = string.Empty;
        NewLinkUrl = string.Empty;
        _save();
    }

    private StudyLinkViewModel WrapLink(StudyLink link) =>
        new(link, l => _openUrl(l.Url), RemoveLink);

    private void RemoveLink(StudyLinkViewModel row)
    {
        _state.StudyLinks.Remove(row.Model);
        Links.Remove(row);
        HasLinks = Links.Count > 0;
        _save();
    }
}
