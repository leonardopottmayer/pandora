# Channels Module — Product Plan

> **Status:** Plan. Describes the evolution — and the renaming — of an **existing** module
> (`Notifications`).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Agenda](../../agenda/en/product-plan.md) ·
> [Integrations](../../integrations/en/product-plan.md) ·
> [Assistant](../../assistant/en/product-plan.md) ·
> [Messaging](../../../architecture/en/messaging.md)

---

## 1. Where the module is today

`Pottmayer.Pandora.Modules.Notifications` already delivers a durable outbound queue:

- `Notification` aggregate (`not001_notification`) with `Pending → Sending → Sent`, exponential
  backoff, `MaxAttempts` and a `Dead` dead-letter state.
- `NotificationEnqueuer` — renders a template and persists a row, deduplicated by `correlation_id`.
- `NotificationDispatcherBackgroundService` — picks up due rows and sends them.
- Subscribers for Identity's events (activation, password reset/change, MFA enable/disable).
- `SendNotificationRequested` — a broker-ready POCO, the escape hatch for ad-hoc sends.

And it has four hard limits:

1. **`Channel` has exactly one value, `email`.** The smart enum was written anticipating more
   (`// Sms / Telegram / WhatsApp can be added later`), but nothing downstream is channel-aware.
2. **`Recipient` is an `Email` value object.** A Telegram chat id does not fit that type.
3. **`NotificationContent` is `{ Subject, Body, IsHtml }`** — an email-shaped record. Telegram has no
   subject, has its own markup dialect, and has inline keyboards that email cannot express.
4. **The module has no notion of *who* the notification is for.** It knows an address, not a user —
   so there is nowhere to hang "Leonardo prefers Telegram for reminders and email for statements".

---

## 2. Where it needs to go

Everything [Agenda](../../agenda/en/product-plan.md) and
[Assistant](../../assistant/en/product-plan.md) do depends on this module learning five things:

1. Send over **Telegram** as a first-class channel, not as a special case bolted onto email.
2. Address a **user**, and resolve that user's channels on its own.
3. Render **per channel** — an email body and a Telegram message are different artifacts from one
   template key.
4. Receive **inbound** updates: account linking, inline-button callbacks, text messages and voice
   notes.
5. **Route** that inbound traffic to the owning module, instead of broadcasting it to everyone.

### 2.1 Why the name changes

The module is called `Notifications`, and from item 4 onward half of what it does is not a
notification. It hosts a webhook, handles `/start`, stores chat ids and receives audio. The name was
already tight when "notify" meant "send an email"; it stops describing the contents when it means
"talk to the user".

**`Channels`** names what the module becomes: the owner of the conversation with the human, in both
directions, across every channel. The rename happens in **phase C1**, while the module is still
small — it is the cheapest thing on the roadmap now and the most expensive one after Telegram lands.

### 2.2 Why one module, not two

Splitting "transport" from "delivery policy" was evaluated and **rejected**. Drawn there, the
boundary cuts in the wrong place three times:

- **Fan-out is a join.** Deciding whether a reminder becomes email, Telegram or both requires
  crossing the user's preference for that category (`chn005`) with the channels they have enabled and
  verified (`chn001`). Two tables that are never read apart. In separate modules that becomes a
  cross-module call on the hot path of *every* notification, and the channel decision no longer fits
  in one transaction.
- **The port would have a single caller.** `IChannelTransport` is implemented twice (email, Telegram)
  and called from exactly one place: the dispatcher. That is a useful internal interface, and that is
  what it should stay. Promoting it to a module boundary costs seven projects and a schema for
  nothing — and contradicts rule 2 of `CLAUDE.md` ("no abstractions for single-use code").
- **Interaction tokens are born from notifications.** A *Done* button only exists because some
  notification declared it. In one module, `chn003_interaction` has an FK to the queue row that
  produced it and "which message did this click come from" is a column. Split, it is a correlation by
  opaque id across two schemas.

What genuinely does **not** belong to the conversation with the user is the *processing* of what they
sent — transcribing, interpreting, executing a command. That lives in
[Assistant](../../assistant/en/product-plan.md) and stays there.

### 2.3 The seam moves inside, and it is real

