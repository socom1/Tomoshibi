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

---

## Post-v1.0 polish (2026-06-07)

First clean compile on this machine — `dotnet restore` and `dotnet run` both went
straight through on Avalonia 11.2.1. The two spots I'd flagged as risky (the
`$parent[ItemsControl]` cast for the per-row remove button, and the converter's
`TryGetResource` call) both work as written, so the binding lookup and the theme
resource lookup are sound on this version.

Three small things I cleaned up before moving on:

- **Midnight reset for the always-on case.** The original reset only ran in the
  constructor, so the app left open through midnight would still show yesterday's
  stats and intention. I lifted the reset rules into `ApplyDailyReset(today)`,
  made `Greeting` an observable property, and added a one-minute `DispatcherTimer`
  in the main view model that refreshes the greeting on every tick and re-applies
  the reset when the calendar date has rolled over. One minute is plenty granular
  for a stats-and-greeting refresh and is cheap.
- **Empty task state.** The old "no tasks yet" caption sat just under the input
  row and felt accidental. Wrapped the list area in a Grid that holds both the
  `ScrollViewer` and a centered `課題はまだない · no tasks yet` label, with their
  visibility flipped by `HasTasks`. The empty state now occupies the same space
  the list will and matches the bilingual JP/EN style used by the other section
  labels.
- **Settings flyout caption.** The original "changes apply from the next round"
  was wrong — when the timer is idle the change shows immediately; only a
  *running* phase has to finish first. New caption is
  "applies live · running phase finishes first", which is what the code actually
  does.

---

## v1.1 — zen focus mode (2026-06-07)

The first standout feature: a full-screen layout that strips the UI back to the
timer. The idea is that when I genuinely sit down to work, I don't want the
intention line, stats, tasks, or even the chrome around the clock — I want a
lamp and a number ticking down.

**Entry and exit.** A small `⛶` button in the header next to the gear toggles
zen on. To exit there's a `✕` in the top-right of the zen view and, more
importantly, Escape — wired as a Window-level `KeyBinding` to an `ExitZenCommand`
whose `CanExecute` is gated by `IsZenMode`, so pressing Escape outside zen is a
no-op rather than a half-state toggle.

**What's shown.** Phase label, the time display blown up to ~180pt and coloured
by the same phase brush converter, the round indicator, and the start/pause,
reset, skip controls. Nothing else. Background is the same ink colour, so the
clock floats on the dark.

**Layout structure.** I kept the existing layout intact and wrapped it, plus a
new minimal zen panel, in a single outer `Grid`. Their visibility flips on
`IsZenMode`. That's lighter than building a second window or a hosted view, and
it means the view-model state (running timer, settings) is shared by definition
rather than synchronised. The Pomodoro view model is reused untouched — zen is
purely a view concern.

**Window state.** The view model is deliberately Avalonia-free, so I didn't want
it to know about `WindowState`. The code-behind subscribes to the view model's
`PropertyChanged`, and on `IsZenMode` changes it flips `WindowState` between
`FullScreen` and `Normal`. Avalonia restores the prior size on its own when
coming back from full screen, so I didn't need to remember it. On macOS this
also rides the system's native full-screen so the menu bar gets out of the way.

**Not persisted.** `IsZenMode` lives only on the view model, not in `AppState`.
Every session starts in normal view. The feeling I'm after is that zen is a
deliberate action you take, not a state you fall back into.

**Things I deliberately left out.** No animation on the transition; no
fade-on-idle for the buttons; no minimum-size protection for the 180pt clock on
small displays. All three are easy follow-ups if they prove worth it after I've
used the feature for a bit.

---

## v1.2 — nav sidebar + timetable (2026-06-08)

Up to v1.1 the whole app lived inside one window with one layout. For v1.2 I
wanted to add a second page (a class-schedule timetable), and rather than
bolt it on as another section, I took the moment to put a proper
**navigation sidebar** in front of the app. The "today" view becomes the
first destination behind the nav; the timetable becomes the second. New
features land as new destinations from here on out.

