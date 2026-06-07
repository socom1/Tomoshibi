# Architecture

A short tour of how tomoshibi is put together and why.

## The shape of it

tomoshibi is a single-project Avalonia desktop app on .NET 8, using MVVM via
CommunityToolkit.Mvvm. One codebase publishes to Windows, macOS and Linux as a
self-contained download — no runtime install required on the target machine.

The project follows a conventional layered layout:

```
src/Tomoshibi/
  Models/        plain data: AppState, TaskItem, DailyStats, PomodoroSettings
  Services/      side effects behind interfaces: IStorageService + JSON impl
  ViewModels/    UI state and behaviour, no Avalonia types
  Views/         .axaml + thin code-behind, no logic
  Styles/        Palette.axaml (tokens) + Controls.axaml (control styles)
  App.axaml      app entry, theme + resource wiring
  ViewLocator    maps a view model to its view by naming convention
```

## Why these choices

**Avalonia, not WPF/MAUI/Electron.** One XAML codebase that runs natively on all
three desktop platforms, with a real styling system. No browser runtime, no
per-platform UI fork.

**MVVM with the community toolkit.** The `[ObservableProperty]` and
`[RelayCommand]` source generators remove the usual `INotifyPropertyChanged`
boilerplate, so view models stay readable. Views bind to view models and hold no
logic of their own; view models never reference Avalonia, which keeps them
testable.

**Local-first JSON.** Everything lives in one JSON file in the OS app-data
folder. It's the simplest thing that survives restarts, it's easy to inspect, and
it keeps the app fully offline. No database, no accounts.

## Data flow

```
launch ─▶ JsonStorageService.Load() ─▶ AppState
                                          │
                                          ▼
                                 MainWindowViewModel
                                  (daily reset applied)
                                          │
                            two-way binding │ ▲
                                          ▼ │
                                       Views (.axaml)
                                          │
                       change ─▶ Save(AppState) back to disk
```

State is loaded once at startup into `AppState`. The main view model applies the
daily reset (clear stats and the intention when the calendar date has rolled
over), exposes the bits the UI needs as observable properties, and writes back to
disk when something meaningful changes. Persistence is deliberately dumb: serialise
the whole `AppState` and overwrite the file. The data is tiny, so there's no need
for anything cleverer.

## Theming

Colours, the monospace font and shape tokens live in `Styles/Palette.axaml` as a
merged resource dictionary. Reusable control styles (cards, buttons, headings)
live in `Styles/Controls.axaml`. Views reference tokens through `DynamicResource`,
so the whole look is defined in one place and a future light theme would be a
matter of swapping the dictionary.

## Testing approach

The logic worth testing has no UI dependency: the Pomodoro state machine (round
counting, when a long break is due) and the daily-reset rules. Those will get
plain xUnit tests in month 2. Views and styling are verified by eye.

## Known limitations

- Save-on-change rewrites the entire file. Fine at this scale; revisit only if the
  data grows a lot.
- The package versions in the csproj are pinned to a known-good set and may lag
  the latest Avalonia release; bump deliberately.