One module does not mean one undifferentiated pile. The internal split carries the same boundary, in
namespaces rather than in `.csproj` files:

```
Pottmayer.Pandora.Modules.Channels.Application
├── Delivery/        preferences · fan-out · rendering · dispatcher · retry
├── Ingress/         drivers · triage · linking · interaction resolution
└── Addressing/      chn001_user_channel — read by both

Pottmayer.Pandora.Modules.Channels.Infrastructure
├── Transports/      IChannelTransport: Email (MailKit) · Telegram (Tars) — internal
├── Ingress/         long-polling driver · webhook controller — same handler
└── Templates/       files per key/channel/locale, validated at startup
```

The rule that keeps this honest: nothing in `Ingress` writes to the queue directly (it publishes an
event, or calls `Delivery` through the same surface an external module would use), and nothing in
`Delivery` knows the Bot API.

---

## 3. Principles

1. **Channels sends now; it does not schedule.** No `ScheduledFor`, no cancellation API. Whoever
   wants delivery at 14:00 calls at 14:00. This keeps the queue simple and the module stateless with
   respect to business time. *(C1)*
2. **The caller names a user and an intent, not an address.** Address resolution, channel selection,
   quiet hours and opt-outs are delivery policy, and delivery policy lives here. *(C2)*
3. **One request becomes N notifications.** "Email and Telegram" are two rows sharing a group id —
   independent retry, independent failure, honest status. *(C3)*
4. **Channels are ports.** Adding WhatsApp is an `IChannelTransport` implementation plus a template
   variant. No `switch` in the dispatcher. *(C4)*
5. **Inbound is classified structurally, never semantically.** The module resolves an id in a table
   and reads the column the owning module wrote; it never interprets what an action means. *(C5)*
6. **Rendering happens at enqueue, not at send.** What went out is stored, retry resends byte-for-byte
   the same content, and changing a template tomorrow does not rewrite history. *(C6)*

---

## 4. Naming and coordinates