### The shape of it

The window content is now: nav rail on the left, a 1-pixel divider, then the
main content area. The nav rail holds one button per destination. The main
area is a `ContentControl` whose `Content` is bound to `MainWindowViewModel.
ActiveContent` — a `ViewModelBase` that points at either the today or
timetable view model depending on the current `Destination` enum. The
existing `ViewLocator` resolves the bound view model to its matching view by
the `Foo.ViewModels.BarViewModel → Foo.Views.BarView` naming convention, so
adding a third destination is just three files: an enum entry, a view model,
and a view.

I'd originally written the timetable feature as *a sidebar that contained
the timetable*, with the timetable's data and controls living in the panel
itself. The nav reframing replaces that: the sidebar is now navigation
chrome and the timetable is a page that opens in the main area when you pick
it. The plumbing is the same; what changed is what the sidebar holds.

### Extracting "today"

The main view model used to own the daily intention, Pomodoro timer, today's
focus stats and the task list directly. With the timetable arriving as a
peer page, that arrangement stopped being neutral — anything in the main VM
implicitly belonged to the today page. I lifted those fields and methods
into a new `TodayViewModel` and added a corresponding `TodayView`
UserControl that hosts the intention card and the body grid. The
`MainWindowViewModel` now owns the chrome (greeting, nav state, zen state,
active destination, the settings flyout) and exposes two destinations
(`Today` and `Timetable`) plus an `ActiveContent` switch.

The day-watcher logic (the once-a-minute `DispatcherTimer` that re-checks
the calendar date for the midnight reset) stays in the main view model
because it's not Today-specific; on a rollover it calls
`Today.RefreshFromState()` to pull the cleared stats and intention back into
the today view model's observable properties.

### Nav rail

A narrow vertical strip of `Button.nav` controls, one per destination. The
selector style draws a 3-pixel left edge that's transparent by default and
matcha on `.active`, plus a muted text colour that goes to full text colour
on `.active`. The active class is bound to a `bool` per destination on the
main view model (`IsTodayActive`, `IsTimetableActive`), each computed from
the `ActiveDestination` enum with `NotifyPropertyChangedFor` so they
re-evaluate when the enum flips. A `☰` button in the top-left of the main
header toggles the whole rail open or closed; both the open/closed state and
the active destination persist in `AppState`, so the app comes back the way
you left it.

The rail's three column widths are flipped from code-behind on
`IsNavOpen` change: open is `Auto / 1px / *` (rail sized to its content,
divider 1px, main takes the rest); closed is `0 / 0 / *`. Keeping this in
code-behind avoided a `BoolToGridLength` converter for two lines of logic
and kept the view model Avalonia-free.

### Timetable destination

Two new plain data classes — `ClassSlot` (`Day`/`Start`/`End`/`Title`/
`Course`) and `Deadline` (`Date`/`Title`/`Course`) — plus a `WeekDay` enum
ordered Mon-first. `Start` and `End` are `TimeOnly`; `Date` is `DateOnly`.
Both lists hang off `AppState` and persist through the existing JSON
storage.

`TimetableViewModel` wraps the two model lists in observable collections of
small row view models (`ClassSlotItemViewModel`, `DeadlineItemViewModel`),
each with the read-only display strings the row template binds to. The
wrappers exist so the row template stays XAML-only — no converters, no
string formatting in markup. Past deadlines flag themselves via an `IsPast`
property that toggles the same `done` strikethrough style the task list
uses, so a stale entry visibly fades.

Add/remove for both lists goes through `[RelayCommand]` methods that mutate
the underlying `AppState` lists, insert the new row at the right sorted
position in the observable collection, and trigger a single save. Sorting
is by `(Day, Start)` for slots and by `Date` for deadlines, so the lists
are always in reading order without any re-sorting work on render.

