# Known Issues

Cross-cutting problems that are understood but not yet fixed. Each entry records the symptom, the
root cause, the affected places and the options on the table, so the fix can be planned rather than
rediscovered. A fixed issue leaves only a short note on how the system works now.

---

## KI-001 — Time zones *(fixed 2026-09-20)*

An assistant-created reminder for "22h" used to fire 3 hours early: with no preferences row, every
module fell back to UTC. How it works now:

- **Storage is UTC.** Instants are `timestamptz`; calendar dates in Finances are `DateOnly`.
- **One resolver for the user's zone.** Identity's `IEffectiveTimeZoneResolver` resolves
  `requested → stored preference → Pandora:DefaultTimeZone → UTC`. The default is
  `America/Sao_Paulo`, so a user who only ever used Telegram still gets a real clock. Agenda, the
  Assistant and Channels' quiet hours all go through it.
- **The preference is captured.** `PreferencesProvider` saves the browser's zone on the first
  authenticated load when the account has no preferences row.
- **"Today" is the user's day.** Agenda (`GetToday`, `GetTasks`), the per-user Finances commands and
  the Finances daily jobs take "today" in each user's zone (`ResolveTodayAsync` / `DayBoundary` /
  `UserDays`). The jobs run hourly, so a day turns near each user's own midnight.
- **All-day values are dates** *(2026-11-13)*. A date-only task due and an all-day event are stored at
  the midnight that starts their date in the item's zone and read back at the same date's midnight in
  the viewer's zone (`AllDayDates`), so they stay on their date for a viewer in any zone.
- **Forms and display use the account zone.** Agenda's pickers show and send the account zone's wall
  clock (`toPicker` / `fromPicker`, ISO with that zone's offset), Finances' date fields default to the
  account's today (`todayIn`), and every screen formats timestamps in the account zone (see
  [KI-003](#ki-003--timestamps-shown-in-the-devices-zone-fixed-2026-11-13)).

---

## KI-002 — No mandatory-configuration gate after register/login *(closed 2026-11-16, not needed yet)*

The idea: a blocking setup flow after the first web login, until every *required* setting is
satisfied. Not built, because no setting is required today: the time zone, the only candidate, falls
back to `Pandora:DefaultTimeZone` (KI-001). Build it when the first setting appears that has no safe
fallback. It would gate the web only, so a user who starts on Telegram still needs a backend fallback,
or linking a channel must require finishing the web setup first.

---

## KI-003 — Timestamps shown in the device's zone *(fixed 2026-11-13)*

Some screens formatted instants with `toLocaleString()` or a device-zone `dayjs()`, so on a device set
to another zone they differed from the rest of Pandora. How it works now: every timestamp formatter takes
the account zone from `usePreferences()` (`{ timeZone }` on `toLocale*String`/`Intl.DateTimeFormat`, or
`.tz(timeZone)` on dayjs), and "today"/"yesterday" labels compare days in that zone (`todayIn`). Notes
chip dates are wall-clock strings the user typed, not instants, so they are shown as written.

---

## KI-004 — Two harmless warnings in the container's startup log *(closed 2026-11-22, nothing to fix)*

- `Error: libgssapi_krb5.so.2: cannot open shared object file`: .NET looks for the Kerberos library
  once; Pandora authenticates to Postgres with a password and never uses it.
- `Storing keys in a directory '/root/.aspnet/DataProtection-Keys' that may not be persisted`: ASP.NET
  Core registers its own data protection by default, but nothing in Pandora uses it (auth is JWT, no
  cookies, sessions or antiforgery). The secrets Pandora encrypts use `DATA_PROTECTION_KEY`
  (Tars Security.DataProtection), which comes from the environment and survives a container rebuild.
  If a cookie or antiforgery feature is ever added, persist these keys (a volume) first.
