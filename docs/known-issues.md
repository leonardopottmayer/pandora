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
- **"Today" is the user's day.** Agenda (`GetToday`, `GetTasks`) and the per-user Finances commands
  take "today" in the user's zone (`ResolveTodayAsync` / `DayBoundary`); the global Finances jobs anchor
  on the default zone (`ResolveDefault()`).
- **Display uses the account zone** on the Agenda screens. Finances is the exception: see
  [KI-003](#ki-003--finances-shows-instants-in-the-devices-zone).

---

## KI-002 — No mandatory-configuration gate after register/login

**Status:** idea, not built.

### Problem

Some settings are effectively required for the system to behave correctly, and nothing forces the user
to set them. The time zone was the first; KI-001's default zone now covers it, but the next required
setting will have no such fallback.

### Proposed shape

A **required-configuration gate** on the web home, evaluated right after registration or login:

- Maintain a registry of *required settings*, each with a predicate ("is it satisfied for this user?")
  and the screen that satisfies it.
- After login, if any required setting is unmet, route the user into a blocking setup flow and
  **prevent using the rest of the system** until every required setting is satisfied.
- Adding a new required setting later re-gates existing users the next time they log in.

### Considerations

- This gates the **web** entry point. A user whose first contact is Telegram is not gated, so every
  required setting still needs a backend fallback, or linking a channel must require finishing the web
  setup first.
- Keep the gate to genuinely required settings; optional preferences must not trap the user.

---

## KI-003 — Finances shows instants in the device's zone

**Status:** open, low stakes.

Finances' `formatDateTime` (`client-web/src/modules/finances/lib/format.ts`) formats with
`Intl.DateTimeFormat` and no `timeZone`, so instants show in the device's zone, not the account's.
Calendar dates (`DateOnly`) are not affected. It shows on the audit screen, the audit drawer and the
import detail. On a device set to another zone, those times differ from what the rest of Pandora shows.

**Fix:** pass the account zone from `usePreferences()` into the formatter, as Agenda's `datetime.ts`
does.
