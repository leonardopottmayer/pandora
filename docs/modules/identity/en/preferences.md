# Preferences

[← Back to index](../README.md) · Related: [Data Model](data-model.md)

---

Per-user preferences (`idt003`, one row per user) hold UI choices **and** the scheduling defaults that
other modules read.

| Field | Values | Consumed by |
|---|---|---|
| `theme` | `light` \| `dark` \| `system` | The web client. |
| `language` | `pt-BR` \| `en` | The web client; the locale carried into notifications. |
| `time_zone` | IANA (default `America/Sao_Paulo`) | The **user-level default** time zone. |
| `week_starts_on` | `sunday`…`saturday` | Calendar week rendering. |

## API

- `GET /identity/preferences` — read (`GetPreferences`). Fails with a not-found error if the user has
  never saved preferences yet — the row is created lazily, not at sign-up.
- `PUT /identity/preferences` — upsert (`UpsertPreferences`): creates the `idt003` row on the first
  call, updates it afterwards. Requires all five fields in the request body (no partial updates).
  Validates the theme and language against the supported sets, the week start against `DayOfWeek`
  names, and the time zone with `TimeZoneInfo.TryFindSystemTimeZoneById`.

Note: the column defaults in the `idt003` migration (`en`, `America/Sao_Paulo`, `sunday`, `-15`) only
apply if a row were inserted without those columns — in practice the application always supplies every
field on the first `PUT`, so a user who never opens the preferences screen has **no** `idt003` row at
all, not one holding these defaults.

## Cross-module note

Identity **does** carry the user's IANA time zone and week start, which the Agenda plan called a
"phase 0" prerequisite. A preference only one module uses lives in that module: the default alert
offset is in the Agenda's `agd013_preferences`. Two consuming pieces remain in other modules:

- **Agenda** stores a `time_zone` on each item because recurrence must expand in the *item's own* zone
  (which can differ from the user default); wiring the Identity preference in as the default for new
  items is a small follow-up.
- **Channels** quiet hours are not built yet; they need this time zone, which is now available — so
  they are unblocked, not blocked. See [Channels product-plan](../../channels/en/product-plan.md).