| Item | Value |
|---|---|
| Backend projects | `Pottmayer.Pandora.Modules.Channels.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| PostgreSQL schema | `channels` (renamed from `notifications`) |
| Table prefix | `chnXXX_`, PK `uuid_generate_v7()` |
| API base | `/api/v{version}/channels` |
| Frontend | *Notifications* section in settings (see §9) |
| Migrations | `migrations/migrations/channels/` |
| Tars building block | `Pottmayer.Tars.Communication.Telegram.*` (see §10) |

---

## 5. Model changes

### 5.1 `Channel`

```csharp
public static readonly Channel Email    = new("email");
public static readonly Channel Telegram = new("telegram");
```

Plus `FromValue` and an `All` collection for iteration. The smart-enum shape is already right — it is
a two-line change plus the parse arm.

### 5.2 `Recipient` → `NotificationAddress`

The shared `Email` VO is replaced in the aggregate by a channel-aware value object:

```csharp
public sealed record NotificationAddress(Channel Channel, string Value)
```

Validation delegates per channel: an email address is validated by the existing `Email` VO; a
Telegram address is a numeric chat id. The column stays a single `text` — only the invariant changes.

### 5.3 Content becomes per-channel

`NotificationContent { Subject, Body, IsHtml }` does not cover Telegram. The `Subject`/`Body`/`IsHtml`
columns are **kept** for email and joined by a new `rendered_payload jsonb` for structured channels,
rather than a destructive rewrite:

| Channel | Rendered payload |
|---|---|
| `email` | `{ subject, body, isHtml }` — exactly today's content, unchanged on the way out. |
| `telegram` | `{ text, parseMode, disableNotification, buttons: [{ interactionId, label }] }` |

Note that the rendered button carries the **interaction id**, not the action: the action → id mapping
happens at enqueue, against `chn003` (see §7.3).

### 5.4 Schema catalogue

**Addressing**

**`chn001_user_channel`** — where a user can be reached.

| Column | Notes |
|---|---|
| `user_id`, `channel` | Unique together. One address per channel per user. |
| `address` | Email address, or Telegram chat id. |
| `is_verified`, `verified_at` | Email inherits Identity's activation; Telegram is verified by the linking handshake. |
| `is_enabled`, `disabled_reason` | The user's off switch — and the automatic disable after a permanent failure. |
| `metadata` | jsonb — Telegram username/first name, shown in settings. |

**`chn002_channel_link_token`** — the Telegram handshake.

| Column | Notes |
|---|---|
| `user_id`, `channel`, `token` | Short random token, unique. |
| `expires_at`, `consumed_at` | Single use, 15-minute TTL. |

**Inbound**

**`chn003_interaction`** — a registered button, and its route home.

| Column | Notes |
|---|---|
| `user_id` | Owner. Checked against the callback's sender. |
| `owner_module` | `agenda`, `assistant`, … Written by whoever asked for the button; read to build the routing key. |
| `action` | `task_done`, `snooze_1h`, `confirm`, … Opaque to this module. |
| `payload` | jsonb, opaque. Handed back untouched to the owner. |
| `notification_id` | FK to the queue row that produced the button. Null for system-message buttons. |
| `expires_at`, `consumed_at` | Single use. A second click is "expired", not a second command. |

**`chn004_inbound_update`** — inbound idempotency and trail.

| Column | Notes |
|---|---|
| `provider`, `provider_update_id` | Composite PK. For Telegram, the `update_id`. Reprocessing is harmless by construction. |
| `raw` | jsonb of the raw update, for debugging. Short retention (see open question 3). |
| `user_id` | Resolved from `chn001`; null when the chat is unknown. |
| `classification` | `interaction` \| `command` \| `message` \| `discarded`. |
| `received_at`, `processed_at` | |

**Delivery**

**`chn005_notification_preference`** — delivery policy per category.

| Column | Notes |
|---|---|
| `user_id`, `category` | e.g. `agenda.reminder`, `agenda.task`, `identity.security`, `finances.statement`. |
| `channels` | Ordered array. Empty ⇒ muted. |
| `quiet_hours_start`, `quiet_hours_end` | In the user's timezone (from Identity preferences). |
| `quiet_hours_behaviour` | `suppress` \| `deliver_anyway`. See §5.5. |

Security notifications (`identity.*`) are **not configurable** — a password-reset email is not a
preference. The category registry marks those as mandatory.

**`chn006_notification`** — the durable queue. Today's `not001_notification`, renamed, plus
`rendered_payload`, `group_id` and `provider_message_id`.

| New column | Notes |
|---|---|
| `group_id` | Shared by the N rows produced by one request. |
| `rendered_payload` | jsonb, for structured channels (§5.3). |
| `provider_message_id` | The provider's message id after sending. Enables correlating a threaded reply (see §7.5). |

The dedup index migrates from `correlation_id` to **`(correlation_id, channel)`** — otherwise the
second channel is swallowed as a duplicate. This is the one migration over the existing table that
needs care.

### 5.5 Quiet hours

`defer_to_end` is **dropped**. Holding a delivery until morning is scheduling, and principle C1 says
scheduling does not live here. What remains is `suppress` and `deliver_anyway`; anyone who truly
wants deferral reschedules on the caller's side, where the scheduler already exists.

---

## 6. Templates

### 6.1 Who fills in what

The chain already exists in the code and is right; what changes is the channel dimension and where
the files live.

| Participant | Knows | Does not know |
|---|---|---|
| **Producer** (Identity, Agenda, …) | The fact: `PasswordResetRequested(userId, email, token, locale)`. | That email, templates or channels exist. |
| **Subscriber** (here) | The fact → `TemplateKey` + flat payload + category mapping. Builds derived values (the reset URL, from options). | How the text is written. |
| **Renderer** (here) | The file, by `(key, channel, locale)`. Substitutes placeholders and nothing else. | Where the payload came from. |
| **Queue** (here) | The already-rendered content. | Everything else. |

There are **two paths** into this, and the rule for picking one is simple: **whoever owns the buttons
owns the `NotifyUserRequested`**.

- **No buttons** (`identity.*`): the producer publishes a fact and a subscriber in *this* module maps
  it to a template. That is how Identity already works, and it stays that way.
- **With buttons** (`agenda.*`, `assistant.*`): the caller publishes `NotifyUserRequested` directly,
  because each button's action and payload are its domain. If a subscriber here had to invent the
  buttons, this module would need to know what a task is — exactly the coupling principle C5 exists
  to prevent.

Today's `NotificationEnqueuer` already does exactly this — `renderer.Render(...)` before
`Notification.Queue(...)`. The signature gains the channel:

```csharp
RenderedContent Render(TemplateKey key, Channel channel, string locale,
                       IReadOnlyDictionary<string, string> payload);
