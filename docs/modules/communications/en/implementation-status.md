# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is designed but not yet implemented. The forward roadmap
lives in [product-plan.md](product-plan.md).

---

## Implemented (M1 — connect and read)

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
| **API** | `GET`/`POST /mailboxes`, `PATCH`/`DELETE /mailboxes/{id}`, `POST /mailboxes/{id}/sync`, `GET /mailboxes/{id}/labels`, `GET /messages`, `GET /messages/{id}`, `GET /messages/{id}/attachments/{attachmentId}`. |
| **Integrations** | `CommunicationsAccountUsageSource`: the connections screen shows "Email · address" as a usage of the account. |
| **Frontend** | `/mail` and `/mail/accounts`; `prepareMailHtml` + sandboxed iframe + CSP. |
| **Tests** | Unit: 47 (`MailboxTests`, `ThreadKeyTests`, `LabelRoleTests`, `GmailMailboxProviderTests`). Integration: 22 (`SyncEngineTests`, `CommunicationsApiTests`, `ImapMailboxProviderTests` against GreenMail, `PersistenceTests`, `ProviderRegistrationTests`). Front: `prepareMailHtml.test.ts`, `MailPage.test.tsx`. |

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

### Notable deviations from the original plan

- **No `Google.Apis.Gmail.v1`.** REST with `HttpClient`, as `GoogleOAuthProvider` does.
- **No Gmail batch endpoint.** One `messages.get` per message, two at a time.
- **Only `gmail.modify` is requested.** The plan asked for `gmail.send` and `gmail.settings.basic` as well; M3 and
  M4 will need a new consent.
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
| **Labels both ways, batch actions** | Planned | M2 |
| **Compose, reply, forward, attachments** | Planned | M3 |
| **Rules, Gmail filters both ways, `com006`** | Planned | M4 |
| **Gmail push, Radar, more providers** | Future | Later |

## Known open points

1. **The job is sequential**, so a large mailbox's backfill delays every other mailbox by up to its 5-minute budget.
   Fine for one user. The first thing to change with several: a separate backfill queue with limited concurrency.
2. **Single instance.** The gate is in process; several instances would need a lease in the database.
3. **An exception out of a cycle is not recorded on the mailbox.** Only `Result` failures reach `RecordFailure`. A
   thrown exception (a failed write, as the date bug was) is logged by the job, and the screen keeps showing
   "syncing" while the same batch fails each cycle.
4. **Testing mode on Google.** The Gmail refresh token expires after 7 days, so the mailbox asks to reconnect about
   once a week. Going past 100 users would need Google's verification of a restricted scope.
5. **Search** uses `ILIKE` with no index, and does not read bodies. Fine at one person's volume.
6. **Attachment download** fetches the whole raw message again.
7. **The migration comment on `com003`** still describes the IMAP id as `{label_id}:{uid}`; the code and this
   documentation are right. Only the comment is out of date; the column is unaffected.
