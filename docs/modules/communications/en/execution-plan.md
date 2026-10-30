# Communications — Execution Plan, M1 (Connect and read)

> **Status:** Execution plan for phase **M1** of the [product-plan](product-plan.md). Nothing built yet.
> Step 1 was done by Integrations [phase I5](../../integrations/en/product-plan.md) (multiple
> accounts): the `password` kind, `GetPasswordAsync`, lookups by account id, the `GetProviders` fix
> and the `titan`/`imap` descriptors. The mailbox screens pick the account with I5's
> `AccountSelect`, and Communications implements `IExternalAccountUsageSource`
> (mailbox → "Email · <address>").
> 🇧🇷 [Versão em português](../pt-BR/execution-plan.md)

This doc is the work list. The *why* behind each decision is in the [product-plan](product-plan.md);
section numbers (§) point there.

**M1 delivers:** connect a Gmail and a Titan mailbox, mirror their headers and labels (read only),
read messages with bodies on demand, and publish `MailMessageReceived`. No organizing (M2), no
sending (M3), no rules (M4).

---

## What already exists (do not rebuild)

| Piece | Where | What M1 uses |
|---|---|---|
| Communications template | `backend/src/Modules/Communications/*` (7 projects, wired in `Program.cs`) | `CommunicationsModule` (name/key/schema), `CommunicationsOptions` (`Pandora:Communications`), an empty `CommunicationsDbContext` |
| Credentials | Integrations: `ExternalAccount`, `IExternalCredentialProvider`, `ISecretProtector` | access token for Gmail; the new password path follows `SaveApiKey` step by step |
| Incremental Google consent | `GoogleOAuthProvider` already sends `include_granted_scopes=true`; `POST integrations/{provider}/connect` accepts `scopes` | the Gmail scopes are requested **by the mail screen**, not added to the defaults — Agenda-only users never grant Gmail |
| Outbox | `Host/OutboxRegistration.cs`, `modelBuilder.AddTarsOutbox(schema)` | one more relay + contracts assembly |
| Notifications | `NotifyUserRequested` + `Channels.Infrastructure/Templates/TemplateCatalog.cs` | the IMAP auth-failure notice |
| Background jobs | `Finances.Infrastructure/Jobs/*BackgroundService.cs` | the pattern for `MailboxSyncBackgroundService` |
| MIME | MailKit is already used through `Tars.Communication.Email.MailKit` (system sender) | Communications references MailKit directly (per-mailbox credentials) |
| Front | `dompurify` installed; antd v6 `Table` has `virtual` | no new front dependency |

---

## Locked decisions

| Decision | Value | Reason |
|---|---|---|
| Gmail scopes | requested on "Connect Gmail" via `connect` with `scopes = [openid, email, gmail.modify, gmail.send, gmail.settings.basic]` | incremental consent keeps the defaults untouched; all three Gmail scopes now so M3/M4 need no second consent |
| Password credential | `AuthKind.Password` (`password`), `provider_account_id` = username, secret in `access_token_enc` | §5; no new column |
| Several mailboxes per provider | password accounts are looked up **by account id**, never by (user, provider) | two Titan mailboxes on two domains is the realistic case |
| Several Google accounts | each Gmail mailbox stores its Google account id and asks for its token by that id | Integrations I5 lifted the one-account limit |
| Body and attachments | the adapter fetches the **raw RFC 822** message (Gmail `format=raw`, IMAP `BODY.PEEK[]`) and MimeKit parses it — one parser for both providers | an attachment download re-fetches the raw message (`ponytail:` ceiling: huge messages; upgrade to Gmail `attachments.get` / IMAP `BODY[part]`) |
| Content cache | age-based: rows not accessed in `ContentCacheRetentionDays` (default 30) are deleted by the sync job | closes open question 2 of the product-plan with the simplest rule |
| IMAP message key | `provider_message_id` built by the adapter from the folder path, `UIDVALIDITY` and UID | UIDs are per folder; a `UIDVALIDITY` change resyncs the folder and drops its rows |
| IMAP snippet | empty until the message is opened, then filled from the parsed text | IMAP has no snippet; a partial body fetch per message is not worth it in M1 |
| IMAP folders | subscribed folders (`LIST SUBSCRIBED`); all selectable folders when none is subscribed | §7 |
| Sync loop | one `BackgroundService`, mailboxes in sequence, every `SyncInterval` (default 2 min); each cycle = **incremental, then one backfill batch** | personal scale; `ponytail:` sequential loop, parallelize per mailbox if it falls behind |
| "Sync now" | runs one cycle inside the request, under the mailbox gate | no signalling between request and job |
| Search | `ILIKE` on from/subject/snippet, within the user's mailboxes | `ponytail:` add `pg_trgm` when it is slow |
| Pagination | keyset on `(sent_at, id)` descending | stable while new mail arrives |

