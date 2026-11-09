# Implementation Status

[← Back to index](../README.md)

What is built, how it was checked against real accounts, and the known open points. What is left is in
[product-plan.md](product-plan.md).

---

## Implemented

All four phases are built: M1 connect and read, M2 organize, M3 send, M4 rules.

| Area | Notes |
|---|---|
| **Module scaffold** | Seven layered projects; `communications` schema; `com001`–`com006`; the module's outbox and relay. |
| **Domain** | `Mailbox`, `MailLabel`, `MailMessage`, `MailContent`, `MailRule`; `MailProvider`, `MailboxStatus`, `LabelKind`, `LabelRole`, `ThreadKey`; `IMailboxProvider`; `MailErrors`. |
| **Gmail adapter** | REST without the Google SDK; `history.list` for changes; two parallel gets; retries with backoff on throttling. |
| **IMAP adapter** | MailKit, for Titan and any server; per-folder cursors; CONDSTORE when offered, the last 200 UIDs otherwise; removals by snapshot. |
| **Sync** | `SyncMailboxCommand` under per-mailbox gates (`MailboxGates`, two per mailbox); `MailboxSyncBackgroundService` every 2 minutes; backfill in batches within a 5-minute budget per cycle; an expired cursor reads again; messages moved in the webmail are stitched by Message-ID. |
| **Failures** | `needs-reconnect` with one notice for IMAP (`communications.mailbox-auth-failed`, email + Telegram, en + pt-BR); `error` after 5 failures; subscribers to `ExternalAccountRevoked` and `ExternalAccountDisconnected`. |
| **Reading** | `OpenMessage` with the content cache; 30-day retention; `MimeReader`; attachments on demand. |
| **Event** | `MailMessageReceived` through the outbox, never for a moved message or a Sent/Drafts copy. Pandora's rule engine consumes it. |
| **Organizing** | `ApplyMessageAction` (read, star, move, archive, trash, labels), `CreateLabel`, `RenameLabel`, `DeleteLabel`; writes on both adapters; no permanent deletion. |
| **Sending** | `SendMessage`, `SendKeys`, `GetComposeDraft`, `MessageComposer`; SMTP with a detected sent copy; Gmail's multipart upload; no repeat on a doubt. |
| **Rules** | `com006`, `mail/rules`. On Gmail a rule is a filter, both ways: every cycle lists the filters and brings in what is new, gone or changed. Elsewhere `RunMailRulesHandler` runs them on `MailMessageReceived`. A filter that does not fit the model is read-only, never rewritten. |
| **Save to Notes** | "Save to Notes" in the reader copies the message's text into a new page through `INoteImporter` (Notes.Abstractions). Plain text is preferred; HTML-only mail is reduced to text by MimeKit's tokenizer. Attachments and formatting are not copied, and the page does not follow later changes to the mail. |
| **API** | See [API Reference](api-reference.md). |
| **Integrations** | `CommunicationsAccountUsageSource`: the connections screen shows "Email · address" as a usage of the account. |
| **Frontend** | `/mail`, `/mail/accounts` and `/mail/rules`; `prepareMailHtml` + sandboxed iframe + CSP; `MailActions`, selection in the list, the label menu in the sidebar; `ComposeDrawer`; the open message lives in the URL (`/mail?message={id}`). |
| **Tests** | Unit: `MailboxTests`, `MessageComposerTests`, `ThreadKeyTests`, `LabelRoleTests`, `GmailMailboxProviderTests` and the rule tests. Integration: `SyncEngineTests`, `CommunicationsApiTests`, `MailActionsApiTests`, `SendMessageApiTests`, `ImapMailboxProviderTests` against GreenMail, `PersistenceTests`, `ProviderRegistrationTests`. Front: `prepareMailHtml.test.ts`, `MailPage.test.tsx`, `ComposeDrawer.test.tsx`, `composeBody.test.ts`. |

### Checked against real accounts

Every phase was checked by hand against a real Gmail (about 64,000 messages) and a real Titan mailbox
(about 1,600), between 2026-10-30 and 2026-11-01. What those checks established and still matters:

- **Titan** offers CONDSTORE but not ManageSieve, so Titan rules run in Pandora's engine. Its SMTP saves
  exactly one copy in Sent.
- **Gmail** can take more than one cycle to show a change made in its webmail. An edit to a filter can
  take minutes to reach `filters.list`.
- **Gmail's own screens keep a filter's id when editing it**, which its documentation does not say. The
  rule diff therefore compares what a known filter does, not only its id.
- Filters that use "never send to spam" (`removeLabelIds: SPAM`) do not fit the rule model and show
  read-only.
- Never shown on a real account: a reply from Gmail joining its thread, a forward with an attachment, Bcc,
  and a double click on Send.

### Decisions that differ from the first design

- **No `Google.Apis.Gmail.v1`.** REST with `HttpClient`, as `GoogleOAuthProvider` does.
- **No Gmail batch endpoint.** One `messages.get` per message, two at a time.
- **Only `gmail.modify` is requested up front.** Sending needs no more. Writing filters needs
  `gmail.settings.basic`, asked for only when a rule is saved.
- **A send does not bring its sent copy at once.** It shows up on the next cycle, within 2 minutes.
- **A cycle reads backfill batches until a budget runs out**, not one batch per cycle: one batch per 2
  minutes took over 3 hours on 50,000 messages.
- **`POST /mailboxes` does not start the backfill.** The first cycle does.
- **No per-folder columns in `com002`.** IMAP's state lives in the opaque `sync_cursor`.
- **The content cache has no size cap**, only age. See [Data Model](data-model.md#content-cache).
- **The message list is a plain list** behind "Load more", not a virtual `Table`.
- **`cid:` images are `data:` URLs**, not links to the API.

## Not yet implemented

| Area | Status |
|---|---|
| **Gmail push, Radar consuming the event, more providers** | Later; see [product-plan.md](product-plan.md) |

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
9. **Send keys live in process memory,** so a retry right after a restart can send twice.
10. **A server that saves its sent copy more than 3 seconds later** gets two copies. Titan does not.
11. **The 25 MB limit is unconfirmed:** Google's API pages state none. A mail over the real limit comes back as a refusal.
12. **No sender name, no inline images, quoted text is plain.**
13. **Rules' errors are in English,** like the server's other messages, and a rule whose label is gone reads "Move to ?" until it is edited.
14. **Rules read their conditions and actions back as Gmail gives them,** so a rule saved as archive + add label comes back as a move. They mean the same.
