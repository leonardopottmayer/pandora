# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is designed but not yet implemented. The forward roadmap
lives in [product-plan.md](product-plan.md).

---

## Implemented (M1 — connect and read; M2 — organize; M3 — send)

| Area | Notes |
|---|---|
| **Module scaffold** | Seven layered projects; `communications` schema; `com001`–`com005`; the module's outbox and relay. |
| **Domain** | `Mailbox`, `MailLabel`, `MailMessage`, `MailContent`; `MailProvider`, `MailboxStatus`, `LabelKind`, `LabelRole`, `ThreadKey`; `IMailboxProvider`; `MailErrors`. |
| **Gmail adapter** | REST without the Google SDK; `history.list` for changes; two parallel gets; retries with backoff on throttling. |
| **IMAP adapter** | MailKit, for Titan and any server; per-folder cursors; CONDSTORE when offered, the last 200 UIDs otherwise; removals by snapshot. |
| **Sync** | `SyncMailboxCommand` under a per-mailbox gate; `MailboxSyncBackgroundService` every 2 minutes; backfill in batches within a 5-minute budget per cycle; expired cursor → read again. |
| **Failures** | `needs-reconnect` with one notice for IMAP (`communications.mailbox-auth-failed`, email + Telegram, en + pt-BR); `error` after 5 failures; subscribers to `ExternalAccountRevoked` and `ExternalAccountDisconnected`. |
| **Reading** | `OpenMessage` with the content cache; 30-day retention; `MimeReader`; attachments on demand. |
| **Event** | `MailMessageReceived` through the outbox, without duplicates for moved messages. No consumer yet. |
| **Organizing (M2)** | `ApplyMessageAction` (read, star, move, archive, trash, labels), `CreateLabel`, `RenameLabel`, `DeleteLabel`; writes on both adapters; no permanent deletion. |
| **Sending (M3)** | `SendMessage`, `SendKeys`, `GetComposeDraft`, `MessageComposer`; SMTP with a detected sent copy; Gmail's multipart upload; no repeat on a doubt. Mail in Sent or Drafts is not an arrival. |
| **Sync, M2** | `MailboxGates` (two per mailbox), `MailboxChangesStep`, stitching moved messages by Message-ID, vanished labels removed after the changes. |
| **API** | `GET`/`POST /mailboxes`, `PATCH`/`DELETE /mailboxes/{id}`, `POST /mailboxes/{id}/sync`, `GET /mailboxes/{id}/labels`, `GET /messages`, `GET /messages/{id}`, `GET /messages/{id}/attachments/{attachmentId}`, `POST /messages/actions`, `POST /mailboxes/{id}/labels`, `PATCH`/`DELETE /labels/{id}`, `POST /messages/send`, `GET /messages/{id}/compose`. |
| **Integrations** | `CommunicationsAccountUsageSource`: the connections screen shows "Email · address" as a usage of the account. |
| **Frontend** | `/mail` and `/mail/accounts`; `prepareMailHtml` + sandboxed iframe + CSP; `MailActions`, selection in the list, the label menu in the sidebar; `ComposeDrawer`. |
| **Tests** | Unit: 89 (`MailboxTests`, `MessageComposerTests`, `ThreadKeyTests`, `LabelRoleTests`, `GmailMailboxProviderTests`). Integration: 66 (`SyncEngineTests`, `CommunicationsApiTests`, `MailActionsApiTests`, `SendMessageApiTests`, `ImapMailboxProviderTests` against GreenMail, `PersistenceTests`, `ProviderRegistrationTests`). Front: `prepareMailHtml.test.ts`, `MailPage.test.tsx`, `ComposeDrawer.test.tsx`, `composeBody.test.ts`. |

### Real-world check (2026-10-30)

Against one real Gmail (about 64,000 messages) and one real Titan mailbox (about 1,600):

| Check | Result |
|---|---|
| Both mailboxes connected, backfill finished, no errors | ✅ |
| Read in the webmail → read in Pandora | ✅ on both. **On Gmail it took more than one cycle**, a few minutes; the plan asked for one. The moment of the read was not recorded, so the delay's cause is unknown |
| A newsletter fires no remote request until "Show images" | ✅ |
| Headers arrive newest first | Covered by tests; not checked separately on the real accounts |
| Titan: CONDSTORE | ✅ Offered: the cursor holds `highestModSeq`, which is written only when the server has it |
| Titan: QRESYNC | Not checked; not used either, since removals go by snapshot |
| Titan: ManageSieve | ❌ Not offered (ports 4190 and 2000 closed), so Titan rules run in Pandora's engine |
| Titan: does SMTP save to Sent? | Not checked; Pandora does not send yet. Needed at the start of M3 |

The first real runs found two bugs the tests had not caught, both fixed: Gmail's throttling `403`s failed whole
batches, and a `Date` header with an offset could not be saved. GreenMail normalizes dates, so its test could not
catch the second; `SyncEngineTests` now covers it with an offset through the fake provider.

### Real-world check of M2 (2026-10-31)

On the same accounts, with disposable messages, by hand; the outbox and labels checked in the database:

