# Overview — Boundary & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. What the module does

**Communications** is the user's **inbox** inside Pandora: the messages that arrive *to* the user,
from any provider, in one place. Email comes first.

Built today (M1, connect and read; M2, organize; M3, send):

- Connect **any number of mailboxes**: Gmail through the Gmail API, Titan Email and any other server
  through IMAP.
- **Mirror** every mailbox's headers and labels, newest first, and keep them current with a background
  job (every 2 minutes) or "Sync now".
- **Read** messages in one list, filtered by mailbox, label, unread or text. Bodies are fetched when a
  message is opened, then cached.
- **Show mail safely**: HTML is sanitized and rendered in a sandboxed iframe, and remote images stay
  blocked until the user asks for them.
- **Organize**: read and unread, star, move, archive and trash, in batches, and labels created, renamed and deleted. It all goes to the
  provider first, and what is changed in the webmail comes back to Pandora.
- **Send**: compose, reply, reply all and forward, with attachments. A send is never repeated when the mail may have gone.
- **Publish `MailMessageReceived`** through the outbox for each new message, so other modules can
  process mail. Nothing consumes it yet.

Rules (M4) are a plan: see the [product plan](product-plan.md).

## 2. What it is not

- **Not outbound notifications.** Pandora telling the user something (Telegram, system email) is
  [Channels](../../channels/README.md). Communications is the other direction: the user's own mailboxes.
- **Not a mail server or an archive.** The provider keeps the mail. Removing a mailbox from Pandora
  deletes Pandora's copy and nothing at the provider.
- **Not a credential store.** Passwords and OAuth tokens live in
  [Integrations](../../integrations/README.md); a mailbox stores only the id of its account.
- **Not a way to lose mail.** Nothing here deletes a message for good: "delete" is the provider's trash, and only an empty IMAP folder
  can be deleted, since what is inside would go with it.

## 3. Core principles

1. **The provider is the source of truth.** The local mirror can be rebuilt by syncing again; that is
   exactly what happens when a sync cursor expires.
2. **One neutral model: messages + labels.** A Gmail label and an IMAP folder are both a **label**. A
   folder is a label marked **exclusive**: a message carries at most one.
3. **Providers declare what they can do.** Everything above `IMailboxProvider` sees the same operations
   and opaque cursors, whichever provider is behind them.
4. **Headers mirrored, bodies on demand.** Bodies are fetched on open and cached; attachments are never
   stored.
5. **Remote content is blocked by default.** A tracking pixel never fires just because a message was
   opened.
6. **The provider first.** A user's action goes to the provider, and the row changes only after it accepts. A failure changes nothing.
7. **Single instance.** Each mailbox has two in-process gates, the same assumption as Integrations' refresh gate.

## 4. Ubiquitous language (glossary)

| Term | Meaning |
|---|---|
| **Mailbox** | One connected email account (`com001`), running on one Integrations account. |
| **Provider** | How a mailbox is reached: `gmail`, `titan` (IMAP with Titan's servers preset) or `imap`. |
| **Label** | A Gmail label or an IMAP folder (`com002`). |
| **Exclusive label** | A label a message can have only one of: an IMAP folder. |
| **Role** | What a label is for: `inbox`, `sent`, `drafts`, `trash`, `spam`, `archive`, or none. |
| **Backfill** | The first download of a mailbox's headers, newest first, in resumable batches. |
| **Cycle** | One sync of one mailbox: labels, then changes, then backfill batches while there is budget. |
| **Sync cursor** | Where the incremental sync stopped: a Gmail `historyId` or the IMAP state per folder. Opaque outside the adapter. |
| **Snapshot** | Every message id a label has right now; on IMAP, how removals are found. |
| **Thread key** | What groups a conversation: Gmail's `threadId`, or on IMAP the first Message-ID of `References`. |
| **Content cache** | Opened bodies (`com005`), deleted when not opened for `ContentCacheRetentionDays`. |

## 5. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

The `communications` schema (`com001`–`com005` + outbox); the Gmail and IMAP adapters; backfill and
incremental sync; the content cache and attachments; the mailbox, label and message endpoints; labels both ways and batch actions; sending;
`MailMessageReceived`; the `mail` and `mail/accounts` screens.

### Out of scope / future (see [product-plan.md](product-plan.md))

| Feature | Phase |
|---|---|
| Rules, with Gmail filters both ways | M4 |
| Gmail push instead of polling, Radar consuming the event, more providers | Later |
