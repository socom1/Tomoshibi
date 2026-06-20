# tomoshibi 灯火

[![ci](https://github.com/socom1/Tomoshibi/actions/workflows/ci.yml/badge.svg)](https://github.com/socom1/Tomoshibi/actions/workflows/ci.yml)

A calm, late-night study companion for university students. A Pomodoro timer
and a daily intention sit at the centre; around them are a class timetable, a
todo backlog, spaced-repetition flashcards, a grade tracker and a focus
journal — pulled together on a morning dashboard and reachable from a Cmd/K
command palette. Wrapped in a dark "Tokyo Night" coding aesthetic, and
everything stays on your machine.

> *tomoshibi* (灯火) — a small light or lamp; the bit of light you keep on while
> you work into the night.

## Features

- **Dashboard** — a morning landing page that pulls a glance together: today's
  intention and focus, the week's momentum, the next seven days' agenda, what's
  due, your grade standing, the subjects that need work, and quick links.
- **Pomodoro timer** — focus/break cycles with a longer break after every Nth
  round, a soft chime and a native notification on phase change, auto-continue,
  and a progress bar. The active task can override the phase lengths.
- **Zen mode** — full-screen, just the clock, your intention and the controls.
- **Daily intention & journal** — one line to set the day's focus, an
  end-of-day reflection on how it went; both bank into a journal look-back at
  the midnight rollover.
- **Tasks as code** — today's plan written in a tiny template grammar
  (`// title`, `study: 25`, `course: MATH101`, `done`), edited in a simple
  list, a form, or raw source. Click a task to make it drive the timer.
- **Timetable** — weekly class schedule on a week grid (or list) plus dated
  deadlines, with `.ics` import for university timetable exports.
- **Todo backlog** — longer-horizon coursework as numbered tickets with
  statuses, priorities, due dates, effort estimates and subtask checklists;
  send an item to today's plan with one click.
- **Subjects & grades** — track assessments per subject against a grade scale
  (US GPA, letter bands or your own custom boundaries), weight years, set an
  overall goal and see what each remaining piece needs to hit it.
- **Flashcards** — spaced-repetition decks with a review queue that schedules
  cards by how well you recall them.
- **Focus stats & streak** — a month calendar tinted by focus, current and best
  streak, a 14-day sparkline, focus-by-course, and the journal look-back.
- **Command palette** — `Cmd/Ctrl+K` to jump to any page, run a quick action,
  or search straight to a subject, todo ticket, deck or past reflection.
- **Deadline reminders** — desktop notifications as exams and due dates
  approach, fired once on the way in and again on the day.
- **Themes & embers** — earn embers as you focus and spend them in a small shop
  on extra colour themes; a music player can loop a local folder while you work.
- **Local-first** — all data in a single JSON file on your computer, written
  atomically with a `.bak` fallback; no account, no network.

Roadmap and longer-term ideas live in [docs/ROADMAP.md](docs/ROADMAP.md).

## Tech

Avalonia + .NET 8, MVVM with CommunityToolkit.Mvvm. One codebase, published
self-contained for Windows, macOS and Linux. Architecture notes are in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); the full build story is in
[docs/BUILD_JOURNAL.md](docs/BUILD_JOURNAL.md).

## Project layout

```
Tomoshibi.sln
src/Tomoshibi/
  Models/        data classes (AppState, ClassSlot, TodoItem, Subject,
                 Deck, DayNote, …)
  Services/      side effects behind interfaces: storage, sound, music,
                 notifications + reminders, the task-template parser, the
                 .ics importer, grade scales and the review scheduler
  ViewModels/    UI state + behaviour, one per destination + the shell
  Views/         .axaml UI (MainWindow shell + one view per destination:
                 Dashboard, Today, Timetable, Todo, Subjects, Stats,
                 Review, Shop, Settings)
  Styles/        Tokyo Night palette + control styles
  Assets/        icon (png/ico/icns) + chime
tests/Tomoshibi.Tests/   xUnit tests for the pure logic (grade engine,
                 task-template parser, storage round-trip + crash recovery)
scripts/         packaging scripts per platform
docs/            roadmap, architecture, build journal
.github/workflows/ci.yml   build + test on every push/PR, across win/mac/linux
```

## Getting started

You'll need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone <your-repo-url> tomoshibi
cd tomoshibi
dotnet restore
dotnet run --project src/Tomoshibi
```

Run the tests with:

```bash
dotnet test
```

## Building a release

Use the packaging scripts — they handle the per-platform wrapping:

```bash
# macOS: a proper Tomoshibi.app with the dock icon (tested)
./scripts/pack-mac.sh

# Linux: tar.gz with a .desktop launcher (written, not yet run on Linux)
./scripts/pack-linux.sh

# Windows: zip of a self-contained folder (written, not yet run on Windows)
pwsh scripts/pack-win.ps1
```

> Note: avoid `-p:PublishSingleFile=true` on macOS — SkiaSharp's native
> library doesn't survive single-file extraction there, which is why
> `pack-mac.sh` ships the publish folder inside the .app instead.

## Where your data lives

A single `tomoshibi.json` file in your OS application-data folder:

- Windows — `%APPDATA%\Tomoshibi\`
- macOS — `~/Library/Application Support/Tomoshibi/`
- Linux — `~/.config/Tomoshibi/`

Delete it to reset the app to a clean state.

## License

MIT — see [LICENSE](LICENSE).
