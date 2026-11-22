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
| POST | `/mailboxes/{id}/labels` | Create a label |
| PATCH | `/labels/{id}` | Rename a label |
| DELETE | `/labels/{id}` | Delete a label |
| GET | `/mailboxes/{id}/rules` | The rules, in order |
| POST | `/mailboxes/{id}/rules` | Create a rule |
| PUT | `/mailboxes/{id}/rules/order` | Reorder the rules |
| PUT | `/rules/{id}` | Edit, enable or disable a rule |
| DELETE | `/rules/{id}` | Delete a rule |
| GET | `/messages` | List messages, newest first |
| GET | `/messages/{id}` | One message with its body, fetched and cached on the first open |
| GET | `/messages/{id}/attachments/{attachmentId}` | The attachment's bytes |
| POST | `/messages/actions` | One action on up to 100 messages of one mailbox |
| POST | `/messages/send` | Send a message |
| GET | `/messages/{id}/compose` | The prefilled form for a reply or a forward |

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

### POST `/mailboxes/{id}/labels`, PATCH `/labels/{id}`, DELETE `/labels/{id}`

`{ name }`, trimmed, 1 to 100 characters. Each answers with the mailbox's labels as they are after the operation. Only a user's label can be renamed or
deleted. On Gmail a deleted label only leaves its messages; on IMAP only an empty folder with no subfolders can go.

| Status | When |
|---|---|
| 404 | The mailbox or label does not exist or is someone else's |
| 409 | The folder has messages or subfolders (`LabelNotEmpty`) |
| 422 | A system label (`SystemLabel`), a bad name (`LabelNameInvalid`), or the provider's refusal |

### GET `/mailboxes/{id}/rules`, POST `/mailboxes/{id}/rules`, PUT `/rules/{id}`, DELETE `/rules/{id}`, PUT `/mailboxes/{id}/rules/order`

Every one answers with the mailbox's rules as they are now, in order: `id`, `name`, `position`, `conditions`, `actions`, `execution`, `isEnabled`, `status`, `lastError`, `providerFilterRaw`. Someone else's is a 404.

```json
{ "name": "Bills", "conditions": [{ "kind": "from", "value": "bank" }], "actions": [{ "kind": "move", "labelId": "…" }] }
```

`PUT /rules/{id}` takes the same plus `isEnabled`. `PUT …/order` takes `{ "ruleIds": [] }`: those, then any other.

On a mailbox with filters, the provider goes first and its refusal changes nothing (a 422 with its message, such as over 1,000 filters).

- **A new name alone** does not reach Gmail. **An edit** is create, save, delete; if only the delete fails, this is a 200 with `lastError` saying so, and the old filter comes back as a rule of its own on the next cycle.
- **Disabling** deletes the filter and keeps the rule; **enabling** makes it again. **Deleting** deletes the filter, for good.
- **A read-only rule** answers `RuleReadOnly` to a `PUT`. It can be deleted.
- **Without `gmail.settings.basic`**, writes answer `MissingFilterScope`. The mailbox goes on.

The mailboxes carry `filterSyncError`: why the last read of Gmail's filters failed.

### POST `/messages/actions`

```json
{ "mailboxId": "…", "messageIds": ["…"], "action": "move", "labelId": "…" }
```

`action`: `mark-read`, `mark-unread`, `star`, `unstar`, `move`, `archive`, `trash`, `add-label`, `remove-label`; `labelId` goes with `move`, `add-label` and
`remove-label`. The provider goes first: a failure changes nothing. Answers with the messages as they are now. There is no way to delete a
message for good.

| Status | When |
|---|---|
| 404 | The mailbox, a message or the label does not exist or is someone else's; a message of another mailbox stops the whole action, before the provider |
| 422 | Not 1–100 ids (`MessageCountInvalid`), an unknown action or labels on IMAP (`ActionNotSupported`), a label that cannot be used (`SystemLabel`), no such folder (`NoTrashFolder`, `NoArchiveFolder`), or the provider's refusal |

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
- **This endpoint does not mark the message as read.** The screen sends `mark-read` after it opens.
- The first open calls the provider, so it can fail with the provider's errors.

### GET `/messages/{id}/attachments/{attachmentId}`

The bytes, with `X-Content-Type-Options: nosniff`. `Content-Disposition` is `inline` only for PNG, JPEG, GIF, WebP
and BMP, and `attachment` for anything else. It also serves `cid:` images.

### POST `/messages/send`

`multipart/form-data`: `mailboxId`, `sendKey` (a uuid made when the form opens), `to`, `cc`, `bcc` (bare addresses, one field each), `subject`, `textBody`, `htmlBody`, `replyToId?` or `forwardOfId?`, `keepAttachmentIds` (which of the forwarded message's files go along), `files`. Answers `{ copySaved }`. **This cannot be taken back.**

| Status | When |
|---|---|
| 200 | Sent. `copySaved: false` means it went, but no sent copy could be saved |
| 404 | The mailbox or the original does not exist or is someone else's, or a kept file does not exist |
| 409 | This key was already handled (`AlreadySent`) |
| 422 | Bad or no recipients, over 100 (`RecipientsInvalid`); attachments over 25 MB (`MessageTooLarge`); a refusal by the provider (the key is freed); `SendUncertain`: it **may have gone**, and the key stays taken |

### GET `/messages/{id}/compose?mode=reply|reply-all|forward`

The form, worked out from the raw message: `to`, `cc`, `subject`, `inReplyTo`, `references`, `originalFrom`, `originalDate`, `originalText` (as written; the screen quotes it), `attachments` (a forward's files, without inline images).

---

## Event

**`MailMessageReceived`** (Contracts), through the outbox: `EventId`, `OccurredAt`, `UserId`, `MailboxId`,
`MessageId`, `From`, `Subject`, `Labels`. Published for a message that arrives after the backfill, and only when
the mailbox has no row with the same Message-ID.

## Copy a message to Notes

`POST /messages/{id}/notes { title?, spaceId?, folderId? }` returns the created Notes page UUID in
the normal envelope. Omitted title uses the subject (Email if empty); explicit titles must be
nonblank and at most 255 characters. A folder determines its owning space; without a folder the
selected/default space is used. Requires communications.messages.read, notes.pages.read/create,
notes.spaces.read and notes.folders.read, and both module features. Foreign/missing message or
destination returns 404; missing access 403; invalid title/page limits 422. Body is read
server-side, converted to literal text and copied through the Notes import port; attachments are
excluded. No provider flags are changed. Each explicit save creates a new independent page. Its
bookmark opens `/mail?message={id}`, subject to normal authentication/ownership checks.
