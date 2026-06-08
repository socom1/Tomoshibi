# Roadmap

Tomoshibi ships a lean **v1.0** as soon as the core app is polished, then grows
through small versioned releases. Dates are targets, not promises — the point is a
sensible order of work.

## Versioning

- **v1.0 — core.** The everyday study app: Pomodoro timer, daily intention, focus
  stats, task list with course tags, settings, all saved locally. Released after a
  short polish pass and a confirmed build.
- **v1.1 — zen focus mode.** A distraction-free full-screen layout that hides
  everything but the timer.
- **v1.2 — nav sidebar + timetable.** Introduce a togglable left navigation
  sidebar, refactor the existing app into a "today" destination behind it,
  and add a "timetable" destination with a recurring class schedule and
  upcoming deadlines, plus `.ics` import.
- **v1.3 and on — the standout features**, each as its own release: streak
  calendar, system tray + notifications, ambient soundscapes, and packaged
  self-contained downloads.

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
- [x] Polish pass: bilingual empty state, accurate settings caption, midnight reset for the always-on case
- [x] Confirm a clean `dotnet restore` + `dotnet run`
- [x] Tag v1.0

## v1.1 — zen focus mode

- [x] Full-screen layout that hides everything but the timer
- [x] Phase-coloured oversized clock, round indicator, basic controls
- [x] Toggle from the header (`⛶`) and Esc to exit

## v1.2 — nav sidebar + timetable

Introduce a left-side **navigation sidebar** that routes the main content
area between destinations, and ship the first non-"today" destination — a
class-schedule timetable with deadlines.

- [x] Nav sidebar mechanic — togglable from the header (`☰`), open/closed
      state persists
- [x] Refactor the existing app behind a "今日 · today" destination
- [x] "時間割 · timetable" destination
- [x] Models + storage for `ClassSlot` and `Deadline`
- [x] Manual add / remove for both, with course-tag autocomplete pulled from
      tasks, slots and deadlines
- [x] Responsive week-grid view (7-day columns, slots placed by hour) +
      deadlines list above
- [ ] `.ics` import — file picker, `VEVENT`/`RRULE` parsing, recurring → slots, one-shots → deadlines

## After v1.2 — feature releases

- **Streak calendar** — days with at least one completed session.
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
