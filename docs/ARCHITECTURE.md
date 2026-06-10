# Architecture

A short tour of how tomoshibi is put together and why.

## The shape of it

tomoshibi is a single-project Avalonia desktop app on .NET 8, using MVVM via
CommunityToolkit.Mvvm. One codebase publishes to Windows, macOS and Linux as a
self-contained download — no runtime install required on the target machine.

The project follows a conventional layered layout:

```
src/Tomoshibi/
  Models/        plain data: AppState, DailyStats, TaskBlock, ClassSlot,
                 Deadline, TodoItem, the enums (Destination, WeekDay, …)
  Services/      side effects + pure helpers behind interfaces:
                 IStorageService (JSON on disk), ISoundService (chime),
                 INotificationService (native alerts),
                 TaskTemplateParser (task code grammar), IcsImporter
  ViewModels/    UI state and behaviour — the MainWindow shell plus one view
                 model per destination (Today / Timetable / Todo)
  Views/         .axaml + thin code-behind (window-state, focus checks,
                 file pickers — things only the view layer can know)
  Styles/        Palette.axaml (tokens) + Controls.axaml (control styles)
  Assets/        app icon (png/ico/icns) + the phase chime
  App.axaml      app entry, theme + resource wiring
  ViewLocator    maps a view model to its view by naming convention
```

## Navigation

The window is a shell: a collapsible nav rail on the left, and a main content
area driven by a `Destination` enum. `MainWindowViewModel.ActiveContent`
returns the active destination's view model, a `TransitioningContentControl`
crossfades between them, and the `ViewLocator` resolves each view model to its
view by naming convention. Adding a destination = an enum entry, a view model,
a view, and a nav button.

## Why these choices

**Avalonia, not WPF/MAUI/Electron.** One XAML codebase that runs natively on
all three desktop platforms, with a real styling system. No browser runtime,
no per-platform UI fork.

**MVVM with the community toolkit.** The `[ObservableProperty]` and
`[RelayCommand]` source generators remove the usual `INotifyPropertyChanged`
boilerplate, so view models stay readable. Views bind to view models and hold
no logic of their own. View models avoid Avalonia *UI* types (no brushes,
controls or windows) so the logic stays testable; the one deliberate exception
is `DispatcherTimer` for the second-tick and the day-watcher.

**Local-first JSON.** Everything lives in one JSON file in the OS app-data
folder. It's the simplest thing that survives restarts, it's easy to inspect,
and it keeps the app fully offline. No database, no accounts.

**Side effects shell out.** Sound and notifications call the OS's own tools
(afplay / osascript on macOS, paplay / notify-send on Linux) instead of
pulling in audio or notification libraries. A missing tool means silence, not
a crash.

## Data flow

```
launch ─▶ JsonStorageService.Load() ─▶ AppState
                                          │
                                          ▼
                                 MainWindowViewModel
                          (daily reset + history banking)
                              │           │          │
                              ▼           ▼          ▼
                        TodayViewModel  Timetable  Todo
                                          │
                            two-way binding │ ▲
                                          ▼ │
                                       Views (.axaml)
                                          │
                       change ─▶ Save(AppState) back to disk
```

State is loaded once at startup into `AppState`. The shell applies the daily
reset (clear stats and the intention when the calendar date rolls over,
banking the finished day into `History` first), each destination's view model
exposes its slice as observable properties, and anything meaningful writes the
whole state back to disk. Persistence is deliberately dumb: serialise
everything, overwrite the file. The data is tiny.

The today task list is a special case: the persisted form is the *raw template
text* the user wrote, and `TaskTemplateParser` re-derives the task blocks on
every edit. Structured edits (the done checkbox, "send to today" from the
backlog) edit the text surgically rather than regenerating it, so the user's
own formatting survives.

## Theming

Colours, the monospace font and shape tokens live in `Styles/Palette.axaml` as
a merged resource dictionary. Reusable control styles (cards, buttons, inputs,
nav, headings) live in `Styles/Controls.axaml`. Views reference tokens through
`DynamicResource`, so the whole look is defined in one place and a future
light theme would be a matter of swapping the dictionary.

## Testing approach

Deliberately deferred for now. The logic that would be tested first when that
changes: the Pomodoro state machine, the daily-reset/history rules,
`TaskTemplateParser` (parse + the done-toggle source surgery) and
`IcsImporter`. All are pure and UI-free by design, so the door stays open.

## Known limitations

- Save-on-change rewrites the entire file. Fine at this scale; revisit only if
  the data grows a lot.
- The package versions in the csproj are pinned to a known-good set and may
  lag the latest Avalonia release; bump deliberately.
- `.ics` import reads times as wall-clock and only maps weekly recurrences;
  exotic RRULEs are counted and skipped.
- Windows has no native notification yet (the chime still fires); toast
  notifications need an app identity that plain unpackaged EXEs don't have.