```

### 6.2 Where they live

**In files in the repository**, embedded as resources, not in the database. They are content that
goes through code review and needs to track the version of the code that fills them; a database
catalogue gives hot reload that is not needed and takes the text out of the diff.

What changes relative to `InMemoryNotificationTemplateRenderer`'s `switch` is scale: key × channel ×
locale does not fit a `switch`, but it fits a tree:

```
Templates/
├── password-reset/
│   ├── email.pt-BR.txt          (line 1 = subject, rest = body)
│   └── email.en.txt
└── agenda.reminder.due/
    ├── email.pt-BR.txt
    ├── email.en.txt
    ├── telegram.pt-BR.txt
    └── telegram.en.txt
```

A registry enumerates known keys × channels allowed per category × locales and **fails startup** if a
variant is missing. Today's in-memory renderer already enumerates its catalogue, so this is a
validation pass over it.

All logic leaves the renderer: what is today
`options.PasswordResetUrlTemplate.Replace("{token}", ...)` moves to the subscriber, which hands over
a ready `resetUrl` in the payload. The renderer becomes `{{resetUrl}}` substitution plus file
selection — and its test becomes trivial.

### 6.3 Buttons

Label is content; action is domain.

- The **caller** declares which actions to offer and each one's payload (`NotificationButton(action,
  payload)`).
- The **template** for the channel carries the labels per action, because labels have a locale:
  `buttons.task_done = ✓ Done`.
- The **merge** happens at render time. Channels that do not support buttons drop the list without
  error.

---

## 7. Inbound

### 7.1 Two drivers, one handler

The webhook needs public HTTPS, which does not exist at the start. Long polling covers that period —
and stays useful forever, because it works behind NAT with no tunnel at all, which is the development
scenario.

`getUpdates` accepts `timeout=30`: the request **hangs** until an update arrives or the timeout
expires. It is not short polling in a loop; it is an open connection that returns immediately.
Latency is practically the same as the webhook's.

```
Long-polling job ────┐
                     ├─► IInboundUpdateHandler ─► chn004 ─► triage (§7.2)
Webhook controller ──┘
```

No code beyond the driver knows which one is active.
`Channels:Telegram:Ingress = LongPolling | Webhook` is the only difference.

Three constraints long polling imposes, which need to be written down:

- `getUpdates` and `setWebhook` are **mutually exclusive**, and two consumers on the same bot token
  get `409 Conflict`. The job is a singleton; a second replica would require leader election.
- The **offset is the ack**. It advances in the same transaction that writes the `chn004` row;
  processing comes after. A crash in between reprocesses, and `provider_update_id` as PK makes that
  harmless.
- Telegram retains unconfirmed updates for **24 h**, so an outage of a few hours loses nothing.

The webhook controller, when it lands, is `POST /api/v{version}/channels/telegram/webhook` —
anonymous, protected by the `X-Telegram-Bot-Api-Secret-Token` header, verified in constant time.

### 7.2 Triage

Step zero, before any classification: **resolve `chat_id` → `user_id`** via `chn001`. An unknown chat
gets a generic message and is discarded — no enumeration.

Then three exits, decided by the **structure of the update**, never by its content:

| Update | Action | Destination |
|---|---|---|
| `callback_query` | Resolves `callback_data` in `chn003`; checks owner, validity and use; answers `answerCallbackQuery`. | Publishes `inbound.interaction.<owner_module>.<action>` |
| `/start <token>`, `/unlink`, `/status`, `/help` | Handled locally. Linking, unlinking, diagnostics. | Never becomes an event. |
| Free text, audio, photo | Normalizes. | Publishes `inbound.message.<channel>` |

Any other `/command` gets "I don't know that command" and stops there.

### 7.3 Interaction tokens

This is the mechanism that replaces the broadcast, and it exists for a concrete reason before any
architectural one: Telegram's `callback_data` is **64 bytes**. It does not hold a userId, an itemId,
an action name and an origin module. The indirection table is mandatory — and once it exists, it is
the right place to store who owns the button.

**Outbound.** At enqueue, for each declared button, write a `chn003` row with
`(user_id, owner_module, action, payload, notification_id, expires_at)`. The rendered `callback_data`
is the row's id.

**Inbound.** The callback resolves the id, and the routing key comes from the `owner_module` column:

```
inbound.interaction.agenda.task_done
```

Only Agenda's queue is bound to `inbound.interaction.agenda.#`. No other module wakes up, and no
module filters.

