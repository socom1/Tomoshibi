# tomoshibi 灯火

A calm, late-night study companion for university students — a Pomodoro timer,
a daily intention, a code-style task list, a class timetable and a todo
backlog, wrapped in a dark "Tokyo Night" coding aesthetic. Everything stays on
your machine.

> *tomoshibi* (灯火) — a small light or lamp; the bit of light you keep on while
> you work into the night.

## Features

- **Pomodoro timer** — focus/break cycles with a longer break after every Nth
  round, a soft chime and a native notification on phase change, auto-continue,
  and a progress bar. The active task can override the phase lengths.
- **Zen mode** — full-screen, just the clock, your intention and the controls.
- **Daily intention** — one line to set the day's focus; resets each morning
  and follows you into zen mode.
- **Tasks as code** — today's plan written in a tiny template grammar
  (`// title`, `study: 25`, `course: MATH101`, `done`), edited in a simple
  list, a form, or raw source. Click a task to make it drive the timer.
- **Timetable** — weekly class schedule on a week grid (or list) plus dated
  deadlines, with `.ics` import for university timetable exports.
- **Todo backlog** — longer-horizon coursework with due dates; send an item to
  today's plan with one click.
- **Focus stats & streak** — sessions and hours today, day streak, and a
  14-day dot history.
- **Local-first** — all data in a single JSON file on your computer; no
  account, no network.

Still planned: streak calendar view, system tray icon, ambient soundscapes.
See [docs/ROADMAP.md](docs/ROADMAP.md).

## Tech

Avalonia + .NET 8, MVVM with CommunityToolkit.Mvvm. One codebase, published
self-contained for Windows, macOS and Linux. Architecture notes are in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); the full build story is in
[docs/BUILD_JOURNAL.md](docs/BUILD_JOURNAL.md).

## Project layout

```
Tomoshibi.sln
src/Tomoshibi/
  Models/        data classes (AppState, TaskBlock, ClassSlot, TodoItem, …)
  Services/      side effects behind interfaces: storage, sound,
                 notifications, the task-template parser, the .ics importer
  ViewModels/    UI state + behaviour, one per destination + the shell
  Views/         .axaml UI (MainWindow shell + Today / Timetable / Todo pages)
  Styles/        Tokyo Night palette + control styles
  Assets/        icon (png/ico/icns) + chime
scripts/         packaging scripts per platform
docs/            roadmap, architecture, build journal
```

## Getting started

You'll need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone <your-repo-url> tomoshibi
cd tomoshibi
dotnet restore
dotnet run --project src/Tomoshibi
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