---

## Steps

Each step ends green (build + its tests) before the next one. Suggested commit points, mirroring Files'
F1a/F1b/F1c: **M1a** = steps 1–2, **M1b** = steps 3–7, **M1c** = steps 8–9. Commits only when asked.

### Step 1 — Integrations: the `password` credential

- `AuthKind.Password = new("password")` + `FromValue`.
- `ExternalAccount.ConnectPassword(userId, provider, username, passwordEnc, tp)` (display name = the
  username) and `ReplacePassword(passwordEnc, tp)`.
- `int001` `CREATE TABLE`: `chk_int001_auth_kind` gains `'password'` (edited in place, pre-1.0).
- `IExternalAccountRepository.FindAsync(userId, provider, providerAccountId)`.
- `PasswordProviderDescriptor(Name, DisplayName)` + its registry; `AddIntegrationsPasswordProviders()`
  registers `titan` ("Titan Email") and `imap` ("IMAP").
- `SavePasswordCommand` — upsert by (user, provider, username), protect, event log
  `connected`/`reconnected`; returns the account id. Endpoint `PUT integrations/{provider}/password`,
  body `{ username, password }`, response `{ externalAccountId }`.
- `IExternalCredentialProvider.GetPasswordAsync(userId, externalAccountId)` →
  `Result<PasswordCredential(Username, Password)>`; fails when the account is not the user's or not
  `password`.
- `GetProvidersQueryHandler`: `ToDictionary(a => a.Provider)` throws with two accounts on one
  provider — switch to a lookup ("connected" = any account). Catalog lists the password providers with
  `authKind = password`.
- `ConnectionsPage`: a `password` account renders like an api-key one (status + disconnect); no form
  there — Titan is added from the mail screen.
- **Tests:** domain (connect/replace password); integration `PasswordTests` mirroring `ApiKeyTests`
  (save → listed, no plaintext in the account DTO, upsert per username, two usernames → two accounts,
  unknown provider → 422, empty password → 422).

### Step 2 — Communications: schema, persistence, outbox ✅

> **Built (2026-10-27).** Beyond the list below: `com001` also has `backfill_completed_at` and
> `uq (user_id, external_account_id)` (one mailbox per credential); `com003` splits the sender into `from_name`/`from_address` and indexes
> `(mailbox_id, thread_key)` for conversations. The Communications.Contracts assembly joins the
> event-type registry in step 6, with its first event. Test: `PersistenceTests` (round trip through
> the repositories).


- Migrations under `migrations/migrations/communications/` (one folder per file, `.up.sql` +
  `.down.sql`, same naming as `integrations/`): `create-schema-communications`, `com001_mailbox`,
  `com002_mail_label`, `com003_mail_message`, `com004_mail_message_label`, `com005_mail_content`,
  `tars-outbox-message`. `com006_mail_rule` waits for M4.
  - Indexes: `uq (mailbox_id, provider_message_id)`; `(mailbox_id, sent_at desc, id desc)`;
    `(mailbox_id, rfc822_message_id)`; `uq (mailbox_id, provider_label_id)`; `com004 (label_id, message_id)`;
    `com005 (last_accessed_at)`.
  - Checks for every enum column, with hyphenated values (`needs-reconnect`).
  - `com001` adds `message_total_estimate` (for the backfill progress bar) to the product-plan's
    columns.
