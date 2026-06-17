using System;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tomoshibi.Models;
using Tomoshibi.Services;

namespace Tomoshibi.ViewModels;

/// <summary>
/// The Pomodoro timer. A one-second tick drives a small state machine:
/// focus -> short break, with a long break after every Nth focus round,
/// then back to a fresh set.
/// </summary>
public partial class PomodoroViewModel : ViewModelBase
{
    private readonly Func<PomodoroSettings> _getSettings;
    private readonly ISoundService? _sound;
    private readonly INotificationService? _notify;
    private readonly DispatcherTimer _timer;

    // A second, faster timer purely for the braille spinner in the panel title.
    private readonly DispatcherTimer _spinTimer;
    private static readonly string[] SpinFrames =
        { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private int _spinIndex;

    private int _remainingSeconds;
    private int _round = 1; // current focus round within the set (1..RoundsBeforeLongBreak)

    // Captured when the phase starts so the phase keeps the length it began
    // with — changing settings or the active task mid-block neither warps the
    // progress bar nor mis-credits the stats.
    private int _phaseTotalSeconds;
    private int _phaseFocusMinutes;

    /// <summary>Raised when a focus block finishes; carries the focused minutes.
    /// Not raised for breaks or when the user skips.</summary>
    public event Action<int>? FocusSessionCompleted;

    /// <summary>Raised when any block finishes naturally (focus or break),
    /// carrying the phase that just ended and its focus minutes — drives the
    /// session log feed.</summary>
    public event Action<PomodoroPhase, int>? BlockCompleted;

    /// <summary>The current braille spinner frame, cycled while running and a
    /// steady glyph when idle.</summary>
    [ObservableProperty]
    private string _spinner = "·";

    [ObservableProperty]
    private PomodoroPhase _phase = PomodoroPhase.Focus;

    [ObservableProperty]
    private string _timeDisplay = "25:00";

    [ObservableProperty]
    private string _phaseLabel = "集中 · focus";

    /// <summary>Bare phase name for compact spots like the window title.</summary>
    [ObservableProperty]
    private string _phaseShortLabel = "focus";

    [ObservableProperty]
    private string _roundLabel = "● ○ ○ ○";

    /// <summary>0..1 fraction of the current phase still to run. Drains down.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Elapsed))]
    [NotifyPropertyChangedFor(nameof(Gauge))]
    [NotifyPropertyChangedFor(nameof(PercentLabel))]
    private double _progress = 1.0;

    /// <summary>0..1 fraction of the phase already done.</summary>
    public double Elapsed => Math.Clamp(1.0 - Progress, 0, 1);

    private const int GaugeWidth = 26;

    /// <summary>A terminal-style progress bar that fills as the phase elapses,
    /// e.g. "[//////////////.           ]" — '/' done, '.' the head, the rest
    /// blank, all monospace so the brackets stay put.</summary>
    public string Gauge
    {
        get
        {
            var filled = Math.Clamp((int)Math.Round(Elapsed * GaugeWidth), 0, GaugeWidth);
            var head = filled < GaugeWidth ? 1 : 0;
            var empty = GaugeWidth - filled - head;
            return "[" + new string('/', filled) + new string('.', head)
                       + new string(' ', empty) + "]";
        }
    }

    public string PercentLabel => $"{Elapsed * 100:0}%";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartPauseLabel))]
    private bool _isRunning;

    /// <summary>True when stopped mid-phase — lets the view dim the clock so
    /// paused doesn't look identical to not-started.</summary>
    [ObservableProperty]
    private bool _isPaused;

    public string StartPauseLabel => IsRunning ? "pause" : "start";

    /// <summary>Spin the spinner only while the clock runs; settle on a steady
    /// dot when it stops.</summary>
    partial void OnIsRunningChanged(bool value)
    {
        if (value)
        {
            _spinTimer.Start();
        }
        else
        {
            _spinTimer.Stop();
            Spinner = "·";
        }
    }

    /// <summary>Constructs the timer with a callback that returns the current
    /// effective settings (global, optionally overridden by the active task).
    /// The callback is called fresh each time a phase starts.</summary>
    public PomodoroViewModel(Func<PomodoroSettings> getSettings,
                             ISoundService? sound = null,
                             INotificationService? notify = null)
    {
        _getSettings = getSettings;
        _sound = sound;
        _notify = notify;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;

        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _spinTimer.Tick += (_, _) =>
        {
            _spinIndex = (_spinIndex + 1) % SpinFrames.Length;
            Spinner = SpinFrames[_spinIndex];
        };

        SetPhase(PomodoroPhase.Focus, resetRound: true);
    }

    /// <summary>Parameterless ctor for the XAML designer preview only.</summary>
    public PomodoroViewModel() : this(() => new PomodoroSettings())
    {
    }

    [RelayCommand]
    private void ToggleRun()
    {
        if (IsRunning)
        {
            _timer.Stop();
            IsRunning = false;
        }
        else
        {
            _timer.Start();
            IsRunning = true;
        }

        UpdatePaused();
    }

    [RelayCommand]
    private void Reset()
    {
        _timer.Stop();
        IsRunning = false;
        _remainingSeconds = _phaseTotalSeconds;
        UpdateTimeDisplay();
        UpdatePaused();
    }

    [RelayCommand]
    private void Skip()
    {
        // Move on without counting the current block.
        Advance(natural: false);
    }

    /// <summary>
    /// Called when timer settings change. If the timer is idle, refresh the
    /// current phase so the new length shows right away; if it's running, the
    /// next phase will pick the new values up.
    /// </summary>
    public void ApplySettings()
    {
        if (!IsRunning)
            SetPhase(Phase, resetRound: false);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
            UpdateTimeDisplay();
        }

        if (_remainingSeconds <= 0)
            Advance(natural: true);
    }

    private void Advance(bool natural)
    {
        _timer.Stop();
        IsRunning = false;

        // Captured before SetPhase overwrites them — the block that just ended.
        var finishedPhase = Phase;
        var finishedFocusMinutes = _phaseFocusMinutes;

        switch (Phase)
        {
            case PomodoroPhase.Focus:
                if (natural)
                    FocusSessionCompleted?.Invoke(_phaseFocusMinutes);

                var longDue = _round >= _getSettings().RoundsBeforeLongBreak;
                SetPhase(longDue ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak, resetRound: false);
                break;

            case PomodoroPhase.ShortBreak:
                _round++;
                SetPhase(PomodoroPhase.Focus, resetRound: false);
                break;

            case PomodoroPhase.LongBreak:
                SetPhase(PomodoroPhase.Focus, resetRound: true);
                break;
        }

        // A skip is the user intervening — let them choose when to resume.
        if (natural)
        {
            BlockCompleted?.Invoke(finishedPhase, finishedFocusMinutes);

            var s = _getSettings();

            if (s.ChimeEnabled)
                _sound?.PlayPhaseChime();

            if (s.NotificationsEnabled)
            {
                _notify?.Notify("灯火 · tomoshibi", Phase == PomodoroPhase.Focus
                    ? "break over — back to focus 集中"
                    : $"focus done — {PhaseShortLabel} 休憩");
            }

            if (s.AutoContinue)
            {
                _timer.Start();
                IsRunning = true;
            }
        }

        UpdatePaused();
    }

    private void SetPhase(PomodoroPhase phase, bool resetRound)
    {
        if (resetRound)
            _round = 1;

        var s = _getSettings();

        Phase = phase;
        _phaseTotalSeconds = phase switch
        {
            PomodoroPhase.Focus => s.FocusMinutes * 60,
            PomodoroPhase.ShortBreak => s.ShortBreakMinutes * 60,
            PomodoroPhase.LongBreak => s.LongBreakMinutes * 60,
            _ => s.FocusMinutes * 60
        };
        _phaseFocusMinutes = s.FocusMinutes;
        _remainingSeconds = _phaseTotalSeconds;

        PhaseLabel = phase switch
        {
            PomodoroPhase.Focus => "集中 · focus",
            PomodoroPhase.ShortBreak => "休憩 · short break",
            PomodoroPhase.LongBreak => "休憩 · long break",
            _ => "集中 · focus"
        };

        PhaseShortLabel = phase switch
        {
            PomodoroPhase.Focus => "focus",
            PomodoroPhase.ShortBreak => "short break",
            PomodoroPhase.LongBreak => "long break",
            _ => "focus"
        };

        // Dots read at a glance: filled = this round and the ones behind it.
        RoundLabel = phase switch
        {
            PomodoroPhase.Focus => string.Join(" ",
                Enumerable.Range(1, Math.Max(s.RoundsBeforeLongBreak, _round))
                          .Select(i => i <= _round ? "●" : "○")),
            PomodoroPhase.LongBreak => "long break",
            _ => "short break"
        };

        UpdateTimeDisplay();
        UpdatePaused();
    }

    private void UpdateTimeDisplay()
    {
        var span = TimeSpan.FromSeconds(_remainingSeconds);
        TimeDisplay = $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";

        Progress = _phaseTotalSeconds > 0
            ? (double)_remainingSeconds / _phaseTotalSeconds
            : 0.0;
    }

    private void UpdatePaused() =>
        IsPaused = !IsRunning && _remainingSeconds > 0 && _remainingSeconds < _phaseTotalSeconds;
}
