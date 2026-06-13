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
    private readonly Action<string> _openUrl;

    /// <summary>Exposed so the dashboard can bind their live labels directly
    /// (greeting, intention, stats, ticket counts, GPA).</summary>
    public TodayViewModel Today { get; }
    public TodoViewModel Todo { get; }
    public SubjectsViewModel Subjects { get; }

    public ObservableCollection<WeakSpotViewModel> WeakSpots { get; } = new();
    public ObservableCollection<StudyLinkViewModel> Links { get; } = new();

    [ObservableProperty] private string _dateLabel = string.Empty;
    [ObservableProperty] private int _sessionsThisWeek;
    [ObservableProperty] private string _momentumLabel = string.Empty;
    [ObservableProperty] private bool _hasWeakSpots;
    [ObservableProperty] private string _weakSpotsCaption = string.Empty;
    [ObservableProperty] private bool _hasLinks;

    // ---- New-link form ----
    [ObservableProperty] private string _newLinkTitle = string.Empty;
    [ObservableProperty] private string _newLinkUrl = string.Empty;

    public DashboardViewModel(AppState state, Action save,
                              TodayViewModel today, TodoViewModel todo, SubjectsViewModel subjects,
                              Action<SubjectViewModel> openSubject, Action goToday, Action<string> openUrl)
    {
        _state = state;
        _save = save;
        Today = today;
        Todo = todo;
        Subjects = subjects;
        _openSubject = openSubject;
        _goToday = goToday;
        _openUrl = openUrl;

        foreach (var link in _state.StudyLinks)
            Links.Add(WrapLink(link));
        HasLinks = Links.Count > 0;

        Refresh();
    }

    /// <summary>Recompute the glance — called when the user lands here.</summary>
    public void Refresh()
    {
        Today.RefreshScheduleInfo();
        DateLabel = $"{DateTime.Now:dddd, MMMM d}".ToLowerInvariant();

        RecomputeMomentum();
        RebuildWeakSpots();
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