- Entities + EF configurations + repositories (Tars `IStandardRepository`); `modelBuilder.AddTarsOutbox(schema: CommunicationsModule.Schema)`.
- Wiring: `Tars:Data:Connections:communications` in `appsettings.json`, `docker-compose.yml` and the
  test factory (connection + `communications` in Respawn's `SchemasToInclude`);
  `OutboxRegistration`: `AddTarsOutboxRelay(CommunicationsModule.DatabaseKey)` and the
  Communications.Contracts assembly in the event-type registry.
- **Tests:** the integration suite boots with the new schema (existing tests stay green).

### Step 3 — Domain and the provider port ✅

> **Built (2026-10-27).** Changes from the list below:
> - **Cursors are opaque, IMAP included.** The IMAP per-folder state (UIDVALIDITY, UIDNEXT, MODSEQ)
>   lives inside the mailbox's `sync_cursor`, which only the IMAP adapter reads, so the sync engine is
>   the same for both providers. `com002` lost `uid_validity`/`highest_modseq`.
> - **No `security` column.** The port decides it (993/465 TLS, 143/587 STARTTLS, MailKit's
>   `SecureSocketOptions.Auto`): one value for both servers was wrong for the common 993 + 587 pair.
> - **No resolver.** The adapters are keyed services under each provider value (the IMAP one under
>   `titan` and `imap`); the engine asks for the key.
> - **Removals without QRESYNC** come as `RemoteLabelSnapshot`s (every message a label has now), not as
>   an id-prefix diff. A history the provider no longer knows is the `SyncCursorExpired` error.
> - `Mailbox.StartBackfill(syncCursor, backfillCursor, total, now)` also starts over; `AdvanceBackfill(null)`
>   completes it (no separate `CompleteBackfill`); `Reconnect()` leaves `needs-reconnect`.
> - Tests: new `Pottmayer.Pandora.Modules.Communications.Tests` (35: mailbox, thread key, label roles).


- `MailProvider` value (`gmail`/`titan`/`imap`; `titan` carries the preset hosts
  `imap.titan.email:993`, `smtp.titan.email:465`, SSL).
- `Mailbox` aggregate: `Connect…`, `StartBackfill(total)`, `AdvanceBackfill(cursor)`,
  `CompleteBackfill`, `SetSyncCursor`, `RecordSuccess`, `RecordFailure(error)` (→ `error` at 5
  consecutive), `MarkNeedsReconnect`. `MailLabel`, `MailMessage` (+ label links), `MailContent`.
- `IMailboxProvider` (Domain/Ports), M1 subset: `GetCapabilities`, `TestConnection`, `ListLabels`,
  `StartBackfill` (returns the incremental cursor *at connect time* + total), `FetchBackfillBatch`,
  `FetchChanges(cursor)`, `GetRawMessage`. Results are provider-neutral records (`RemoteLabel`,
  `RemoteMessage` with the full label set and flags, `RemoteChanges` = upserts + removed ids + new
  cursor, or `CursorExpired`).
- `IMailboxProviderResolver` picks the adapter by `MailProvider` (keyed DI; `titan` and `imap` → IMAP).
- `ThreadKey.From(messageId, references)` for IMAP.
- **Tests:** mailbox state transitions (failure counter, needs-reconnect), thread key, label role
  mapping helpers.

### Step 4 — IMAP adapter (MailKit) ✅

> **Built (2026-10-27)** — `Infrastructure/Imap/ImapMailboxProvider` (keyed under `titan` and `imap`),
> MailKit 4.18.2. Changes from the list below:
> - **Message id** = `"{uidvalidity}:{uid}:{folder path}"` (the path last, since it may contain `:`).
> - **Removals always by snapshot:** every cycle sends each folder's UID set (`UID SEARCH ALL`); no
>   QRESYNC/`VANISHED` path yet (`ponytail:` add it if large folders make the search slow).
> - **A changed UIDVALIDITY** fails with `SyncCursorExpired` instead of resyncing only that folder.
>   Step 6 handles it by dropping the mailbox's messages and starting the backfill again (rare event).
> - **A folder that appears after the backfill** (created, or subscribed) arrives whole as upserts; a
>   message moved there keeps its Message-ID, so step 6's duplicate check publishes no event for it.
> - The backfill reads one folder per batch, INBOX first. The CONDSTORE branch is written but not
>   covered (GreenMail has no CONDSTORE); step 9 checks it on Titan.
> - Tests: `ImapMailboxProviderTests` (2) against `greenmail/standalone:2.1.0`: wrong password → auth
>   failure; backfill in batches newest first, thread key, a new arrival, read in the webmail, raw fetch
>   without marking as read, moved to a new folder, deleted.


- Connect with the mailbox's host/port + `GetPasswordAsync`; `AuthenticationException` maps to
  a typed `MailboxAuthFailed` error (drives `needs-reconnect`).
- Labels from folders: SPECIAL-USE flags (`\Sent`, `\Trash`, `\Junk`, `\Drafts`, `\Archive`) → `role`;
  `INBOX` → `inbox`; all `is_exclusive = true`.
- Per folder at backfill start: record `UIDVALIDITY` and `UIDNEXT` (the boundary) in the sync cursor. Incremental:
  `UID ≥ boundary` = new; `CHANGEDSINCE highest_modseq` when CONDSTORE is advertised, else re-read
  flags of the last N UIDs (`ponytail:` window, full flag diff if it misses changes); removals by
  `VANISHED` (QRESYNC) or a UID-set diff. Backfill: UIDs below the boundary, descending, batches of
  500, fetching `ENVELOPE FLAGS BODYSTRUCTURE RFC822.SIZE` + `References`.
- `GetRawMessage` = `BODY.PEEK[]` (never marks as read).
- **Tests:** integration against **GreenMail** (Testcontainers): seed messages over SMTP, connect,
  backfill, deliver one more → seen as new, flag `\Seen` from another client → seen as read, delete →
  removed. Covers the non-CONDSTORE path.

### Step 5 — Gmail adapter ✅

> **Built (2026-10-27)** — `Infrastructure/Gmail/GmailMailboxProvider` (keyed under `gmail`). Changes from
> the list below:
> - **No SDK.** The Gmail REST API with `HttpClient` + `System.Text.Json`, as Integrations'
>   `GoogleOAuthProvider` already does: no `Google.Apis.*` + Newtonsoft, and the adapter is testable
>   with a fake HTTP handler.
> - **No batch request.** One `messages.get` per message, 4 at a time, under the per-user quota
>   (`ponytail:` Gmail's batch endpoint if the backfill is too slow).
> - `UNREAD`/`STARRED` are flags, not labels; every other label id (system, `CATEGORY_*`, user) is a
>   label. The backfill includes spam and trash. `sent_at` is Gmail's `internalDate`.
> - **Scopes:** the token must carry `gmail.modify` (or `https://mail.google.com/`), checked before any
>   call; a 403 for scopes is also `MissingScopes`; a 401 is `MailboxAuthFailed`.
> - Tests: 11 unit tests on canned Gmail answers (`GmailMailboxProviderTests`) + `ProviderRegistrationTests`
>   (every provider value resolves an adapter). The real account is still step 9.


- `Google.Apis.Gmail.v1`, `GoogleCredential.FromAccessToken(GetAccessTokenAsync(userId, mailbox.ExternalAccountId))`.
  Rejects a Google account whose scopes lack `gmail.modify` with `MissingScopes` (the UI asks to
  reconnect).
- `ListLabels` = `labels.list` (system labels map to roles; `is_exclusive = false`).
- `StartBackfill` = `users.getProfile` → `historyId` (cursor) + `messagesTotal`. Backfill =
  `messages.list` pages (newest first) + `messages.get(format=metadata)` for headers
  `From, To, Cc, Subject, Date, Message-ID, References`, in a **batch request** of 100.
  `has_attachments` = top-level `multipart/mixed`.
- Incremental = `history.list(startHistoryId)`; every touched id is re-read with `format=metadata`
  (full state → upsert; a 404 on get = removed). History 404 → `CursorExpired` → the engine runs a
  state resync (all ids, diff).
- `GetRawMessage` = `messages.get(format=raw)`.
- **Tests:** none automated (no reliable fake Gmail) — manual against the real account, step 9.

> **Changed (2026-10-29)** — the first real backfill (≈64k messages) failed every batch on a few
> `403`s Gmail sends when throttling, and nothing was saved. Each Gmail request is now retried after
> 1, 2, 4, 8 and 16 s (or the server's `Retry-After`) on `429`, `5xx` and a `403` whose reason is
> `rateLimitExceeded`/`userRateLimitExceeded`; an error that persists carries Gmail's reason in the
> mailbox's last error, and `ParallelGets` went from 4 to 2. Test:
> `A_throttled_request_is_retried_and_a_lasting_one_names_the_reason`.

### Step 6 — Sync engine ✅

> **Built (2026-10-27)** — `SyncMailboxCommand`, `MailboxSyncBackgroundService`, the two account
> subscribers, `MailMessageReceived` (Contracts, in the event-type registry) and
> `CommunicationsAccountUsageSource` ("Email · <address>" on the connections screen). Details and
> changes from the list below:
> - **`SyncCursorExpired`** (Gmail history gone, IMAP UIDVALIDITY changed) deletes the mailbox's
>   messages and starts the backfill again: no events, the cached bodies go with the rows.
> - **Who is told.** A refused IMAP/Titan password → `needs-reconnect` and one notice
>   (`communications.mailbox-auth-failed`, email + Telegram, en + pt-BR), only on the change. Gmail
>   auth failures and `MissingScopes` → `needs-reconnect` without a notice (Integrations already warns
>   on a revoked grant; the screen shows the missing scopes).
> - **A label the provider stopped listing** is deleted; on IMAP (an exclusive label, i.e. a folder)
>   its messages go with it, on Gmail they only lose the label.
> - **Content-cache retention** runs once per job cycle for every mailbox (one indexed delete), not
>   inside each mailbox's command.
> - Messages left with no label by a snapshot are deleted in bulk: EF cannot delete a tracked entity
>   whose JSON collections (`to_recipients`) it tracks.
> - `Mailbox.Reconnect()` is called by step 7's `sync` and `PATCH` once the provider accepts the
>   credential again.
> - Tests: `SyncEngineTests` (10) with a scripted `FakeMailProvider` under `titan`: backfill without
>   events, one event per arrival, flags and removals, snapshot removal, same Message-ID → no event,
>   expired cursor → re-read without events, refused password → needs-reconnect + one notice,
>   transient failure counted then cleared, revoked account → needs-reconnect, usage on the accounts
>   API.


- `SyncMailboxCommand(mailboxId)` (Application), under a per-mailbox `SemaphoreSlim` gate (same
  remark as `ExternalCredentialProvider`): labels diff → incremental changes → one backfill batch →
  content cache retention. Provider calls happen outside transactions; each apply is one unit of work.
- `MailMessageReceived(EventId, OccurredAt, UserId, MailboxId, MessageId, From, Subject, Labels)` in
  Communications.Contracts, published **only from the incremental path** and only when the mailbox has
  no row with the same `rfc822_message_id` (product-plan open question 1).
- Failures: `MailboxAuthFailed` → `needs-reconnect` and, for IMAP, `NotifyUserRequested`
  (`communications.mailbox`, template `communications.mailbox-auth-failed`, new entry in
  `TemplateCatalog`, en + pt-BR); transient → `RecordFailure`.
- `MailboxSyncBackgroundService` (Infrastructure/Jobs): every `SyncInterval`, each `active`/`syncing`/`error`
  mailbox in sequence; skips `needs-reconnect`.
- Subscribers: `ExternalAccountRevoked` and `ExternalAccountDisconnected` → the mailboxes on that
  account go `needs-reconnect` (data kept).
- `CommunicationsOptions`: `SyncInterval`, `BackfillBatchSize`, `BackfillBudget`, `ContentCacheRetentionDays`.
- **Tests:** integration with a fake `IMailboxProvider` registered in the factory: backfill fills rows
  without events; an incremental arrival publishes one event; a re-appearing `rfc822_message_id`
  publishes none; auth failure → `needs-reconnect` + one `NotifyUserRequested`; revoked event →
  `needs-reconnect`.

> **Changed (2026-10-29)** — one cycle no longer reads a single backfill batch: it reads batches back
> to back until the backfill ends or `BackfillBudget` runs out (at least one batch). The default went
> from 1 to 5 minutes after the real Gmail test: longer than the 2-minute interval, so a backfill runs
> almost all the time instead of half of it.
> With one batch per 2-minute cycle a 50,000-message mailbox took over 3 hours. Test:
> `One_cycle_reads_backfill_batches_until_the_backfill_ends` (batches of 2, five messages, one cycle).

### Step 7 — API

`/api/v1/communications`, `[Authorize]`, every query scoped to the caller's mailboxes:

- `GET mailboxes` (status, last error, backfill progress = rows / `message_total_estimate`).
- `POST mailboxes` `{ provider, externalAccountId, address?, displayName?, imapHost?, imapPort?, smtpHost?, smtpPort? }`
  — validates the account is the caller's and of the right kind, runs `TestConnection`, then creates
  the mailbox and starts the backfill. A failed test → 422 with the provider's message.
- `PATCH mailboxes/{id}` (display name, server settings), `DELETE mailboxes/{id}` (local data only;
  the credential stays in Integrations).
- `POST mailboxes/{id}/sync` (one cycle, returns the mailbox).
- `GET mailboxes/{id}/labels` (with unread counts).
- `GET messages?mailboxId&labelId&unread&q&threadKey&before&limit` (keyset).
- `GET messages/{id}` — metadata + content (fetched and cached on first open; updates `last_accessed_at`).
- `GET messages/{id}/attachments/{attachmentId}` — streamed, `Content-Disposition` from the part;
  also serves `cid:` images.
- **Tests:** integration with the fake provider: another user's mailbox/message → 404; create with a
  failing test connection → 422; list paginates by date; opening a message caches it once.

> **Built (2026-10-28)** — `Presentation/Controllers/CommunicationsController`, commands `ConnectMailbox`,
> `UpdateMailbox`, `DeleteMailbox`, `SyncMailboxNow`, `OpenMessage`, queries `GetMailboxes`, `GetLabels`,
> `ListMessages`, `GetAttachment`. Changes from the list above:
> - **`POST mailboxes` does not start the backfill**: it creates the mailbox with no cursor, and the
>   first sync cycle (the job's next tick, or `POST mailboxes/{id}/sync`) starts it. The screen calls
>   `sync` right after connecting. An account that already has a mailbox → 409. The account must be
>   the caller's (404 otherwise) and of the provider's kind (`gmail` ↔ `google`, 422 otherwise); the
>   address defaults to the Google email or the IMAP username.
> - **`POST mailboxes/{id}/sync` reconnects**: a `needs-reconnect` mailbox is tested first; a refused
>   test → 422 and nothing changes, a passing one → `Reconnect()` and the cycle runs. A cycle that fails
>   is not an HTTP error: the mailbox comes back with its status and `lastError`.
> - **`PATCH`** replaces the display name; new server settings (all four fields, or none) are tested
>   first and, when they pass, also reconnect.
> - **Page cursor**: `nextCursor` is `"{sent_at utc ticks}.{id}"`, sent back as `before`; a bad one →
>   422. `limit` is 1–100 (default 50). `q` is a case-insensitive substring of subject, snippet and
>   sender (`ILIKE`, no index — fine at one person's volume).
> - **Opening a message** does not mark it as read (M1 only reads), and fills the IMAP snippet from the
>   text body (or the HTML with tags stripped). The body is parsed with MimeKit in Application.
> - **Attachment ids** are the part's position among the message's body parts (stable for the same
>   bytes); parts are files or inline images with a `Content-ID`. The download sends `nosniff`, and only
>   PNG/JPEG/GIF/WebP/BMP go `inline` — anything else (HTML, SVG, PDF) is `attachment`, since the part
>   comes from whoever sent the mail.
> - Tests: `CommunicationsApiTests` (6): refused test → 422 and no row; other provider's account → 422,
>   second mailbox on an account → 409; paging newest first + search + labels with counts + bad
>   cursor; opening fetches the raw message once and serves the PDF as `attachment`; another user's
>   mailbox and message → 404 on every route; sync now stops/reconnects and delete removes the rows.

### Step 8 — Web (`client-web/src/modules/communications`)

- `models.ts`, `services/communications.service.ts`, `hooks/` (TanStack Query, same shape as
  integrations/files).
- **`MailPage`** (`/mail`): left — mailboxes and labels with unread counts; center — antd `Table`
  with `virtual`, keyset "load more", account filter, search; right — reading pane + conversation.
  - `prepareMailHtml(html)`: DOMPurify, then rewrite remote `src` (img, srcset, CSS `url()`) to a
    blocked placeholder unless "Show images"; `cid:` → the attachment endpoint; links get
    `target=_blank rel=noopener noreferrer`.
  - Rendered in `<iframe sandbox="allow-popups allow-popups-to-escape-sandbox" srcdoc=…>` with a
    `<meta http-equiv="Content-Security-Policy">` that allows `img-src` only from `data:` and the API
    origin until images are allowed.
- **`MailAccountsPage`** (`/mail/accounts`): "Connect Gmail" → `integrations/google/connect` with the
  Gmail scopes and `redirectAfter=/mail/accounts?connected=google` → on return, `POST mailboxes`;
  "Add Titan / IMAP" form → `PUT integrations/{titan|imap}/password` → `POST mailboxes`; status,
  progress, "Sync now", reconnect when `needs-reconnect`.
- Router (`mail`, `mail/accounts`), `navigation.tsx` ("Email" group), i18n `mail.*` in `en.json` and
  `pt-BR.json`.
- **Tests:** vitest for `prepareMailHtml` (remote image blocked, allowed after opt-in, script removed,
  `cid:` rewritten) — it is the security boundary of the screen.

> **Built (2026-10-29)** — `client-web/src/modules/communications`: `MailPage`, `MailAccountsPage`,
> `ConnectMailboxModal`, `FixConnectionModal`, `MailSidebar`, `MessageList`, `MessageReader`,
> `lib/prepareMailHtml`. Changes from the list above:
> - **`cid:` images are `data:` URLs**, not the API origin: the attachment endpoint needs the Bearer
>   token, and a `blob:` URL belongs to the app's origin, which the sandboxed iframe does not share. So
>   the iframe's CSP is `img-src data:` until "Show images" (then `data: https: http:`), plus
>   `default-src 'none'; style-src 'unsafe-inline'`.
> - **The list is a plain list**, not a virtual `Table`: pages of 50 behind "Load more" (`ponytail:` in
>   `MessageList`, virtualize if a long scroll gets slow).
> - **Back from Google** (`/mail/accounts?connect=gmail`) the "add" modal reopens on Gmail with the
>   account preselected; the user confirms, then `POST mailboxes` and `sync`. `AccountSelect` gained
>   `excludeIds`, so an account that already has a mailbox is not offered.
> - **Reconnect**: Gmail sends the browser to Google with the account id and comes back to
>   `?sync={id}`; Titan/IMAP open "Edit connection" (a new password replaces the Integrations secret,
>   new IMAP servers go through `PATCH`), then `sync`.
> - The mailbox list polls every 15 s while a backfill or a cycle runs, so the progress bar moves.
>   Attachments download through the API client (the notes `downloadAttachment`); inline images the
>   HTML shows are not listed again.
> - Tests: `prepareMailHtml.test.ts` (6: remote images blocked and allowed, `url()` in style blocks,
>   scripts and handlers removed, `cid:` resolved, links in a new tab, CSP) and `MailPage.test.tsx`
>   (the opened message is in the sandbox, with no tracker URL until "Show images").

### Step 9 — Real-world check and docs

- Manual, against the real accounts — the product-plan's M1 **done when**:
  1. Gmail and Titan connected; headers arrive newest first; the progress bar moves.
  2. A message read in each webmail shows as read in Pandora within one cycle.
  3. Opening a newsletter fires no remote request (browser devtools) until "Show images".
- Titan checklist (product-plan §14): CONDSTORE/QRESYNC (MailKit `Capabilities`), whether SMTP saves to
  Sent (answer needed by M3), ManageSieve (port 4190). Results recorded in the product-plan.
- Docs: `modules/communications/README.md` + `pt-BR/README.md` and the row in `docs/README.md`;
  product-plan status → "M1 implemented".

---

## Known limits

- **IMAP snippet** stays empty until a message is opened.
- **Attachment download** re-fetches the whole raw message.