What the table gives for free: expiry (yesterday's button does not act today), single use (clicking
"Done" twice is a handled case instead of two commands), and the FK to the originating notification.

**The sentence that summarises the design:** the reply does not go back to the notification, it goes
back to the **button**. A notification has no return channel; the button does, because it was
*registered* on the way out.

### 7.4 Linking

1. User opens Settings → Notifications → *Connect Telegram*.
2. The backend issues a `chn002_channel_link_token` and returns `https://t.me/<bot>?start=<token>`.
3. User taps; Telegram sends `/start <token>`.
4. Triage consumes the token, writes `chn001` with the chat id and `is_verified`, and replies with a
   confirmation message.

The token is the only thing binding a chat to an account, it is single-use and short-lived, and the
chat id is never accepted from the client.

### 7.5 Threaded replies *(optional, phase C4)*

With `provider_message_id` stored on the queue row, a threaded reply in Telegram
(`reply_to_message_id`) lets the module enrich `inbound.message` with the originating notification's
correlation. Assistant gains context — "this was about *that* reminder" — without anyone having to
interpret text to find out.

---

## 8. Contracts

Published from `Channels.Contracts`. All broker-ready POCOs, no domain value objects.

### 8.1 Work coming in

```csharp
public sealed record NotifyUserRequested(
    Guid   EventId,
    DateTimeOffset OccurredAt,
    Guid   UserId,
    string Category,                   // → preference lookup
    string TemplateKey,
    string? Locale,                    // null ⇒ user preference
    IReadOnlyList<string>? Channels,   // null ⇒ user preference for the category
    IReadOnlyDictionary<string,string> Payload,
    IReadOnlyList<NotificationButton>? Buttons,
    Guid   CorrelationId) : IIntegrationEvent;

public sealed record NotificationButton(string OwnerModule, string Action, string? Payload);
```

Today's `SendNotificationRequested` stays for admin/ad-hoc sends with an explicit address.

### 8.2 Inbound going out

```csharp
[IntegrationEventName("inbound.interaction")]   // full key: inbound.interaction.{module}.{action}
public sealed record InboundInteractionReceived(
    Guid   EventId, DateTimeOffset OccurredAt,
    Guid   UserId, string Channel,
    string OwnerModule, string Action, string? Payload,
    Guid?  SourceCorrelationId) : IIntegrationEvent;

[IntegrationEventName("inbound.message")]       // full key: inbound.message.{channel}
public sealed record InboundMessageReceived(
    Guid   EventId, DateTimeOffset OccurredAt,
    Guid   UserId, string Channel,
    string? Text, string? MediaRef, string? MediaMimeType,
    Guid?  InReplyToCorrelationId) : IIntegrationEvent;
```

`MediaRef` is opaque — for Telegram, the `file_id`. Bytes are fetched through a port:

```csharp
public interface IInboundMediaReader
{
    Task<Stream> OpenAsync(string channel, string mediaRef, CancellationToken ct = default);
}
```

It is the only thing Assistant calls in this module, and it is what lets Assistant never learn that
Telegram exists.

### 8.3 Delivery failure

```csharp
public sealed record UserChannelDisabled(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid UserId, string Channel, string Reason) : IIntegrationEvent;
```

A permanent provider error (Telegram *chat not found*, *bot blocked*) marks the row `Dead`
immediately, disables that channel for the user with a reason, and publishes the fact — there is no
point retrying five times against a blocked bot, and the user needs to know it stopped.

---

## 9. Frontend

No screen of its own. The module contributes a **Notifications** section in settings:

- Connect/disconnect Telegram, with link status.
- Channels per category, with the disabled-channel warning and its reason.
- Quiet hours.
- Delivery history, filterable, with status and last error.
- Test send per channel.

