# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Outbound & Templates](outbound-and-templates.md), [Inbound & Linking](inbound-and-linking.md)

---

## 1. Project layout

Layered projects under `backend/src/Modules/Channels/`:

```
Pottmayer.Pandora.Modules.Channels.
  Abstractions      → ChannelsModule registration, ChannelsOptions, IInboundMediaReader (the one
                      port other modules — Assistant — call to read inbound audio)
  Application       → Commands, Queries, Subscribers, the enqueuer, linking, DTOs, DI
  Contracts         → integration events: NotifyUserRequested, SendNotificationRequested,
                      InboundMessageReceived, InboundInteractionReceived, UserChannelDisabled
  Domain            → Aggregates, ValueObjects, Rendering, Ports (repositories + services), Errors
  Infrastructure    → Transports (Email/Telegram), Ingress (long polling, triage, media),
                      Jobs (dispatcher, retention), Templates (file renderer + catalog)
  Persistence       → EntityConfigs, Repositories, DbContext, DI
  Presentation      → ChannelsController, DI
```

## 2. The internal seam (Delivery / Ingress / Addressing)

One module does not mean one undifferentiated pile. The boundary that would have split the module runs
**inside** it, in namespaces:

```
Application
├── Delivery   (Enqueue, DispatchPending, SetNotificationPreference, GetDeliveryHistory) — preferences · fan-out · rendering · dispatcher · retry
├── Ingress    (Subscribers, ConsumeTelegramLink, PurgeInboundUpdates) — drivers · triage · linking · interaction resolution
└── Addressing (CreateChannelLink, UnlinkChannel, GetUserChannels, chn001) — read by both

Infrastructure
├── Transports (IChannelTransport: EmailChannelTransport · TelegramChannelTransport) — internal
├── Ingress    (TelegramLongPollingService · TelegramInboundTriage · TelegramInboundMediaReader)
└── Templates  (FileNotificationTemplateRenderer · TemplateCatalog[+Validator])
```

The rule that keeps it honest: nothing in `Ingress` writes to the queue directly (it publishes an
event, or calls `Delivery` through the same surface an external module would use), and nothing in
`Delivery` knows the Bot API.

## 3. Domain building blocks

### Aggregates (`Domain/Aggregates`)

| Aggregate root | Responsibility / key invariants |
|---|---|
| **Notification** | The durable queue row. `Pending → Sending → Sent`; `Failed`/`Dead` on exhaustion; exponential backoff via `next_attempt_at`; content is rendered once and immutable (C6). |
| **UserChannel** | An address on a channel. Usable only when `is_verified && is_enabled`; a permanent send failure disables it (`disabled_reason`) and publishes `UserChannelDisabled`. |
| **ChannelLinkToken** | The Telegram handshake. Single-use, TTL-bounded; consumed by `/start <token>`. |
| **Interaction** | A registered inline button + its route home. Single-use; a second tap is "expired". FK to the notification that declared it. |
| **InboundUpdate** | Idempotency record + trail for every received update. Unique on `(provider, provider_update_id)`; carries the triage `classification`. |
| **NotificationPreference** | Per-category ordered channel list. Empty ⇒ muted. `identity.*` never consults it. |

### Value objects (`Domain/ValueObjects`)

`Channel` (`email` \| `telegram`, with `All`), `NotificationAddress` (channel-aware: email VO or numeric
chat id), `NotificationContent` (`Subject`/`Body`/`IsHtml` for email), `NotificationStatus`,
`InboundClassification`, `TemplateKey`. Structured Telegram output is `Rendering/TelegramRenderedPayload`
(`text`, `parseMode`, `disableNotification`, `buttons: [{ interactionId, label }]`).

### Ports (`Domain/Ports`)

- **Services:** `IChannelTransport` (one impl per channel, called only by the dispatcher),
  `INotificationTemplateRenderer` (file-backed).
- **Repositories:** one per aggregate (`INotificationRepository`, `IUserChannelRepository`,
  `IChannelLinkTokenRepository`, `IInteractionRepository`, `IInboundUpdateRepository`,
  `INotificationPreferenceRepository`).

## 4. Background jobs

Module-owned hosted services (the same async-without-a-broker pattern as the rest of Pandora — see the
[messaging doc](../../../architecture/en/messaging.md)):

- **`NotificationDispatcherBackgroundService`** — picks up due rows (`status`, `next_attempt_at`),
  sends through the channel's transport, advances status/backoff.
- **`TelegramLongPollingService`** — `getUpdates(timeout=30)` ingress driver; writes each update to
  `chn004` (confirming the offset) then triages it.
- **`InboundUpdateRetentionBackgroundService`** — daily; nulls the `raw` payload of `chn004` rows older
  than `Channels:RawRetention:RetentionDays` (default 7), keeping the row.

## 5. Key design decisions

| # | Decision | Rationale |
|---|---|---|
| **C1** | Send-now only; no scheduling in the queue. | Keeps the module stateless w.r.t. business time; scheduling lives in the caller. |
| **C2** | Callers address a **user + intent**; the module resolves channels. | Delivery policy is one concern in one place. |
| **C3** | Fan-out = N rows sharing a `group_id`, deduped by `(correlation_id, channel)`. | Independent retry/failure per channel with honest status. |
| **C4** | `IChannelTransport` is an internal port, one impl per channel. | Add a channel without a `switch`; not promoted to a module boundary (single caller). |
| **C5** | Inbound routed by a structural id lookup; the module never interprets meaning. | Prevents Channels needing to know what a task is. |
| **C6** | Render at enqueue; store what went out. | Retry resends identical bytes; template edits don't rewrite history. |

## 6. Tars building block: `Communication.Telegram`

Mirrors the `Communication.Email` / `.MailKit` split. `Pottmayer.Tars.Communication.Telegram` is a
thin transport over the Bot API (`sendMessage`, `answerCallbackQuery`, `getUpdates`, `getFile`/download,
`setWebhook`, MarkdownV2 escaping, secret-token validation). Templates, retries, addressing, triage and
persistence are Pandora's business and live here. Its documentation is in the Tars repo
(`docs/communication/telegram.md`).

## 7. Cross-cutting rules

- **Multi-tenant by user.** Every user-owned table has `user_id NOT NULL`; endpoints are scoped to the
  token's user.
- **A chat id is never accepted from the client** — only the linking handshake authorizes one.
- **`TimeProvider` everywhere** — TTLs, backoff and retention are computed against injected time.
