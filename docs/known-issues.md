# Known Issues

Cross-cutting problems that are understood but not yet fixed. Each entry records the symptom, the
root cause, the affected places, and the options on the table — so the fix can be planned rather than
rediscovered.

---

## KI-001 — Time zones: user input, storage, display, and "today"

**Status:** open. A prototype fix was explored and reverted (see [History](#history)); the analysis
below is the durable record.

### The intended model (this is correct — the bugs are deviations from it)

- **Storage is always UTC.** Instant columns are `timestamptz` (they store a UTC instant); pure
  calendar dates in Finances are `DateOnly` (a wall date, deliberately zone-less — "the transaction
  occurred on 2026-09-11" is a date, not a moment).
- **Input that carries a time is interpreted in the user's account time zone, then reduced to a UTC
  instant.** For the assistant this happens by feeding the model the current time + the user's IANA
  zone so it emits an absolute ISO-8601 timestamp *with offset*, which parses to a UTC instant.
- **On read, the client converts the UTC instant back to the user's zone.**

### Root cause shared by most of the symptoms

The user's time zone lives in Identity preferences (`identity.idt003_user_preferences.time_zone`), read
through `IUserPreferencesReader`. When a user has **no preferences row**, the reader returns `null` and
every consumer falls back to **UTC**. Two things make this easy to hit and hard to notice:

- The web Settings screen *shows* a time zone detected from the browser
  (`Intl…resolvedOptions().timeZone`), but that value is only persisted if the user explicitly changes a
  setting — so the account can look configured while the database has no row at all
  (`PreferencesProvider`).
- When the zone falls back to UTC, some outputs *also* format in UTC and therefore look
  self-consistent, hiding the error until a second surface (a grid that converts to the browser zone)
  disagrees.

### Symptom classes and affected places

**A. Input interpretation — the assistant stores the wrong instant.** With no zone, the assistant's
system prompt says the reference clock is UTC, so "hoje às 22h" becomes `22:00Z` instead of
`22:00-03:00`. The reminder fires 3h early and displays 3h off.
- `Modules/Assistant/.../Interpret/AssistantSystemPrompt.cs` (injects the reference clock + zone).
- `Modules/Assistant/.../Commands/Interpret/InterpretCommandHandler.cs` — `ResolveTimeZone` falls back
  to `TimeZoneInfo.Utc`.
- `Modules/Agenda/.../Preferences/TimeZoneResolver.cs` — `?? "UTC"`, which is what a reminder created
  with `TimeZone: null` (the `create_reminder` tool) ends up stored with.

**B. Display — the client formats in the browser zone, not the account zone.** The Agenda formatters
use `dayjs(iso).format(...)`, i.e. the device's zone. It matches the account only when the user is on a
device in that zone; on another device the grid and the actual fire time diverge.
- `client-web/src/modules/agenda/lib/datetime.ts` — `formatDateTime` / `formatDate` / `formatTime`.
- Callers: `pages/reminders/RemindersListPage.tsx`, `pages/tasks/TasksListPage.tsx`,
  `pages/today/TodayPage.tsx`, `pages/calendar/WeekDayGrid.tsx`, `pages/calendar/EventDetailModal.tsx`.

**C. Recurrence expansion — correct *iff* the stored zone is correct.** Recurring reminders/events
expand their RRULE anchored in the aggregate's own `TimeZone` (`Reminder.ResolveZone()`,
`Event.ResolveZone()`), so day-of-week and DST are right — but only if that stored `TimeZone` is the
user's, not the UTC fallback from symptom A. No separate fix needed once A is fixed.

**D. "What day is it" — computed in the UTC day, not the user's day.** Several read paths take
`now.UtcDateTime.Date` as "today". For a UTC−3 user, anything computed as "today" between ~21:00 and
midnight local has already rolled to the next UTC date, so items land on the wrong day / bucket. This
is a **day-boundary** error, independent of symptoms A–C.
- Agenda: `Queries/GetToday/GetTodayQueryHandler.cs` (`dayStart = now.UtcDateTime.Date`),
  `Queries/GetTasks/GetTasksQueryHandler.cs` (Overdue/Today/Week buckets).
- Finances: `DateOnly.FromDateTime(now.UtcDateTime)` used as "today" in
  `Commands/CreateTransaction/…`, `Commands/CloseStatement/…`, `Commands/PayStatement/…`,
  `Commands/GenerateRecurringTransactionOccurrence/…`, and similar. (Stored `DateOnly` values are fine;
  only the "default to today" / "is it past due" comparisons are affected.)

### What is NOT affected

The dispatch sweeps fire on an instant compared to `GetUtcNow()` — both UTC — so a reminder/alert rings
at the right moment regardless of any display zone:
- `Modules/Agenda/.../Sweep/DispatchDueRemindersCommandHandler.cs`,
  `DispatchDueTaskAlertsCommandHandler.cs`, `DispatchDueEventAlertsCommandHandler.cs`.
- Retention/purge jobs (Channels inbound retention, Identity refresh-token purge) are "older than X" —
  pure elapsed time.

### Possible solutions

1. **Guarantee the preference exists (preferred — kills symptoms A–C at the source).**
   - Seed the account with the browser-detected zone on first authenticated load, when the backend has
     no preferences row (client-side, in `PreferencesProvider`).
   - And/or a **configurable default zone** on the backend (`Pandora:DefaultTimeZone`) used as the
     fallback instead of UTC, so a channel with no browser (the assistant over Telegram) still resolves
     the user's clock. This belongs in `TimeZoneResolver` (Agenda) and the assistant's `ResolveTimeZone`.
   - Best is both: the default covers "never opened the web", the seed captures the real per-user zone.
2. **Fix "today" in the user's zone (symptom D).** In the affected query/command handlers, convert
   `now` to the user's IANA zone *before* taking `.Date` / bucketing, instead of `now.UtcDateTime.Date`.
   Agenda handlers already have `IUserPreferencesReader`; Finances would need it (or the configurable
   default) threaded in.
3. **Mandatory onboarding configuration flow (see [KI-002](#ki-002)).** If the app refuses to run until
   the time zone is set, symptom D's dependency on a present zone is always satisfied and symptoms A–C
   never arise.

---

## KI-002 — No mandatory-configuration gate after register/login

**Status:** idea, not built.

### Problem

Some settings are effectively required for the system to behave correctly — the user's **time zone** is
the first (see [KI-001](#ki-001)) — but nothing forces the user to set them. The app starts usable with
implicit, sometimes wrong, defaults, and the missing configuration only surfaces later as a subtle bug.

### Proposed shape

A **required-configuration gate** on the web home, evaluated right after registration or login:

- Maintain a registry of *required settings*, each with a predicate ("is it satisfied for this user?")
  and the screen that satisfies it.
- After login, if any required setting is unmet, route the user into a blocking setup/onboarding flow
  and **prevent using the rest of the system** until every required setting is satisfied.
- The registry is extensible: adding a new required setting later automatically re-gates existing users
  the next time they log in, until they complete it.

### Considerations

- This gates the **web** entry point. A user whose first contact is a non-web channel (the assistant
  over Telegram) is not gated, so a backend fallback (the configurable default zone in
  [KI-001](#ki-001)) is still worth keeping — or require completing web onboarding before a channel can
  be linked.
- Keep the gate to genuinely required settings; optional preferences must not trap the user.

---

## History

- The time-zone analysis in KI-001 came out of a debugging session where an assistant-created reminder
  ("22h") displayed and was scheduled 3h early. During that session a prototype was built and then
  reverted: a configurable `Pandora:DefaultTimeZone` fallback, seeding the preference from the browser
  on first login, and account-zone formatting on the Agenda screens. The reverted code is the reference
  implementation for solution options 1 and 2 above if/when this is picked up for real.
