# Inbound & Linking

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. Linking a Telegram chat

A chat id is **never accepted from the client**. The only thing that authorizes one is a single-use
handshake:

```
1. SPA    → POST /channels/telegram/link          ← { url, expiresAt }   (chn002 issued)
2. User taps the deep link → Telegram opens the bot with /start <token>
3. Bot receives the update; triage classifies it as a Command
4. ConsumeTelegramLink: validate & consume the token, upsert chn001 (verified),
   store the chat id + metadata (username/first name)
5. SPA    → GET /channels                          ← Telegram now shows as linked
```

`ChannelLinkTokens` mints the token; `ConsumeTelegramLinkCommand` consumes it. Tokens are single-use
with a ~15-minute TTL. `UnlinkChannel` forgets the address.

## 2. Two drivers, one handler

Ingress runs over **long polling** (`TelegramLongPollingService`): `getUpdates(timeout=30)` holds the
connection open until an update arrives or the timeout expires — an open connection that returns
immediately, not short polling in a loop. Long polling works behind NAT with no tunnel, which is the
development and homelab scenario.

A **webhook driver** is deferred (see [product-plan.md](product-plan.md)); when it lands it hands
incoming updates to the *same* triage the long-polling driver uses.

## 3. Idempotent ingress

Every update is written to `chn004_inbound_update` **before** it is processed. Because
`(provider, provider_update_id)` is unique, reprocessing is harmless: a crash between write and
processing replays the update instead of losing it, and the highest `provider_update_id` seen is the
long-polling offset on startup.

## 4. Triage — structural, never semantic

`TelegramInboundTriage` classifies each update into one of four `classification` values, purely by
structure:

| Classification | What it is | What happens |
|---|---|---|
| **Interaction** | An inline-button callback (`callback_data` = an `chn003` id) | Resolve the interaction, check the sender owns it, consume it (single use — a second tap is "expired"), and publish `InboundInteractionReceived` to the owning module. |
| **Command** | A `/command` — notably `/start <token>` | Handled here (linking); other commands are routed as owned. |
| **Message** | Free text or a voice note | Publish `InboundMessageReceived(userId, channel, text?, mediaRef?, mediaMimeType?)`. |
| **Discarded** | Unknown chat, unusable update | Recorded and dropped. |

The module resolves an id in a table and reads the column the owning module wrote; it **never
interprets what an action means** (principle C5).

## 5. Routing back to owners

- **Interactions** carry `owner_module` + `action` + opaque `payload`. Channels builds the routing key
  from `owner_module` and publishes `InboundInteractionReceived`; the owner (e.g. Agenda:
  `task_done`, `snooze_1h`) acts and the button is already consumed. This is the mechanism behind
  *"pressing Done closes the task, and the second click says it expired."*
- **Messages** are published as `InboundMessageReceived`. Assistant is the intended consumer — it
  transcribes/interprets/executes. Channels does not process the content.

## 6. Media (voice notes)

Assistant needs the audio bytes but must not know Telegram exists. Channels exposes one port from
`Abstractions`:

```csharp
public interface IInboundMediaReader
{
    Task<Stream> OpenAsync(string channel, string mediaRef, CancellationToken ct);
}
```

`TelegramInboundMediaReader` implements it over the Bot API (`getFile` + download). The
`InboundMessageReceived` event carries `mediaRef` + `mediaMimeType`; the consumer opens the stream
through the port. Swapping to WhatsApp, or coming in from the web, touches nothing in the consumer.

## 7. Raw retention

`chn004.raw` holds personal data kept only for debugging. `InboundUpdateRetentionBackgroundService`
runs daily and nulls the `raw` payload of rows older than `Channels:RawRetention:RetentionDays`
(default 7; toggled by `Channels:RawRetention:Enabled`), **keeping the row** — it is still the
idempotency guard and the long-polling offset.
