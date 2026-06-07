# Roadmap

Tomoshibi ships a lean **v1.0** as soon as the core app is polished, then grows
through small versioned releases. Dates are targets, not promises — the point is a
sensible order of work.

## Versioning

- **v1.0 — core.** The everyday study app: Pomodoro timer, daily intention, focus
  stats, task list with course tags, settings, all saved locally. Released after a
  short polish pass and a confirmed build.
- **v1.1 and on — the standout features**, each as its own release: zen/full-screen
  focus mode, streak calendar, `.ics` timetable deadline view, system tray +
  notifications, ambient soundscapes, and packaged self-contained downloads.

## v1.0 — core

Foundations and the daily-use features. Mostly done.

- [x] Solution + project scaffold (Models / Services / ViewModels / Views / Styles)
- [x] Tokyo Night theme (palette, cards, buttons, mono font)
- [x] JSON storage service + app-state model
- [x] Pomodoro timer: 25/5 cycle, long break after every 4th round
- [x] Daily intention line that persists and resets each day
- [x] Focus stats (sessions + hours) that increment and reset daily
- [x] Settings for timer lengths
- [x] Task list: add / complete / delete, with course tags
- [x] Persist tasks to JSON; carry over between launches
- [ ] Polish pass: spacing, empty states, layout at the minimum window size
- [ ] Confirm a clean `dotnet restore` + `dotnet run`
- [ ] Tag v1.0

## After v1.0 — feature releases

Pulled from the original roadmap and shipped one at a time, so each is a clean,
demoable increment.

- **Zen / full-screen focus mode** — timer only, everything else fades.
- **Streak calendar** — days with at least one completed session.
- **Deadline view** — import an `.ics` timetable and list upcoming deadlines.
- **System tray + notifications** — native alerts on phase change.
- **Ambient soundscapes** — rain / café / waves / night.
- **Packaging** — self-contained publish for Windows, macOS and Linux, an app icon,
  and a tagged public release.

## Testing

The logic worth testing has no UI: the Pomodoro state machine (round counting,
when a long break is due) and the daily-reset rules. Those get plain unit tests as
the first thing after v1.0.

## Out of scope (for now)

Deliberately off the list to keep things focused: cloud sync, accounts, mobile,
multi-profile, and any always-on network features. Tomoshibi is a local-first,
single-user desktop app.