---

## 10. Tars building block: `Communication.Telegram`

Mirrors the existing `Communication.Email` / `Communication.Email.MailKit` split, which is the
established pattern in Tars.

| Project | Contents |
|---|---|
| `Pottmayer.Tars.Communication.Telegram.Abstractions` | `ITelegramClient`, `TelegramMessage`, `InlineKeyboard`/`InlineButton`, `TelegramUpdate` and friends, `TelegramSendResult`, `TelegramException` with permanent/transient distinction. |
| `Pottmayer.Tars.Communication.Telegram` | Bot API implementation over `HttpClient`: `sendMessage`, `answerCallbackQuery`, `getUpdates`, `getFile`/download, `setWebhook`, MarkdownV2 escaping, secret-token validation, options binding, DI extension. |

Deliberately thin: transport plus models. Templates, retries, addressing, triage and persistence are
Pandora's business and already exist here. Its documentation goes in the Tars repository
(`docs/communication/telegram.md`), next to the email building block.

The other building blocks this plan assumes — `Messaging.RabbitMq` and `Messaging.Outbox` — are
described in the [messaging doc](../../../architecture/en/messaging.md).

---

## 11. Roadmap

### Phase C1 — Rename *(cheap now, expensive later)*
- `Notifications` → `Channels`: projects, schema, table prefix, routes, migrations.
  `not001_notification` → `chn006_notification`.
- `Channel.Telegram` in the smart enum; `Recipient` → `NotificationAddress`.
- Update the references in the Finances docs (`architecture.md`, `overview.md`,
  `implementation-status.md`, `jobs-and-integration.md`), which name the old module today.
- **Done when:** Identity's emails keep arriving and nothing but the name changed.

### Phase C2 — Outbound Telegram
- `Tars.Communication.Telegram`; internal `IChannelTransport` with both transports.
- Per-channel rendering; templates move out of the `switch` into files; catalogue validation at
  startup.
- `chn001`, `chn002`; deep-link linking.
- Permanent-error handling that disables the channel and publishes `UserChannelDisabled`.
- **Done when:** a test send arrives in a linked chat, with a button that does nothing yet.

### Phase C3 — Preferences and fan-out
- `chn005`; `NotifyUserRequested` contract; fan-out in the enqueuer; dedup by
  `(correlation_id, channel)`.
- Identity subscribers migrated to the per-user path.
- **Done when:** one request becomes two rows with independent retry.

### Phase C4 — Inbound
- `chn003`, `chn004`; `IInboundUpdateHandler`; long-polling driver; triage; routing by key.
- `IInboundMediaReader`; `provider_message_id` and threaded replies.
- First consumer: Agenda (`task_done`, `snooze_1h`).
- **Done when:** pressing *Done* closes the task, and the second click says it expired.

### Phase C5 — Operations
- Webhook driver, once public HTTPS exists.
- Delivery-history endpoint; manual retry of a dead row; test send per channel.
- Metrics: queue depth, dispatch latency, failure rate per channel, discarded updates.
- **Done when:** "did my reminder actually go out?" has an answer in the UI.

### Phase C6 — Extraction as a service *(future, no date)*
Not the first candidate — [Assistant](../../assistant/en/product-plan.md) is, because of the
long-running work. But the seams are established: POCO contracts, its own `ChannelsDbContext`, no
access to anyone else's schema, an independent HTTP surface. See the
[messaging doc](../../../architecture/en/messaging.md).

---

## 12. Open questions

1. **One address per channel.** Two Telegram chats (personal + a group) is out of scope; the unique
   constraint makes that a deliberate future change.
2. **Whether Finances joins.** Its statement/import events are documented as planned but not
   published. Once C3 lands, they get categories for free — worth a small follow-up phase there.
3. **Retention of `raw` in `chn004`.** Keeping the raw update is gold for debugging and is personal
   data (potentially including transcripts). Leaning: 7 days, background purge, configurable. Decide
   in C4.
4. **Categories as a typed registry or a string.** Today `Category` is a string in the contract. A
   central registry would give startup validation ("Agenda declared `agenda.reminder`") at the cost
   of one more place to touch when a module is born. Leaning: string until it hurts.
