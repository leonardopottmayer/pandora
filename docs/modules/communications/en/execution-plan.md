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
| One Google account per user | kept (the OAuth callback upserts by user + provider) | known limit, outside M1 — see [Known limits](#known-limits) |
| Body and attachments | the adapter fetches the **raw RFC 822** message (Gmail `format=raw`, IMAP `BODY.PEEK[]`) and MimeKit parses it — one parser for both providers | an attachment download re-fetches the raw message (`ponytail:` ceiling: huge messages; upgrade to Gmail `attachments.get` / IMAP `BODY[part]`) |
| Content cache | age-based: rows not accessed in `ContentCacheRetentionDays` (default 30) are deleted by the sync job | closes open question 2 of the product-plan with the simplest rule |
| IMAP message key | `provider_message_id = "{label_id}:{uid}"` (label = the folder's `com002` row) | UIDs are per folder; a `UIDVALIDITY` change resyncs the folder and drops its rows |
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

### Step 2 — Communications: schema, persistence, outbox

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

### Step 3 — Domain and the provider port

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

### Step 4 — IMAP adapter (MailKit)

- Connect with the mailbox's host/port/security + `GetPasswordAsync`; `AuthenticationException` maps to
  a typed `MailboxAuthFailed` error (drives `needs-reconnect`).
- Labels from folders: SPECIAL-USE flags (`\Sent`, `\Trash`, `\Junk`, `\Drafts`, `\Archive`) → `role`;
  `INBOX` → `inbox`; all `is_exclusive = true`.
- Per folder at backfill start: record `UIDVALIDITY` and `UIDNEXT` (the boundary). Incremental:
  `UID ≥ boundary` = new; `CHANGEDSINCE highest_modseq` when CONDSTORE is advertised, else re-read
  flags of the last N UIDs (`ponytail:` window, full flag diff if it misses changes); removals by
  `VANISHED` (QRESYNC) or a UID-set diff. Backfill: UIDs below the boundary, descending, batches of
  500, fetching `ENVELOPE FLAGS BODYSTRUCTURE RFC822.SIZE` + `References`.
- `GetRawMessage` = `BODY.PEEK[]` (never marks as read).
- **Tests:** integration against **GreenMail** (Testcontainers): seed messages over SMTP, connect,
  backfill, deliver one more → seen as new, flag `\Seen` from another client → seen as read, delete →
  removed. Covers the non-CONDSTORE path.

### Step 5 — Gmail adapter

- `Google.Apis.Gmail.v1`, `GoogleCredential.FromAccessToken(GetAccessTokenAsync(userId, "google"))`.
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

### Step 6 — Sync engine

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
- `CommunicationsOptions`: `SyncInterval`, `BackfillBatchSize`, `ContentCacheRetentionDays`.
- **Tests:** integration with a fake `IMailboxProvider` registered in the factory: backfill fills rows
  without events; an incremental arrival publishes one event; a re-appearing `rfc822_message_id`
  publishes none; auth failure → `needs-reconnect` + one `NotifyUserRequested`; revoked event →
  `needs-reconnect`.

### Step 7 — API

`/api/v1/communications`, `[Authorize]`, every query scoped to the caller's mailboxes:

- `GET mailboxes` (status, last error, backfill progress = rows / `message_total_estimate`).
- `POST mailboxes` `{ provider, externalAccountId, address?, displayName?, imapHost?, imapPort?, smtpHost?, smtpPort?, security? }`
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

- **One Google account per user.** The OAuth callback upserts by (user, provider), so a second Google
  account replaces the first. Lifting it means looking OAuth accounts up by account id end to end
  (callback, `GetAccessTokenAsync`); an Integrations change, scheduled when a second Gmail is needed.
- **IMAP snippet** stays empty until a message is opened.
- **Attachment download** re-fetches the whole raw message.
