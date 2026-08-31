# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built versus what is designed but not yet implemented. The forward roadmap lives
in [product-plan.md](product-plan.md).

---

## Implemented (phases C1–C5)

| Area | Notes |
|---|---|
| **Rename** | `Notifications` → `Channels`: projects, `channels` schema, `chnXXX_` prefix, routes; `not001_notification` → `chn006_notification`. |
| **Channels & addressing** | `Channel` (`email` + `telegram`); `NotificationAddress`; `chn001_user_channel` with verified/enabled gates. |
| **Telegram transport** | `Pottmayer.Tars.Communication.Telegram` + `TelegramChannelTransport`; email via `EmailChannelTransport` (MailKit). |
| **Per-channel rendering** | File template tree (`FileNotificationTemplateRenderer`), `TemplateCatalog` + `TemplateCatalogValidator` failing startup on a missing variant. |
| **Durable queue** | `chn006` with `Pending→Sending→Sent`, backoff, `Dead`; `NotificationDispatcherBackgroundService`. |
| **Fan-out & preferences** | `chn005`; `NotifyUserRequested`; enqueuer fan-out; dedup by `(correlation_id, channel)`; `group_id`. |
| **Identity subscribers** | Activation, password reset/change, MFA enable/disable mapped to templates. |
| **Linking** | `chn002` handshake; `POST/DELETE /{channel}/link`; `ConsumeTelegramLink` on `/start <token>`. |
| **Inbound** | `chn003`, `chn004`; `TelegramLongPollingService`; `TelegramInboundTriage`; idempotency by `(provider, provider_update_id)`; routing via `InboundInteractionReceived` / `InboundMessageReceived`. |
| **Interactions** | Registered buttons routed to owners; single-use; first consumer Agenda (`task_done`, `snooze_1h`). |
| **Media** | `IInboundMediaReader` + `TelegramInboundMediaReader` (`getFile`/download). |
| **Permanent-failure handling** | Disables the channel + `UserChannelDisabled`. |
| **C5 — raw retention purge** | `InboundUpdateRetentionBackgroundService`; `Channels:RawRetention:{Enabled,RetentionDays}` (default on / 7 days). |
| **C5 — delivery history** | `GET /channels/notifications` (filter + paging); `chn006.user_id`/`category` stamped at enqueue; history table in settings. |
| **C5 — test send** | `POST /channels/{channel}/test` (delivered in C2). |
| **C5 — quiet hours** | `chn007_user_notification_setting`; a global daily "do not disturb" window in the user's own IANA zone (resolved from Identity via `IUserPreferencesReader`); `suppress`/`deliver_anyway`; gated in `NotifyUserRequestedHandler` before fan-out; `GET`/`PUT /channels/notification-settings`; settings UI. Security notifications never reach this path. |
| **C5 — metrics** | `ChannelsMetrics` meter (`Pottmayer.Pandora.Modules.Channels`): `dispatched{channel,outcome}`, `dispatch.duration{channel}`, `queue.depth` gauge, `inbound.updates.discarded`. Subscribed by a `Pottmayer.Pandora.*` `AddMeter` wildcard in the shared observability wiring, exported over OTLP. |
| **Frontend** | Notifications settings section (channels, test, preferences, **quiet hours**, delivery history) in `client-web/src/modules/channels`. |

### Notable deviations from the original plan

- **Quiet hours are a global per-user setting (`chn007`), not columns on `chn005`.** The plan said
  they would "join `chn005`", but that table is per-category and its rows only exist once a user
  customises a category — a single "do not disturb" would have meant a row per category. `chn007` is
  one row per user, so the window is global; per-category muting stays on `chn005`.
- **`chn004` keeps a `uuid_generate_v7()` surrogate PK** with a unique index on
  `(provider, provider_update_id)`, rather than the composite PK the plan proposed.

## Not yet implemented (designed / planned)

| Area | Status | Where |
|---|---|---|
| **Webhook driver** | Deferred; long polling covers ingress. The Tars client already supports `SetWebhook`. | "Maybe later" |
| **Manual retry of a dead row** | Not planned while dead-letters are rare and inspectable. | "Maybe later" |
| **Finances notification categories** | Finances events (`StatementClosed`, `ImportCompleted`, …) not published yet. | Follow-up in Finances |

## Known open points

1. **Categories as a typed registry vs. a string.** Today `Category` is a string; a central registry
   would give startup validation at the cost of one more place to touch. Leaning: string until it hurts.
2. **One address per channel.** Two Telegram chats for one person is a deliberate future change (the
   unique constraint blocks it today).
