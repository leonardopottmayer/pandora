# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built versus what is designed but not yet implemented. The forward roadmap lives
in [product-plan.md](product-plan.md).

---

## Implemented (phases C1–C4, plus most of C5)

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
| **Frontend** | Notifications settings section (channels, test, preferences, delivery history) in `client-web/src/modules/channels`. |

### Notable deviations from the original plan

- **`chn005` has no quiet-hours columns.** Deferred — quiet hours need the user's IANA time zone,
  which Identity does not carry yet. Only the ordered `channels[]` array is stored.
- **`chn004` keeps a `uuid_generate_v7()` surrogate PK** with a unique index on
  `(provider, provider_update_id)`, rather than the composite PK the plan proposed.
- The old `Notifications` tests project still exists under `tests/` as legacy.

## Not yet implemented (designed / planned)

| Area | Status | Where |
|---|---|---|
| **Quiet hours** | Not built. The IANA time zone they need is now in Identity preferences, so unblocked. | C5 |
| **Metrics** (queue depth, dispatch latency, failure rate per channel, discarded updates) | Waits on OpenTelemetry wiring in the Host. | C5 |
| **Webhook driver** | Deferred; long polling covers ingress. The Tars client already supports `SetWebhook`. | "Maybe later" |
| **Manual retry of a dead row** | Not planned while dead-letters are rare and inspectable. | "Maybe later" |
| **Finances notification categories** | Finances events (`StatementClosed`, `ImportCompleted`, …) not published yet. | Follow-up in Finances |

## Known open points

1. **Categories as a typed registry vs. a string.** Today `Category` is a string; a central registry
   would give startup validation at the cost of one more place to touch. Leaning: string until it hurts.
2. **One address per channel.** Two Telegram chats for one person is a deliberate future change (the
   unique constraint blocks it today).
