# API Reference

[← Back to index](../README.md) · Related: [Inbound & Linking](inbound-and-linking.md)

Base path: **`/api/v{version}/channels`**. All endpoints are authenticated and scoped to the token's
user. The inbound Telegram traffic does **not** arrive here — it is ingested by the long-polling
background service, not an HTTP endpoint. Errors are mapped from typed `Result` failures.

---

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/` | The user's addresses (linked or disabled), for the settings screen. |
| POST | `/{channel}/link` | Start the linking handshake; returns the deep link the user taps. |
| DELETE | `/{channel}/link` | Forget the address on this channel. |
| POST | `/{channel}/test` | Queue a test message to the user's own address on this channel. |
| GET | `/notifications` | Delivery history, newest first — filterable by status, channel, category, date; paged. |
| GET | `/preferences` | The user's channel choices per category. |
| PUT | `/preferences/{category}` | Set the channels a category goes out on (empty list mutes; unknown channels rejected). |

### GET `/`

Returns the user's `chn001` rows: channel, address, verified/enabled flags, metadata — the settings
list.

### POST `/{channel}/link`

For Telegram, returns `{ deepLink, token }`: the `https://t.me/<bot>?start=<token>` link the user taps.
The chat id arrives later, from Telegram, carrying the token this issued.

### DELETE `/{channel}/link`

Removes the `chn001` address for the channel.

### POST `/{channel}/test`

Enqueues a test notification to the user's own verified address on the channel — the "did linking
work?" check.

### GET `/notifications?status=&channel=&category=&from=&to=&skip=&take=`

The delivery history read (`GetDeliveryHistory`), backed by `ix_chn006_user_created_at`. Answers "did
my reminder actually go out?".

### GET `/preferences`

Returns the user's `chn005` preferences per category.

### PUT `/preferences/{category}`

```json
{ "channels": ["telegram", "email"] }
```

Sets the ordered channel list for a category. An empty list mutes it; unknown channels are rejected.
`identity.*` categories are mandatory and not settable.

---

## Contracts (in-process events)

Not HTTP, but the module's public surface on the bus:

| Direction | Event | Meaning |
|---|---|---|
| in | `NotifyUserRequested` | Caller-owned notification with buttons (agenda.*, assistant.*). |
| in | `SendNotificationRequested` | Ad-hoc addressed send. |
| in | Identity facts (`PasswordResetRequested`, `AccountActivationRequested`, `MfaEnabled`, …) | Mapped to templates by this module's subscribers. |
| out | `InboundInteractionReceived` | An inline-button tap, routed to the owning module. |
| out | `InboundMessageReceived` | A text/voice message, for Assistant. |
| out | `UserChannelDisabled` | A channel disabled after a permanent failure. |
