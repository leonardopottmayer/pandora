# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is designed but not yet implemented. The forward roadmap
lives in [product-plan.md](product-plan.md).

---

## Implemented (M1 — connect and read; M2 — organize; M3 — send; M4 — rules)

| Area | Notes |
|---|---|
| **Rules (M4)** | `com006`, `mail/rules`. On Gmail a rule is a filter, both ways: every cycle lists them and brings in what is new, gone or changed. Elsewhere `RunMailRulesHandler` runs them on `MailMessageReceived`. A filter that does not fit is read-only, never rewritten |
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
| **Tests** | Unit: 91 (`MailboxTests`, `MessageComposerTests`, `ThreadKeyTests`, `LabelRoleTests`, `GmailMailboxProviderTests`). Integration: 72 (`SyncEngineTests`, `CommunicationsApiTests`, `MailActionsApiTests`, `SendMessageApiTests`, `ImapMailboxProviderTests` against GreenMail, `PersistenceTests`, `ProviderRegistrationTests`). Front: `prepareMailHtml.test.ts`, `MailPage.test.tsx`, `ComposeDrawer.test.tsx`, `composeBody.test.ts`. |

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
[Architecture](architecture.md)). The folders' return published 25 false `MailMessageReceived`, with no effect, since nothing consumed them; fixed in M4.

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

### Real-world check of M4 (2026-11-01)

On the same accounts. Only a `pandora-test` rule, label and folder were written, and all of them were removed afterwards; no existing filter was edited or deleted.

| Check | Result |
|---|---|
| Existing filters show up as rules | ✅ 112: 102 editable, 10 read-only. Those 10 use "never send to spam" (`removeLabelIds: SPAM`), which a rule does not hold |
| `Label_` as the mark of a user label | ✅ As the documentation implied |
| Saving without the scope, then granting | ✅ The prompt appears, consent is granted, the form comes back filled in |
| A rule becomes a Gmail filter | ✅ A cycle afterwards there is still one rule |
| An edit in Pandora | ✅ Create + delete; no old filter came back as a rule |
| A message gets the label from the filter | ✅ Seen in Gmail. **Not with the backend stopped:** it was running and the mail was sent through Pandora. The log shows the only other Gmail write was marking it read, and the engine does not run filter rules, so the label is Gmail's |
| A filter edited in Gmail | ✅ After a fix, below: same rule, same name and position. **Gmail's API took minutes to show it:** on the second try, `filters.list` still gave the old subject at 23:57, and the new one at 23:59, at least 9 minutes after the user reported the edit |
| Disable, enable, delete in Pandora | ✅ A cycle after each shows the filter gone or back. Done through the API, not the screen, so as not to risk a real rule |
| Titan: a new message moves, an earlier one stays | ✅ Sent between the mailbox and itself |
| Titan: the folder is deleted | ✅ The rule goes `broken`, highlighted |
| Dragging | ✅ Moved a rule, one row above the aim. May be the tool; **worth one check by hand** |
| The confirmations for a Gmail rule, on screen | Only in tests; on screen, the one for a Titan rule |

The check found two things the tests had not caught. **Gmail's own screens keep a filter's id when editing it**, which the documentation does not say and the plan assumed otherwise, so the edit never arrived: the diff now compares what a known filter does too. With about 100 labels, the form's list needed a search. And the screen never reloaded the rules by itself: it now does every 30 seconds and on focus.

### Notable deviations from the original plan

- **No `Google.Apis.Gmail.v1`.** REST with `HttpClient`, as `GoogleOAuthProvider` does.
- **No Gmail batch endpoint.** One `messages.get` per message, two at a time.
- **Only `gmail.modify` is requested.** The plan asked for `gmail.send` and `gmail.settings.basic` as well. Sending needs no more
  than `gmail.modify`. Writing filters needs `gmail.settings.basic`, asked for only when a rule is saved.
- **An edit in Gmail can keep the filter's id.** The plan expected one gone + one new, which is still handled, for what recreates a filter.
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
| **Gmail push, Radar, more providers** | Future | Later |

## Known open points

1. **The job is sequential**, so a large mailbox's backfill delays every other mailbox by up to its 5-minute budget.
   Fine for one user. The first thing to change with several: a separate backfill queue with limited concurrency.
2. **Single instance.** The gate is in process; several instances would need a lease in the database.
3. **Testing mode on Google.** The Gmail refresh token expires after 7 days, so the mailbox asks to reconnect about
   once a week. Going past 100 users would need Google's verification of a restricted scope.
4. **Search** uses `ILIKE` with no index, and does not read bodies. Fine at one person's volume.
5. **Attachment download** fetches the whole raw message again.
6. **A move straddling the IMAP walk**, from a folder not read yet to one already read, spans two cycles, so it is not stitched and can fire a false event.
7. **After a Gmail action during a backfill**, a stale batch can show the old state for up to one cycle.
8. **Only the loaded page can be selected**, and there is no undo; trash can be restored at the provider.
9. **Why one backlog was so long is unknown.** After 9 hours stopped, a cycle read for over 20 minutes, about 44,000 history ids. It is now saved in parts, and the log says how many messages each part read.
10. **Send keys live in process memory,** so a retry right after a restart can send twice.
11. **A server that saves its sent copy more than 3 seconds later** gets two copies. Titan did not.
12. **The 25 MB limit is unconfirmed:** Google's API pages state none. A mail over the real limit comes back as a refusal.
13. **No sender name, no inline images, quoted text is plain.**
14. **Rules' errors are in English,** like the server's other messages, and a rule whose label is gone reads "Move to ?" until it is edited.
15. **Rules read their conditions and actions back as Gmail gives them,** so a rule saved as archive + add label comes back as a move. They mean the same.
16. **An edit in Gmail's screens can take minutes to reach `filters.list`,** on top of the 2-minute cycle. Pandora shows what the API answers. One measure, not a figure to rely on.
