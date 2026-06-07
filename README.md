# tomoshibi 灯火

A calm, late-night study companion for university students — a Pomodoro timer, a
daily intention, focus stats and a course-tagged task list, wrapped in a dark
"Tokyo Night" coding aesthetic. Everything stays on your machine.

> *tomoshibi* (灯火) — a small light or lamp; the bit of light you keep on while
> you work into the night.

## Features

- **Pomodoro timer** — 25/5 focus and break cycles, with a longer break after
  every fourth round.
- **Daily intention** — one line to set the day's focus; resets each morning.
- **Focus stats** — sessions completed and hours focused today.
- **Tasks** — a simple list with course tags.
- **Local-first** — all data saved to a single JSON file on your computer; no
  account, no network.

Planned next: zen full-screen mode, a streak calendar, `.ics` timetable import,
system tray + notifications, and an ambient soundscape player. See
[docs/ROADMAP.md](docs/ROADMAP.md).

## Tech

Avalonia + .NET 8, MVVM with CommunityToolkit.Mvvm. One codebase, published
self-contained for Windows, macOS and Linux. Architecture notes are in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Project layout

```
Tomoshibi.sln
src/Tomoshibi/
  Models/        data classes (AppState, TaskItem, DailyStats, …)
  Services/      storage (JSON on disk, behind an interface)
  ViewModels/    UI state + behaviour
  Views/         .axaml UI
  Styles/        Tokyo Night palette + control styles
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

Self-contained, single-file builds per platform:

```bash
# Windows
dotnet publish src/Tomoshibi -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true

# macOS (Apple silicon)
dotnet publish src/Tomoshibi -c Release -r osx-arm64 --self-contained \
  -p:PublishSingleFile=true

# Linux
dotnet publish src/Tomoshibi -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true
```

Output lands under `src/Tomoshibi/bin/Release/net8.0/<rid>/publish/`.

## Where your data lives

A single `tomoshibi.json` file in your OS application-data folder:

- Windows — `%APPDATA%\Tomoshibi\`
- macOS — `~/Library/Application Support/Tomoshibi/`
- Linux — `~/.config/Tomoshibi/`

Delete it to reset the app to a clean state.

## License

MIT — see [LICENSE](LICENSE). *(Add a LICENSE file before publishing.)*
