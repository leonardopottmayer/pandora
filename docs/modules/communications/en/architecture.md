# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [API Reference](api-reference.md)

---

## 1. Project layout

The module mirrors the other Pandora modules, under `backend/src/Modules/Communications/`:

```
Pottmayer.Pandora.Modules.Communications.
  Abstractions      → CommunicationsModule (name, key, schema), CommunicationsOptions
  Contracts         → IntegrationEvents: MailMessageReceived
  Domain            → Entities (Mailbox, MailLabel, MailMessage, MailContent), ValueObjects
                      (MailProvider, MailboxStatus, LabelKind, LabelRole, ThreadKey),
                      Ports (IMailboxProvider, repositories), MailErrors
  Application       → Commands (ConnectMailbox, UpdateMailbox, DeleteMailbox, SyncMailbox,
                      SyncMailboxNow, OpenMessage, ApplyMessageAction, ManageLabels, SendMessage), Queries (GetMailboxes, GetLabels, ListMessages,
                      GetAttachment), MimeReader, subscribers, the account usage source
  Infrastructure    → Gmail/ and Imap/ adapters, Jobs/MailboxSyncBackgroundService
  Persistence       → EntityConfigs, Repositories, DbContext (with the outbox)
  Presentation      → CommunicationsController, requests
```

The screens are in `client-web/src/modules/communications`.

## 2. The provider port

`IMailboxProvider` (Domain/Ports) has one adapter per kind of provider, registered as a keyed service
under each `MailProvider` value it serves, so the engine asks for the mailbox's provider by key:

| Operation | What it does |
|---|---|
| `Capabilities` | `MultipleLabels`, `ServerFilters`, `NativeThreads` |
| `TestConnectionAsync` | Used before a mailbox is created or its connection changed |
| `ListLabelsAsync` | Every label or folder |
| `StartBackfillAsync` | Takes the incremental cursor **now**, plus the backfill cursor and the total |
| `FetchBackfillBatchAsync` | The next batch, newest first |
| `FetchChangesAsync` | What changed since the cursor: upserts, removed ids, snapshots, new cursor |
| `GetRawMessageAsync` | The whole RFC 822 message, without marking it as read |
| `CreateLabelAsync`, `RenameLabelAsync`, `DeleteLabelAsync` | Labels; an IMAP folder with messages or subfolders is refused |
| `SetFlagsAsync` | Read and starred |
| `ChangeLabelsAsync` | Add and remove labels, where `MultipleLabels` |
| `MoveAsync`, `TrashAsync` | Return each message's id after the move, or null when the provider does not say |
| `SendAsync` | Sends a whole RFC 822 message and sees that a sent copy exists; never repeated on a doubt |

Taking the incremental cursor before the first batch is what keeps mail from being lost: anything that
arrives or changes during the backfill shows up in the next `FetchChangesAsync`.

### Adapters

| | `GmailMailboxProvider` (`gmail`) | `ImapMailboxProvider` (`titan`, `imap`) |
|---|---|---|
| **Client** | REST with `HttpClient`, no Google SDK | MailKit, one connection per call |
| **Credential** | `GetAccessTokenAsync`; the token must carry `gmail.modify` or `mail.google.com` | `GetPasswordAsync` |
| **Sync cursor** | `historyId` | JSON per folder: UIDVALIDITY, UIDNEXT, MODSEQ |
| **Backfill cursor** | `pageToken` | JSON per folder: the lowest UID read; one folder per batch, INBOX first |
| **New and changed** | `history.list`; every touched message is read again in full | UIDs at or above UIDNEXT; flags through CONDSTORE, or the last 200 UIDs without it |
| **Removed** | `messagesDeleted`, or a 404 on re-reading | A snapshot of each folder's UIDs (`SEARCH ALL`) every cycle |
| **Message id** | Gmail's id | `{uidvalidity}:{uid}:{folder path}` |
| **Cursor expired** | History answers 404 | UIDVALIDITY changed |
| **Read / starred** | `UNREAD` / `STARRED` labels, turned into flags | `\Seen` / `\Flagged` |
| **Sent date** | `internalDate` | `Date` header, or the internal date |
| **Folders or labels in scope** | Every label | Every selectable folder of the personal namespace; subscriptions are ignored |
| **Write flags and labels** | `messages.batchModify` | `STORE` per folder |
| **Move** | Add the target, remove `INBOX`; the id stays | `MOVE`, reading the new UID |
| **Trash** | `messages.trash`, one call per message | Move to the folder with role `trash` |
| **Send** | `messages.send`, multipart upload with `threadId` | SMTP with the same credential |
| **Sent copy** | Gmail keeps its own | Looks for the Message-ID in the sent folder, waits 3 s, looks again, and appends only if it is not there |

The Gmail adapter sends two `messages.get` at a time and retries `429`, `5xx` and a rate-limit `403`
after 1, 2, 4, 8 and 16 s (a `Retry-After` wins), writes included. Four at a time drew frequent `403`s on a real
64,000-message backfill. Google's quota is 6,000 units per user per minute, and a `messages.get` costs 20, so a user gets at most
5 per second whatever the parallelism. `messages.delete` and `batchDelete` are never called.

