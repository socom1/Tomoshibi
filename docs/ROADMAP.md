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
- **v1.3 — the daily companion.** Tasks as code driving the timer, chime +
  notifications, stats history with streaks, the todo backlog, and packaging.
- **v1.4 — stats + tray.** A streak calendar over the saved history, and
  the timer in the menu bar.
- **v1.5 — subjects.** Weighted grades per subject with targets, drop
  rules, a what-if simulator, term grouping with a year-weighted degree
  projection, exam surfacing, transcript export — and a configurable scale
  (US 4.0 / UK honours / ECTS / percentage).
- **v1.6 — dashboard.** A morning landing page that gathers the glance,
  weak-spot analysis, a study-video board, a week agenda, per-course focus,
  a light theme, and a settings page that gathers everything tweakable.
- **v1.7 — embers & palette.** An embers currency earned by focusing, a theme
  shop to spend it in, a `Cmd/Ctrl+K` command palette and a launch splash.
- **v1.8 — recall & reflection.** Spaced-repetition flashcards, study goals,
  per-subject notes, an end-of-day reflection journal, exports, deadline
  reminders and a first-run onboarding.
- **Later** — soundscapes, a public release.

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
- [x] `.ics` import — file picker, weekly `RRULE`s → slots, one-shots → deadlines
- [x] Edit slots and deadlines in place

## v1.3 — the daily companion

Everything that turned the timer into something that talks back, plus the
backlog. Shipped, untagged so far.

- [x] Tasks as code: the template grammar, the editor, the simple form modal
- [x] Active task drives the pomodoro phase lengths
- [x] Chime + native notification on phase change (notification: macOS/Linux;
      Windows pending an app identity)
- [x] Auto-continue, paused dimming, round dots, live window title, Space
- [x] Daily stats history + day streak with a 14-day dot strip
- [x] "やること · todo" backlog destination with send-to-today
- [x] App icon, title, macOS .app packaging; Windows/Linux pack scripts
      (written, unverified on those OSes)

## v1.4 — stats + tray

- [x] 記録 · stats destination: streak calendar (month heat grid), best
      streak, all-time totals
- [x] Tray icon: start/pause/skip from the menu bar, live tooltip,
      close-to-tray keeps the timer running

## v1.5 — subjects

- [x] 科目 · subjects destination: assessments per subject, weighted toward a
      running grade and a GPA
- [x] Configurable grade scale — US GPA, letter bands, or custom boundaries
- [x] Targets, drop rules, a what-if simulator, term grouping and a
      year-weighted degree projection
- [x] Exam surfacing, a per-subject page with outlook + linked context, and a
      transcript export

## v1.6 — dashboard

- [x] ダッシュボード · dashboard: today's glance, week momentum, the next-7-days
      agenda, what's due, the standing and weak-spot analysis
- [x] Study-video board, per-course focus tracking, a light theme and keyboard
      navigation
- [x] A settings page gathering everything tweakable; debounced saves

## v1.7 — embers & palette

- [x] Embers currency earned by focusing, and a theme shop to spend it in
- [x] `Cmd/Ctrl+K` command palette over pages, actions, subjects — and now
      todo tickets, decks and journal reflections
- [x] Launch splash and a polished, animated nav rail

## v1.8 — recall & reflection

- [x] 復習 · review destination: spaced-repetition flashcard decks with a
      scheduling queue
- [x] Study goals, per-subject notes, and an end-of-day reflection that banks
      into a journal look-back
- [x] Deadline / exam reminders, first-run onboarding, and data exports
- [x] Hardened notification escaping and a capped `.ics` import

## Later

- **Ambient soundscapes** — rain / café / waves / night (needs real audio
  assets; synthesis won't cut it). The local-folder music player is the
  nearest thing today.
- **Smarter palette** — fuzzy matching and content beyond titles (description,
  course) so a typo or a partial still finds the row.
- **Group-project awareness** — an optional owner on todo subtasks, so a shared
  project's split shows in the backlog without any sync or accounts.
- **Backup / restore** — export the whole state to a chosen file and read it
  back, on top of the automatic `.bak`.
- **Bundle a coding font** — pixel-identical look across OSes.
- **A tagged public release** — signed/notarized builds.

## Testing

The pure logic is covered: the grade engine, the task-template parser
(including the done-toggle source surgery), storage round-trip + crash
recovery, the daily-reset/banking rules, the load-time migrations and the
`.ics` importer. Still open: the Pomodoro state machine, which needs
extracting from its timer before tests can drive it.

## Out of scope (for now)

Deliberately off the list to keep things focused: cloud sync, accounts, mobile,
multi-profile, and any always-on network features. Tomoshibi is a local-first,
single-user desktop app.