| Check | Result |
|---|---|
| A label created in Pandora appears in Gmail and in Titan | ✅ |
| Opening, star, labels, move, archive and trash reach the provider | ✅ |
| A change in the webmail comes back to Pandora | ✅ |
| A folder renamed in the Titan webmail shows renamed, messages inside | ✅ |
| A move in the Titan webmail is not an arrival | ✅ No event came from a move or a rename |
| A folder with a message cannot be deleted; empty, it can | ✅ on Titan |
| Trash on Titan | ✅ after the fix below |
| Titan: `UIDPLUS` and `MOVE` | Not recorded: what they change, a move showing a cycle later, was not reported |

The check found one bug the tests had not caught: subscribing a folder created in Pandora flipped M1's rule of subscribed folders, so Trash, Sent and the
others dropped out of Pandora on Titan, though nothing changed at the provider. Every selectable folder is now in scope (decision D8 in
[Architecture](architecture.md)). The folders' return published 25 false `MailMessageReceived` (open point 6 below), with no effect, since nothing consumes them.

### Real-world check of M3 (2026-10-31)

On the same accounts, between the user's own addresses. Closed by the user; the database shows what is marked ✅:

| Check | Result |
|---|---|
| Titan to Gmail with an attachment, Gmail to Titan | ✅ Received once |
| Exactly one copy in Titan's Sent | ✅ One per send, checked in the mirror |
| A reply from Titan joins the Gmail conversation | ✅ Same thread |
| Forward from Gmail, no attachment | ✅ Received once |
| Sent mail is not an arrival | ✅ No event from a Sent copy |
| **A reply from Gmail** (`threadId`), the plan's own criterion | No such message in the mirror: **not shown** |
| **Forward with an attachment, Bcc, a double click** | No such messages in the mirror: **not shown** |
| Reply all, `Reply-To`, the layout | Not reported either way |
| A reply from Titan landed in Gmail's Spam | Seen once. The user judged it unrelated to Pandora; the cause was not recorded |

The check found a bug the tests had not caught: a send waited on the action gate while the job read a long Gmail backlog, with the mail already gone and the screen loading. It no longer runs the sync step.

### Notable deviations from the original plan

- **No `Google.Apis.Gmail.v1`.** REST with `HttpClient`, as `GoogleOAuthProvider` does.
- **No Gmail batch endpoint.** One `messages.get` per message, two at a time.
- **Only `gmail.modify` is requested.** The plan asked for `gmail.send` and `gmail.settings.basic` as well. Sending needs no more
  than `gmail.modify`; M4 will need a new consent for filters.
- **A send does not bring its sent copy at once.** It shows up on the next cycle, within 2 minutes.
- **A cycle reads backfill batches until a budget runs out**, not one batch per cycle: one batch per 2 minutes took
  over 3 hours on 50,000 messages.
- **`POST /mailboxes` does not start the backfill.** The first cycle does.
- **No per-folder columns in `com002`.** IMAP's state lives in the opaque `sync_cursor`.
- **The content cache has no size cap**, only age. See [Data Model](data-model.md#content-cache).
- **The message list is a plain list** behind "Load more", not a virtual `Table`.
- **`cid:` images are `data:` URLs**, not links to the API.

## Not yet implemented (designed / planned)

| Area | Status | Phase |
|---|---|---|
| **Rules, Gmail filters both ways, `com006`** | Planned | M4 |
| **Gmail push, Radar, more providers** | Future | Later |

## Known open points

1. **The job is sequential**, so a large mailbox's backfill delays every other mailbox by up to its 5-minute budget.
   Fine for one user. The first thing to change with several: a separate backfill queue with limited concurrency.
2. **Single instance.** The gate is in process; several instances would need a lease in the database.
3. **Testing mode on Google.** The Gmail refresh token expires after 7 days, so the mailbox asks to reconnect about
   once a week. Going past 100 users would need Google's verification of a restricted scope.
4. **Search** uses `ILIKE` with no index, and does not read bodies. Fine at one person's volume.
5. **Attachment download** fetches the whole raw message again.
6. **A folder that newly comes into scope with messages** arrives as new mail, with one false `MailMessageReceived` per message. Must be fixed before Radar consumes
   the event.
7. **A move straddling the IMAP walk**, from a folder not read yet to one already read, spans two cycles, so it is not stitched and can fire a false event.
8. **After a Gmail action during a backfill**, a stale batch can show the old state for up to one cycle.
9. **Only the loaded page can be selected**, and there is no undo; trash can be restored at the provider.
10. **The Gmail changes are saved only at the end.** After 9 hours stopped, a cycle read for over 20 minutes (about 44,000 history ids; the cause of so many was not found), holding the action gate, and a restart starts it over. Saving in parts is the fix.
11. **Send keys live in process memory,** so a retry right after a restart can send twice.
12. **A server that saves its sent copy more than 3 seconds later** gets two copies. Titan did not.
13. **The 25 MB limit is unconfirmed:** Google's API pages state none. A mail over the real limit comes back as a refusal.
14. **No sender name, no inline images, quoted text is plain.**