## 3. The sync cycle

`SyncMailboxCommand`, one mailbox. Steps 1–2 are `MailboxChangesStep`, which label operations also run:

```
1. Labels     ListLabels → create and update local labels
2. Changes    no cursor yet       → StartBackfill
              cursor expired      → delete the mailbox's messages, StartBackfill
              otherwise           → apply FetchChanges, store the new cursor
              then                → remove the labels the provider stopped listing
3. Backfill   batches back to back until it ends or BackfillBudget runs out (at least one)
4. Success    RecordSuccess
```

**Two gates per mailbox** (`MailboxGates`). `Cycle` covers the whole cycle, so the job and "Sync now" never run two at once. `Action` covers a
user's action, or steps 1–2. The backfill runs outside `Action`, so a click never waits for it. A stale batch can overwrite an action, and the
next cycle corrects it, since the action is a change at the provider: the screen can be wrong for one cycle.

**Stitching.** On IMAP a move gives a message a new id. A message that vanishes and reappears with its Message-ID in the same apply keeps its row
and its cached body, and it is not an arrival. It is one to one, so two copies stay two rows, and a message with no Message-ID is delete + create.
A folder renamed in the webmail looks like one gone and one new, which is why a vanished label is removed only after the changes are applied. A
rename in Pandora takes the same path, since a folder's path is inside each message's id and in the cursor.

- **Provider calls run outside transactions.** Each result is applied in its own unit of work, so a slow
  server never holds a transaction open, and a failure keeps what was already applied. A batch and the
  cursor that follows it are saved together, so a failed batch is simply read again.
- **`MailMessageReceived`** comes only from the incremental path, in the same transaction as the
  message, and only when the mailbox has no row with that Message-ID. A backfill publishes nothing.
- **A label the provider stopped listing** is deleted. On IMAP its messages go with it; on Gmail they
  only lose the label.
- **All dates are saved in UTC.** IMAP hands over the sender's offset and Npgsql only writes UTC to
  `timestamptz`, so `MailMessage` converts on create and on update.

### The job

`MailboxSyncBackgroundService` runs every `SyncInterval`: one cycle per mailbox **in sequence**, skipping
`needs-reconnect`, then the content cache retention. `PeriodicTimer` skips the ticks a long walk covers.
"Sync now" (`POST mailboxes/{id}/sync`) runs one cycle inside the request, under the same gate.

### Failures

| Failure | Result |
|---|---|
| IMAP password refused | `needs-reconnect` and one notice, only on the change (`communications.mailbox-auth-failed`, through Channels) |
| Gmail auth failure or missing scopes | `needs-reconnect` with no notice: Integrations already warns on a revoked grant, and the screen shows the rest |
| `ExternalAccountRevoked` / `ExternalAccountDisconnected` | The mailboxes on that account go `needs-reconnect`; data kept |
| Provider error (network, 5xx, quota) | `RecordFailure`, retried next cycle; `error` after 5 in a row, and a success clears it |
| Cursor expired | The mailbox is read again from the start, without events |
| An exception in the cycle (a failed database write, say) | Logged with its stack trace and counted like a provider error, with a fixed `last_error`: the details stay in the log |

## 4. Opening a message

`OpenMessageCommand`: a cached body is returned and its `last_accessed_at` touched. Otherwise the adapter
fetches the raw message, `MimeReader` (MimeKit) parses it, and the body is cached. On IMAP this also fills
the snippet, which IMAP does not provide.

**Attachments** come from parsing the raw message again; an attachment's id is its position among the
parts. The endpoint sends `nosniff`, and only PNG, JPEG, GIF, WebP and BMP go `inline`. Anything else (HTML,
SVG, PDF) downloads, since the part comes from whoever sent the mail.

## 5. Organizing

`ApplyMessageActionCommand`, under the action gate: up to 100 messages of one mailbox, all checked before the provider hears of them. The provider
goes first and the rows change after it accepts. The local change only anticipates what the next cycle reports, so a move with no new id leaves
the row to that cycle. Move targets are never Sent or Drafts; on Gmail, of the system labels, only the inbox.

Label operations (`LabelOperation`) call the provider under the action gate, release it, then run `MailboxChangesStep`. The provider has accepted by
then, so if the step fails the operation still succeeded: it is logged, and the next cycle catches up.

## 6. Sending

**Sending cannot be taken back,** so it differs from every other write:

- **Repeated only when the provider certainly refused:** a `429` or a rate-limit `403` on Gmail. A `5xx`, a timeout or a lost connection is `SendUncertain`: it may have gone, and the screen says to look in Sent.
- **A send key,** made when the form opens, is refused the second time (`SendKeys`, in process memory for an hour). Only a certain refusal frees it. It stops a double click, a second tab and a retry after a timeout.
- **A failed sent copy is a success:** the mail went, and an error would invite sending it again.
- **No gate and no sync step:** a slow SMTP cannot hold actions back, and a send does not wait for a cycle reading a long backlog. The sent copy shows up on the next cycle.
- **No sender name:** a mailbox's display name is the user's label for it, not who is writing.

