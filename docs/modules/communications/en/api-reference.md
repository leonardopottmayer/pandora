# API Reference

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

Base path: **`/api/v{version}/communications`**. Every endpoint is authenticated and scoped to the caller's
mailboxes: another user's mailbox, message or account answers 404, the same as one that does not exist.
Errors are mapped from typed `Result` failures by the shared HTTP error mapper.

Passwords are saved first through Integrations, with `PUT /api/v1/integrations/{provider}/password`, which
returns the account id a mailbox needs. A Google account comes from `POST /api/v1/integrations/google/connect`.

---

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/mailboxes` | The caller's mailboxes, with status and backfill progress |
| POST | `/mailboxes` | Test the connection and create a mailbox |
| PATCH | `/mailboxes/{id}` | Change the display name and, on IMAP, the servers |
| DELETE | `/mailboxes/{id}` | Delete the local copy; nothing changes at the provider, and the credential stays |
| POST | `/mailboxes/{id}/sync` | Run one cycle now and return the mailbox |
| GET | `/mailboxes/{id}/labels` | The labels, with total and unread counts |
| GET | `/messages` | List messages, newest first |
| GET | `/messages/{id}` | One message with its body, fetched and cached on the first open |
| GET | `/messages/{id}/attachments/{attachmentId}` | The attachment's bytes |

### GET `/mailboxes`

`MailboxDto`: `id`, `provider`, `externalAccountId`, `address`, `displayName`, `imapHost`, `imapPort`,
`smtpHost`, `smtpPort`, `status`, `lastError`, `lastSyncedAt`, `messageCount`, `messageTotalEstimate`,
`backfillCompletedAt`. Backfill progress is `messageCount / messageTotalEstimate`.

### POST `/mailboxes`

```json
{
  "provider": "titan",
  "externalAccountId": "…",
  "address": null,
  "displayName": "Work",
  "imapHost": null, "imapPort": null, "smtpHost": null, "smtpPort": null
}
```

- `provider`: `gmail`, `titan` or `imap`. The account must be of that kind (`gmail` needs a `google` account).
- `address` is optional: it defaults to the Google email or the IMAP username.
- The four server fields go together. None takes the preset, which only Titan has; `imap` requires them.
- The connection is tested **before** anything is created.
- It does **not** start the backfill: the next cycle does. The screen calls `sync` right after.

| Status | When |
|---|---|
| 404 | The account does not exist or is someone else's (`AccountNotFound`) |
| 409 | The account already has a mailbox (`AlreadyConnected`) |
| 422 | Unknown provider, wrong account kind, invalid address or servers, or a refused test (`MailboxAuthFailed`, `MissingScopes`, `ProviderFailed`) |

### PATCH `/mailboxes/{id}`

`{ displayName, imapHost, imapPort, smtpHost, smtpPort }`. The display name is replaced. New servers (all four,
or none to keep the current ones) are tested first and, when they pass, also reconnect the mailbox.

### POST `/mailboxes/{id}/sync`

A `needs-reconnect` mailbox is tested first: a refused test is a 422 and nothing changes; a passing one
reconnects it and the cycle runs. **A cycle that fails is not an HTTP error**: the mailbox comes back with its
`status` and `lastError`.

### GET `/mailboxes/{id}/labels`

`LabelDto`: `id`, `name`, `kind`, `role`, `isExclusive`, `total`, `unread`.

### GET `/messages`

| Query | Notes |
|---|---|
| `mailboxId` | Optional; without it, all the caller's mailboxes |
| `labelId` | |
| `unread` | |
| `q` | Case-insensitive substring of subject, snippet and sender (`ILIKE`). On IMAP the snippet is empty until a message is opened, and bodies are never searched |
| `threadKey` | One conversation |
| `before` | The previous page's `nextCursor`; a bad one is a 422 |
| `limit` | 1–100, default 50 |

Returns `{ items: MessageSummaryDto[], nextCursor }`, with a null `nextCursor` on the last page. The cursor is
a keyset over `(sent_at, id)`, stable while new mail arrives.

`MessageSummaryDto`: `id`, `mailboxId`, `threadKey`, `from`, `to`, `cc` (`{ name, address }`), `subject`,
`snippet`, `sentAt`, `isUnread`, `isStarred`, `hasAttachments`, `labelIds`.

### GET `/messages/{id}`

`MessageDto`: `message` (the summary), `rfc822MessageId`, `textBody`, `htmlBody`, `attachments`
(`{ id, fileName, contentType, sizeBytes, contentId }`).

- **`htmlBody` is as received.** The client must sanitize it before rendering.
- **Opening does not mark the message as read.**
- The first open calls the provider, so it can fail with the provider's errors.

### GET `/messages/{id}/attachments/{attachmentId}`

The bytes, with `X-Content-Type-Options: nosniff`. `Content-Disposition` is `inline` only for PNG, JPEG, GIF, WebP
and BMP, and `attachment` for anything else. It also serves `cid:` images.

---

## Event

**`MailMessageReceived`** (Contracts), through the outbox: `EventId`, `OccurredAt`, `UserId`, `MailboxId`,
`MessageId`, `From`, `Subject`, `Labels`. Published for a message that arrives after the backfill, and only when
the mailbox has no row with the same Message-ID.
