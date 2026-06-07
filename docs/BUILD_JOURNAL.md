# Tomoshibi — Build Journal

This is my running record of building **tomoshibi**, a calm late-night study
companion for students. I'm writing it as I go, partly so I don't forget why I
made the choices I made, and partly because I want to be able to talk through the
whole thing later — every decision here is one I should be able to explain.

The plan is to get a solid **version 1** working (Pomodoro timer, a daily
intention, focus stats, a task list, settings), tidy it up, and only then start
committing properly. Until then this journal *is* the history.

---

## The idea

I wanted a study app that feels like the late-night coding sessions I actually
enjoy — dark, quiet, a bit of a Japanese "lofi" feel — instead of the usual loud
productivity dashboard. The core is small on purpose:

- a Pomodoro timer (25 minutes focus, 5 minute breaks, a longer break every 4th round),
- one line where I set the day's intention,
- focus stats for the day (sessions and hours), reset each morning,
- a task list with course tags,
- everything saved locally so it just works offline.

I gave it the name *tomoshibi* (灯火) — "a small light / lamp" — the bit of light
you keep on while you work into the night. The whole UI leans on a dark "Tokyo
Night" palette with a monospace font, bilingual JP/EN labels, and rounded cards.

Longer term I want zen/full-screen focus mode, a streak calendar, an `.ics`
timetable import for deadlines, a system tray with notifications, and an ambient
soundscape player. Those are the roadmap, not v1.

---

## Picking the stack

I want one app that runs on Windows, macOS and Linux, looks the same everywhere,
and ships as a download people don't have to install a runtime for. I went with
**Avalonia on .NET 8**, using **MVVM** with the **CommunityToolkit.Mvvm**
source generators.

Why Avalonia and not the obvious alternatives:

- **WPF** is Windows-only, so it's out for a cross-platform app.
- **Electron** means shipping a browser; I didn't want the size or the feel.
- **MAUI** leans mobile-first and its desktop story felt less solid for what I want.

Avalonia gives me real XAML, a proper styling system, and a single codebase that
publishes self-contained for all three desktops. CommunityToolkit.Mvvm removes
the `INotifyPropertyChanged` boilerplate with `[ObservableProperty]` and
`[RelayCommand]`, which keeps the view models readable.

I decided up front on a conventional layered layout so there's an obvious home
for everything: **Models / Services / ViewModels / Views / Styles**.

---

## Day 1 (2026-06-07)

### Setting up the project

First I laid down the skeleton: a solution file, one project under
`src/Tomoshibi`, the folder structure above, `Program.cs`, the app manifest, and
`App.axaml` to wire up the theme and resources. I turned on compiled bindings by
default (`AvaloniaUseCompiledBindingsByDefault`) because I'd rather catch binding
mistakes at build time than watch them fail silently at runtime.

A couple of things worth recording:

- I pinned all the Avalonia packages to one version through an `AvaloniaVersion`
  MSBuild property. Avalonia is picky about its packages all matching, so keeping
  them on a single property means I can bump them together and never end up with a
  mismatch.
- I added `Avalonia.Diagnostics` but only for Debug builds, so I get the F12
  inspector while developing without shipping it.
- I included a `ViewLocator` even though the app is one window for now. It maps a
  view model to its view by naming convention, and I'll want it the moment I split
  features into their own views.

**Wrinkle I hit:** I'd cloned an empty repo into a subfolder, so when I scaffolded
the project the files landed one level *above* the actual git repo — the project
and the `.git` were in two different places. I fixed it by moving the repo
metadata up so the project folder itself is the repository root, then deleted the
leftover stub. Clean single-level layout now.

**Thing I couldn't verify:** the machine I scaffolded on didn't have the .NET SDK,
so I couldn't compile as I went. I validated all the XAML as well-formed XML and
reviewed the code by hand, but the first real `dotnet restore` + `dotnet run` is
still the thing to confirm. Noting it so I don't forget.

### The look — Tokyo Night

I split the theme into two files on purpose. `Styles/Palette.axaml` is a resource
dictionary of *tokens*: the colours (ink `#16161E`, surface `#1F2335`, matcha
`#9ECE6A`, sakura `#F7768E`, amber `#E0AF68`, blue `#7AA2F7`, muted text), the
monospace font family, and shape values like the card corner radius.
`Styles/Controls.axaml` holds the actual control styles built from those tokens —
cards, buttons, headings, the JP labels.

I reference the tokens with `DynamicResource` rather than `StaticResource`. It's a
little more forgiving for theme lookups, and it means if I ever add a light theme
it's just a matter of swapping the dictionary — none of the views need to change.

For the font I used a monospace family with per-OS fallbacks (Cascadia / JetBrains
Mono / Consolas / Menlo / DejaVu Sans Mono) so it looks like a coding font
everywhere without me having to bundle a file yet. Bundling a specific font is a
later polish step.

### Saving state — models and storage

I kept persistence as simple as it can be: one `AppState` object serialised to a
single JSON file, behind an `IStorageService` interface so the rest of the app
depends on the contract and not the "JSON on disk" detail.

The models are plain data classes: `AppState` (the root — intention, today's
stats, tasks, settings), `TaskItem`, `DailyStats`, `PomodoroSettings`, and a
`PomodoroPhase` enum. The storage implementation, `JsonStorageService`, writes to
the OS application-data folder (`%APPDATA%` / `~/Library/Application Support` /
`~/.config`) so the data survives reinstalls and never lands in the repo. Load is
wrapped in a try/catch that falls back to a fresh state if the file is missing or
corrupt — I don't want a bad file to stop the app from launching.