`MessageComposer` builds the outgoing message and works out a reply or forward from the raw original: `Reply-To` wins over `From`, reply all leaves out the mailbox's own address, and a line break in a field cannot add a header. Gmail replaces the Message-ID on what it sends, so the key does not survive as the id there; the check is on the server, so that does not matter.

## 7. Frontend

- **`/mail` (`MailPage`):** `MailSidebar` (mailboxes, labels, unread counts), `MessageList` (pages of 50
  behind "Load more"), `MessageReader` (the conversation).
- **`/mail/accounts` (`MailAccountsPage`):** status, backfill progress, "Sync now", `ConnectMailboxModal`,
  `FixConnectionModal`. The list polls every 15 s while a backfill or a cycle runs.
- **`MailActions`** serves both the list's selection and the open message, offering only what applies. A selection across mailboxes becomes one
  request per mailbox, with no move or labels on offer. Nothing updates optimistically; a refusal leaves the selection as it was. Trash asks first.
- **Opening an unread message** marks it as read at the provider, once, after the body loads. A failure is silent.
- **`MailSidebar`** creates, renames and deletes labels. A renamed or deleted label that was open sends the list back to its mailbox.
- **`ComposeDrawer`** writes, replies and forwards: Markdown with a preview, attachments, a draft in the browser. A failure keeps the form; when the mail may have gone it will not send again unless asked, with a new key. `lib/composeBody` removes images, styles and forms from the HTML part, so a tracker in a quote cannot fire in the preview.
- **Connecting Gmail:** consent at Google with `gmail.modify` added to the default scopes, back to
  `?connect=gmail`, where the modal reopens with the account chosen and the user confirms.
- **Reconnecting:** Gmail goes through Google and returns to `?sync={id}`; Titan and IMAP open "Edit
  connection". Either way, `sync` runs next.

### Mail HTML, the security boundary

`lib/prepareMailHtml` handles what the API returns as received:

1. DOMPurify removes scripts and event handlers.
2. Remote `src`, `srcset`, `background` and CSS `url()` become a placeholder until "Show images".
3. `cid:` images become `data:` URLs. The attachment endpoint needs the Bearer token, and a `blob:` URL
   belongs to an origin the sandbox does not share.
4. Links open in a new tab.

The result renders in `<iframe sandbox="allow-popups allow-popups-to-escape-sandbox">` with a CSP:
`default-src 'none'; style-src 'unsafe-inline'; img-src data:`, widened to `data: https: http:` after "Show
images".

## 8. Key design decisions

| # | Decision | Rationale (rejected alternative) |
|---|---|---|
| **D1** | Cursors are opaque, IMAP's per-folder state included. | The engine is the same for both providers; per-folder columns in `com002` would leak IMAP into it. |
| **D2** | One raw-message path and one parser for both providers. | Parsing Gmail's JSON parts and IMAP's structure would be two parsers. The cost: a download fetches the whole message again. |
| **D3** | A Gmail REST client, not `Google.Apis.Gmail.v1`. | Integrations' `GoogleOAuthProvider` already works this way; no Newtonsoft, and a fake HTTP handler can test it. |
| **D4** | Removals on IMAP by snapshot, not QRESYNC. | Works on every server. The cost: a `SEARCH ALL` per folder per cycle. |
| **D5** | An expired cursor reads the whole mailbox again. | Rare; a state diff would be a second sync path. |
| **D6** | The content cache is bounded by age, not size. | Real numbers: see [Data Model](data-model.md#content-cache). |
| **D7** | Gmail through its API, not IMAP with an app password. | IMAP would show one row per Gmail label for each message and could not reach filters (M4). |
| **D8** | Every IMAP folder is in scope; subscriptions are ignored. | "Subscribed, or all when none is" flips the whole mirror when any client subscribes the first folder. It happened on Titan. |
| **D9** | Two gates, the backfill outside the action gate. | One gate: a click waits up to 5 minutes. A shorter budget: the backfill can take twice as long. |
| **D10** | Stitch a moved message by Message-ID. | "Removed + new" loses the cached body on every move and fired a false event. |
| **D11** | A send is repeated only on a certain refusal, with a key against duplicates. | Retrying like the other writes can send twice. |
| **D12** | The sent copy is detected, not configured. | A per-provider flag is something someone has to know and keep right. |
| **D13** | A Markdown text area, not a rich editor. | No new dependency, and plain text just works. Notes' editor is tied to pages and tags. |

## 9. Cross-cutting rules

- **Scoped to the user.** Every query goes through the caller's mailboxes; another user's mailbox,
  message or account answers 404, the same as one that does not exist.
- **`TimeProvider` everywhere** for time reads.
- **The outbox** is the module's own (`AddTarsOutbox`, one relay), since it publishes an event.
