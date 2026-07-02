# Architecture

A short tour of how tomoshibi is put together and why.

## The shape of it

tomoshibi is a single-project Avalonia desktop app on .NET 8, using MVVM via
CommunityToolkit.Mvvm. One codebase publishes to Windows, macOS and Linux as a
self-contained download — no runtime install required on the target machine.

The project follows a conventional layered layout:

```
src/Tomoshibi/
  Models/        plain data: AppState, DailyStats, ClassSlot, TodoItem,
                 Subject + Assessment, Deck + Flashcard, DayNote, the enums
                 (Destination, WeekDay, GradeScaleKind, …)
  Services/      side effects + pure helpers behind interfaces:
                 IStorageService (JSON on disk), ISoundService (chime),
                 INotificationService (native alerts), IMusicService,
                 TaskTemplateParser (task code grammar), IcsImporter,
                 DeckTsv (Anki-compatible deck import/export),
                 ReminderService (deadline alerts), ReviewScheduler (spaced
                 repetition), GradeScale, ThemeService, DailyReset (midnight
                 banking rules), StateMigrations (load-time upgrades)
  ViewModels/    UI state and behaviour — the MainWindow shell plus one view
                 model per destination (Dashboard / Today / Timetable / Todo /
                 Subjects / Stats / Review / Shop / Settings), and the Cmd-K
                 command palette
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

Two view models break the one-per-destination rule on purpose. The
**Dashboard** owns no data of its own — it takes the Today, Todo, Subjects and
Review view models in its constructor and snapshots their derived figures on
`Refresh()`, so the morning glance always agrees with the pages behind it. The
**command palette** (`Cmd/Ctrl+K`) is shell-level: the shell rebuilds its
candidate list each time it opens — pages, quick actions, every subject, and
the user's content (todo tickets, decks, journal reflections) — and each row
carries an `Action` that navigates and then reveals the target.

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
a crash. The one exception is Windows toasts: there is nothing to shell out
to, so the Windows-flavoured build (see the csproj's per-OS target framework)
uses the notifications toolkit, which also registers the app identity an
unpackaged EXE otherwise lacks.

## Data flow

```
launch ─▶ JsonStorageService.Load() ─▶ AppState
                                          │
                                          ▼
                                 MainWindowViewModel
                    (applies StateMigrations, then DailyReset, at load)
                       │        │        │        │        │
                       ▼        ▼        ▼        ▼        ▼
                    Today   Timetable  Todo   Subjects  Review  … (one per
                       │                                          destination)
                            two-way binding │ ▲
                                          ▼ │
                                       Views (.axaml)
                                          │
                       change ─▶ Save(AppState) back to disk
```

State is loaded once at startup into `AppState`. On load the shell applies
`StateMigrations` — the forward migrations (standalone deadlines → todo
tickets, legacy task list → template text, theme ids) — then `DailyReset`:
when the calendar date rolls over it banks the finished day's stats into
`History` and its intention + reflection into the `Journal`, then clears
them. Both are pure state-in/state-out services, kept out of the view models
so their rules stay unit-testable. Each destination's
view model exposes its slice as observable properties, and anything meaningful
writes the whole state back to disk through a short debounce. Persistence is
deliberately simple — serialise everything and replace the file — but never in
place: the save writes a temp file, rotates the previous good copy to `.bak`,
then swaps the temp in, so a crash mid-write leaves the old file or the backup
intact rather than a truncated half-state. The data is tiny.

The today task list is a special case: the persisted form is the *raw template
text* the user wrote, and `TaskTemplateParser` re-derives the task blocks on
every edit. Structured edits (the done checkbox, "send to today" from the
backlog) edit the text surgically rather than regenerating it, so the user's
own formatting survives.

## Theming

Colours, the monospace font and shape tokens live in `Styles/Palette.axaml` as
a merged resource dictionary. Reusable control styles (cards, buttons, inputs,
nav, headings) live in `Styles/Controls.axaml`. Views reference tokens through
`DynamicResource`, so the whole look is defined in one place. Because of that,
`ThemeService` can swap the palette at runtime: a light theme and the extra
themes bought in the shop are just alternate token sets, applied on launch
before the window shows so there's no flash.

## Testing approach

The pure logic is under xUnit tests: the grade engine, `TaskTemplateParser`
(parse + the done-toggle source surgery), storage round-trip and crash
recovery, the daily-reset/banking rules (`DailyReset`), the load-time
migrations (`StateMigrations`), `IcsImporter` and `DeckTsv`. The daily reset and the
migrations used to live inside the shell view model and were extracted into
plain state-in/state-out services precisely so they could be tested.

The one candidate still untested is the Pomodoro state machine — it's
entangled with `DispatcherTimer` and needs the same extraction treatment
before tests can drive it.

## Known limitations

- Save-on-change rewrites the entire file. Fine at this scale; revisit only if
  the data grows a lot.
- The package versions in the csproj are pinned to a known-good set and may
  lag the latest Avalonia release; bump deliberately.
- `.ics` import reads times as wall-clock and only maps weekly recurrences;
  exotic RRULEs are counted and skipped.