The **daily reset** logic lives in the main view model: on launch it compares the
stored date to today, and if the day has rolled over it clears the stats and the
intention. Simple, and it means I don't need a background job.

### The Pomodoro timer

This is the heart of the app, so I gave it its own `PomodoroViewModel` driven by a
one-second `DispatcherTimer`.

The logic is a small state machine. A focus block runs down; when it finishes it
either goes to a short break, or — if it was the 4th focus round — a long break,
and after a long break it starts a fresh set. Breaks just lead back to focus. I
exposed start/pause, reset and skip as relay commands. Skip moves on *without*
counting the block, which matters for the stats.

The view model doesn't touch any stats itself. Instead, when a focus block
genuinely completes it raises a `FocusSessionCompleted` event, and the main view
model listens for that, bumps today's session count and focused minutes, and
saves. I like that the timer stays focused (no pun intended) on timing, and the
persistence stays in one place.

One detail I'm happy with: each phase gets its own accent colour — matcha for
focus, blue for a short break, sakura for a long break — through a tiny
`PhaseToBrushConverter` that looks the brush up from the theme. The view model
just exposes the phase enum; the colour mapping stays in the UI layer where it
belongs, so the view model never references a brush.

### The task list

Coursework lives in a `TasksViewModel`. The tricky bit with a list like this is
that ticking a task needs to update the UI *and* save, so I wrapped each
`TaskItem` model in a small `TaskItemViewModel` that exposes an observable
`IsDone`. Flipping it updates the underlying model (which is the same instance
stored in `AppState`) and fires a change event the list uses to save. Adding a
task takes a title and an optional course tag; the tag shows as a little chip.
There's a remove button per row and an empty state when there's nothing yet.

Two small things I'm pleased with: done tasks get a strikethrough through a
**conditional class** (`Classes.done="{Binding IsDone}"`) rather than a converter,
which feels cleaner; and adding works either from the **+** button or by pressing
Enter in the title box, wired with a `KeyBinding`.

**Wrinkle:** the per-row remove button needs to call a command that lives on the
parent list, not on the row's own view model. With compiled bindings I reached it
with an ancestor lookup and a cast —
`{Binding $parent[ItemsControl].((vm:MainWindowViewModel)DataContext).Tasks.RemoveTaskCommand}`
— passing the row as the command parameter. It's the one binding in the app I want
to double-check once it compiles.

### Settings

Last for the core: editable timer lengths. I put a gear button in the header that
opens a flyout with four `NumericUpDown`s — focus, short break, long break, and
rounds before a long break — backed by a `SettingsViewModel`.

NumericUpDown works in `decimal`, so the view model exposes decimals and casts to
`int` when it writes them onto the shared settings object. Each edit writes
straight through, saves, and tells the timer to refresh: if the timer is idle it
re-reads the current phase so a change shows immediately, and if it's running the
next phase picks the new values up. So you can shorten a focus block and see the
clock update without restarting.

---

## Where v1 stands

The core app is essentially done. What works right now:

- The window opens on a dark Tokyo Night layout with a time-of-day greeting.
- The daily intention line saves and resets each day.
- The Pomodoro timer runs the full 25/5 cycle with a long break every 4th round,
  with start/pause, reset and skip, and colour that changes by phase.
- Completed focus blocks increment today's sessions and hours, and persist.
- The task list adds, completes, tags and removes tasks, and they survive
  relaunch.
- The settings flyout changes the timer lengths live.

The file map as it stands:

```
src/Tomoshibi/
  Models/        AppState, TaskItem, DailyStats, PomodoroSettings, PomodoroPhase
  Services/      IStorageService, JsonStorageService
  ViewModels/    MainWindow, Pomodoro, Tasks, TaskItem, Settings, ViewModelBase
  Views/         MainWindow (.axaml + code-behind)
  Converters/    PhaseToBrushConverter
  Styles/        Palette (tokens) + Controls (styles)
  App.axaml, Program.cs, ViewLocator, app.manifest
docs/            ROADMAP, ARCHITECTURE, this journal
```

## What's left before I tag v1.0

I'm cutting a **lean v1.0** — the core app — and shipping the bigger roadmap
features (timetable, zen mode, streaks, tray, soundscapes) as v1.1 and on. So
what's left before the tag is just a short polish pass:

- Run a clean `dotnet restore` + `dotnet run` and fix anything the compiler
  flags — top of the list is the remove-button binding and the converter's theme
  lookup.
- A small UI pass: spacing, the empty states, maybe a touch of motion, and
  checking the layout at the minimum window size.
- Decide on bundling a real coding font for a consistent look across OSes.
- Sanity-check the daily reset around midnight.

After that I'll cut v1.0 and begin the real commit history; the roadmap features
above become later versions.

---

## Decisions log (notes to myself)

- **One JSON file, save-on-change.** Rewrites the whole file every time, which is
  fine at this size. If the data ever grows a lot, revisit — but not before.
- **View models never reference Avalonia.** Colours, brushes and controls stay in
  the views and converters, so the logic is testable on its own.
- **Pinned package versions.** Deliberately on a known-good set; bump on purpose,
  not by accident, and keep all the Avalonia packages in lockstep.
- **`TryGetResource` in the converter.** If a given Avalonia version doesn't expose
  it, the fallback is `TryFindResource(key, out var brush)`.
- **Testing plan.** The logic worth testing has no UI: the timer state machine
  (round counting, when a long break is due) and the daily-reset rules. Those get
  plain unit tests as the first thing after v1.