**Course autocomplete.** The entry forms autocomplete the course field from
a `KnownCourses` collection that's the union of every course string seen
across tasks, slots and deadlines — distinct, case-insensitive, sorted. So
once "MATH101" exists anywhere it's offered everywhere it could be used
again. The list is rebuilt on construction (snapshotting task courses) and
on every timetable mutation; new tasks added mid-session won't appear until
relaunch, which I accepted as a fair tradeoff for not coupling the
two view models.

**Forms.** Each section header has a `+` icon button that opens a flyout
with the entry form — same pattern as the settings flyout. The slot form
uses an Avalonia `TimePicker` (24-hour) for start/end and a `ComboBox` over
the `WeekDay` enum; the deadline form uses a `CalendarDatePicker`. Both
forms validate by silently no-op'ing on empty titles or `end <= start`
rather than throwing errors at the user — at this scale, no-feedback is
acceptable; I'll add inline validation when there's more than one way to
fail.

### Week grid

Deadlines stay as a sorted list in their own card at the top of the
timetable view. Class slots render as a proper week grid below: hour axis
on the left, seven day columns Mon–Sun, slot blocks placed in their day's
column at their start hour. Keeping deadlines and slots visually separate
("what's due" vs "when you have class") was much cleaner than trying to
overlay deadlines onto day columns, and lets each section evolve
independently.

The grid is a single `Grid` with eight columns (a 28px time axis + seven
star-sized day columns) and fifteen rows (a 22px header row for day labels
+ fourteen 38px hour rows covering 08:00 through 22:00). Day labels and
hour labels are static `TextBlock`s — they don't change, so they're
binding-free. Slots are placed by an `ItemsControl` that overlays rows 1–14
and columns 1–7 and uses an inner `Grid` as its `ItemsPanel`, with each
item's `ContentPresenter` styled with `Grid.Row`, `Grid.Column` and
`Grid.RowSpan` bound to properties on the slot row view model.

`ClassSlotItemViewModel` exposes `DayIndex`, `HourRow` and `HourSpan` for
this. `HourRow` is the start hour minus 8, clamped to the visible range;
`HourSpan` rounds the duration up to the nearest hour (so a 09:30–10:50
block lands in rows 1–2 — visually 09:00–11:00). Snapping to the hour means
the layout is a clean integer `Grid` placement instead of fractional
`Canvas` math, and the slot block still shows the true 09:30–10:50 in its
label so the information isn't lost. If I ever want to-the-minute precision
I can swap the inner panel for a `Canvas` with `Canvas.Top` bound to pixel
offsets without touching the rest.

The grid is naturally responsive: it sits in the main content area and the
seven day columns are star-sized, so widening the window widens the
columns. No fixed pixel widths anywhere on the grid itself.

**Slot block visuals.** A new `Border.slot` style: a translucent matcha
fill (`#339ECE6A`) with a solid matcha left edge for a sticky-note feel.
Title and course are stacked inside, both with `TextTrimming` so a long
title gets an ellipsis instead of overflowing. A faint `✕` in the top-right
of each block goes to full opacity on hover via the existing
`Button.icon:pointerover` rule — same delete affordance as elsewhere,
visually quieter.

### Gotcha — compiled bindings inside an `ItemsControl.Styles` block

First build failed with `AVLN2000: Unable to resolve property or method of
name 'HourRow' on type 'TimetableViewModel'`. The Setter bindings in the
style were being compiled against the *outer* view model rather than the
item type, because the markup compiler can't infer that the
`ContentPresenter` selector applies to slot rows. Annotating the `Style`
itself with `x:DataType="vm:ClassSlotItemViewModel"` told the compiler what
type the Setter bindings target, and it resolves cleanly. Noting it so I
don't spend ten minutes on it again the next time I do a Style-driven item
container.

### Not yet

No overlap handling — two slots on the same day and hour overlap visually;
rare in practice and obvious when it happens. No clip for slots outside
08:00–22:00 (start is clamped; ends are bounded by the grid). And no `.ics`
import — that's the next focused commit.
