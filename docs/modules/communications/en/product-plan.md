# Communications Module — Product Plan

> **Status:** Plan only. The backend projects exist as an empty template (wired into the Host, no
> features, no migrations). This plan covers **email**, the first capability; phases M1–M4 below.
> M1's work list: [execution-plan.md](execution-plan.md).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related: [Integrations](../../integrations/README.md) · [Channels](../../channels/README.md) ·
> [Messaging architecture](../../../architecture/en/messaging.md)

---

## 1. What the module does

**Communications** is the user's **inbox** inside Pandora: the messages that arrive *to* the user,
from any provider, in one place. Email comes first.

- Connect **any number of mailboxes** from different providers. Today: **Gmail** and **Titan Email**
  (A2 Hosting). Any IMAP/SMTP server works through a generic provider; new providers (Outlook via
  Graph, for example) are one more adapter.
- **Read** every mailbox in one list, filtered by account, label, unread or text.
- **Organize**: labels and folders, move, archive, trash, read/unread, star — in batches. Changes
  made in Pandora show up at the provider, and changes made at the provider show up in Pandora.
- **Send**: compose, reply, forward, attachments.
- **Rules** ("from X → label Y, skip inbox"), kept in Pandora and edited in one screen. On Gmail they
  become **native Gmail filters**, so Gmail applies them on arrival even when Pandora is down; edits
  made in Gmail flow back. Where the provider has no filter API, Pandora applies the rules itself.
- Publish `MailMessageReceived` for every new message, so other modules can process mail. The first
  consumer is planned to be **Radar** (a curated tech digest that reads newsletters).

### What it is not

- **Not outbound notifications.** Pandora telling the user something (Telegram, system email) is
  [Channels](../../channels/README.md). Communications is the other direction: the user's own
  mailboxes.
- **Not a mail server or an archive.** The provider keeps the mail. Pandora mirrors metadata and
  caches bodies; deleting a mailbox from Pandora deletes nothing at the provider.
- **Not a credential store.** Passwords and OAuth tokens live in
  [Integrations](../../integrations/README.md).

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Backend projects | `Pottmayer.Pandora.Modules.Communications.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` (already exist, empty) |
| PostgreSQL schema | `communications` |
| Table prefix | `comXXX_`, PK `uuid_generate_v7()` |
| Options section | `Pandora:Communications` |
| API base | `/api/v{version}/communications` — JWT, the usual user scope |
| Frontend | `client-web/src/modules/communications`, routes `mail`, `mail/accounts`, `mail/rules` |
| Migrations | `migrations/migrations/communications/` |
| Libraries | `Google.Apis.Gmail.v1` (Gmail), MailKit + MimeKit (IMAP/SMTP, MIME building for both) |

---

## 3. Principles

1. **The provider is the source of truth for mail.** The local mirror (metadata, labels, cached
   bodies) can be rebuilt by syncing again. *(C1)*
2. **Write-through.** A user action (label, move, trash, send) goes to the provider first; the local
   mirror changes only after the provider accepts it. If the provider fails, the user sees the error
   and nothing changes locally. No optimistic queue. *(C2)*
3. **One neutral model: messages + labels.** A Gmail label and an IMAP folder are both a **label**;
   a folder is a label marked **exclusive** (a message has at most one). "Move" is not an operation of
   its own — it is "swap the exclusive label", and each adapter translates it. *(C3)*
4. **Providers declare capabilities, and everything adapts.** Screens and the rule engine read the
   provider's capabilities instead of branching on its name. *(C4)*
5. **Headers mirrored, bodies on demand.** Metadata of every message is mirrored; bodies are fetched
   when opened (or when a rule needs them) and cached; attachments are streamed, not stored. *(C5)*
6. **Credentials live in Integrations.** Communications stores host/port/security (usage); the
   username, password and OAuth tokens are Integrations' (credential). *(C6)*
7. **Remote content is blocked by default.** Mail HTML renders sandboxed, with no scripts and no
   remote images until the user asks — tracking pixels never fire by just opening a message. *(C7)*
8. **Single instance.** Sync and user actions on a mailbox are serialized by an in-process gate per
   mailbox, the same assumption as Integrations' refresh gate. Revisit only if the host scales out.
   *(C8)*

---

## 4. Providers

### 4.1 The provider port

`IMailboxProvider` (Domain/Ports), one implementation per provider kind:

| Operation | What it does |
|---|---|
| `GetCapabilities` | `MultipleLabels`, `ServerFilters`, `NativeThreads` |
| `Sync(cursor)` | changes since the cursor (added, removed, label/flag changes) + the new cursor |
| `ListLabels` / `CreateLabel` / `RenameLabel` / `DeleteLabel` | label management |
| `ApplyLabelChanges(ids, add, remove)` | labels, move, archive, read/unread (`UNREAD`), star |
| `Trash(ids)` | to the provider's trash |
| `GetContent(id)` / `GetAttachment(id, attId)` | body + attachment list; one attachment's bytes |
| `Send(MimeMessage, threadHint)` | send; the adapter saves the sent copy where the provider does not |
| `ListFilters` / `CreateFilter` / `DeleteFilter` | only when `ServerFilters` |

### 4.2 Adapters

| Adapter | Providers | How |
|---|---|---|
| `GmailMailboxProvider` | `gmail` | Gmail API with the access token from `IExternalCredentialProvider`. `history.list` for incremental sync, native labels and threads, send with the raw MIME, `settings.filters` for rules. |
| `ImapMailboxProvider` | `titan`, `imap` | MailKit: IMAP with UID + CONDSTORE (QRESYNC when available), SMTP to send. `titan` is a preset of `imap` (`imap.titan.email:993`, `smtp.titan.email:465`). |

### 4.3 How the label model maps

| | Gmail | IMAP (Titan, generic) |
|---|---|---|
| Label | a Gmail label, stable id | a folder, identified by its path |
| `is_exclusive` | false (a message carries many) | true (a message is in one folder) |
| Inbox / archive | `INBOX` label / no `INBOX` | `INBOX` folder / the folder with role `archive` |
| Move to L | add L, remove `INBOX` (or the current exclusive one) | `MOVE` to L |
| Read / starred | the `UNREAD` / `STARRED` labels | the `\Seen` / `\Flagged` flags |
| Thread | `threadId` | the first Message-ID of `References` (or the message's own) |

---

## 5. Credentials (changes in Integrations)

- New `AuthKind` **`password`**: username + encrypted password. `provider_account_id` is the username
  and the password goes to the existing encrypted secret column (`access_token_enc`), so there is no
  new column — only `'password'` added to `chk_int001_auth_kind` in the original `CREATE TABLE`
  (pre-1.0, edited in place).
- New provider descriptors `titan` and `imap` (auth kind `password`), a `SavePassword` command and
  `PUT /api/v1/integrations/{provider}/password`. `IExternalCredentialProvider` gains a read path for
  the username/password pair.
- Google gains the scopes `gmail.modify`, `gmail.send` and `gmail.settings.basic` on the existing
  connection. Accounts connected before must reconnect to grant them.
- Communications subscribes to `ExternalAccountRevoked` and marks the mailbox `needs-reconnect`.
- **Google Cloud:** the Gmail scopes are *restricted*. For personal use the app works unverified (the
  user sees the "unverified app" screen), but in **Testing** status the refresh token expires every
  7 days — the project must be switched to **In production** (still unverified).

---

## 6. Data model (draft)

| Table | Holds |
|---|---|
| `com001_mailbox` | user, `provider` (`gmail`/`titan`/`imap`), `external_account_id`, address, display name, `imap_host`/`imap_port`, `smtp_host`/`smtp_port`, `security`, `status` (`active`/`syncing`/`needs-reconnect`/`error`), `sync_cursor` (Gmail `historyId`), `backfill_cursor`, `last_synced_at`, `last_error`, `consecutive_failures` |
| `com002_mail_label` | mailbox, `provider_label_id`, name, `kind` (`system`/`user`), `role` (`inbox`/`sent`/`drafts`/`trash`/`spam`/`archive`/null), `is_exclusive`, `uid_validity` and `highest_modseq` (IMAP only — the cursor lives on the folder) |
| `com003_mail_message` | mailbox, `provider_message_id`, `thread_key`, `rfc822_message_id`, from, to/cc (jsonb), subject, snippet, `sent_at`, `is_unread`, `is_starred`, `has_attachments`, `size` |
| `com004_mail_message_label` | message ↔ label |
| `com005_mail_content` | cached body (text + sanitized-at-render HTML) + attachment list (id, name, type, size, content-id), `fetched_at`, `last_accessed_at` |
| `com006_mail_rule` | mailbox, name, `position`, `conditions` and `actions` (jsonb), `execution` (`provider-filter`/`pandora`), `provider_filter_id`, `is_enabled`, `status` (`ok`/`broken`) |
| `tars-outbox-message` | the module's outbox, as in the other publishing modules |

Notes:

- A unified inbox is a query (all the user's mailboxes), not an entity.
- The content cache is evicted by `last_accessed_at` (size cap in `Pandora:Communications`).

---

## 7. Sync

- **`MailboxSyncBackgroundService`** (Infrastructure/Jobs, same pattern as Finances' jobs) syncs
  every active mailbox every 2 minutes (configurable). "Sync now" triggers one mailbox immediately.
- **Backfill.** After connecting, all headers are fetched **newest first**, in batches, resumable from
  `backfill_cursor` (Gmail `pageToken`; IMAP lowest UID fetched). The screens work with whatever has
  arrived. Order of magnitude: 100k messages ≈ 100 MB and ~35 minutes on Gmail (API quota).
- **Gmail, incremental.** `history.list(startHistoryId)` → added/removed messages, label
  added/removed. History expires after about a week: on 404, a state resync (list ids, diff).
- **IMAP, incremental, per subscribed folder.** If `UIDVALIDITY` changed → resync the folder. Else:
  new = `UID > last`; flag changes = `CHANGEDSINCE modseq`; removals = QRESYNC `VANISHED` when
  supported, otherwise a diff of the UID set.
- **Labels, both directions.** Every sync re-reads the label list (`labels.list` / `LIST`) and
  applies the difference. Gmail labels have stable ids, so a rename is a rename. An IMAP folder has
  no id: a rename in the webmail is seen as "folder gone + new folder" and that folder's messages are
  resynced.
- **Event.** `MailMessageReceived` (mailbox, message id, from, subject, labels) goes through the
  outbox in the same transaction that saves the message — **only for messages that arrive after the
  backfill**, never one per old message.

---

## 8. Organizing

- Label management (create, rename, delete) and message actions (labels, move, archive, trash,
  read/unread, star) in batches, all write-through (C2).
- On IMAP, a label is a folder, so "add a label" on a message that already has one is a move; the
  screens offer only what the capabilities allow (C4).

---

## 9. Sending

- **Compose**: from (which mailbox), to, cc, bcc, subject, HTML body, attachments (sent in the same
  multipart request, within the provider's limit — 25 MB on Gmail). The editor reuses Notes' editor
  if it produces HTML that fits; otherwise a separate one is chosen in M3.
- **Reply / forward** set `In-Reply-To` and `References`; on Gmail the `threadId` is passed too, so
  the message joins the conversation.
- **Synchronous**: "Sent" or the error on screen; on failure the form keeps its content. No queue, no
  undo-send.
- **Sent copy**: Gmail saves it itself. Over SMTP, the adapter `APPEND`s to the folder with role
  `sent` — unless the provider descriptor says the server already does (to verify on Titan, §14).
- **Drafts** are not synced with the provider: the form keeps its content in the browser.

---

## 10. Rules

### 10.1 Model

- All conditions must match (AND). For OR, two rules.
- **Conditions:** `from`, `to`, `subject`, `has-words`, `has-attachment`, `provider-query` (Gmail's
  raw search string, so imported filters lose nothing; Gmail mailboxes only).
- **Actions:** add label, move (swap the exclusive label), archive, mark as read, star, trash.
- All matching rules apply, in `position` order — the same as Gmail.
- A rule that references a label that no longer exists is marked `broken` and highlighted.
- Rules apply to messages that arrive after they are saved; "apply to existing messages" is later.

### 10.2 Where a rule runs

Decided when the rule is saved:

- **Gmail mailbox → `provider-filter`.** Everything the v1 model offers can be expressed as a Gmail
  filter, so every Gmail rule is a native filter for now.
- **Otherwise → `pandora`.** The rule engine runs inside the sync job right after new messages are
  saved. A rule with `has-words` fetches the body (which then sits in the cache). Future conditions
  Gmail cannot express (AI classification) also run here, on any provider.

### 10.3 Gmail filters, both directions

For `provider-filter` rules, **Gmail is the source of truth**; Pandora mirrors and edits them. Only
name, position and `is_enabled` are Pandora's own.

- **Pandora → Gmail.** Create → `CreateFilter`. Edit → delete + create (the Gmail API has no update)
  and store the new `provider_filter_id`. Delete → `DeleteFilter`. **Disable** → delete the Gmail
  filter but keep the rule with `provider_filter_id` null (Gmail has no disabled filters); enable
  recreates it.
- **Gmail → Pandora.** Every sync calls `filters.list` (one cheap call) and diffs by
  `provider_filter_id`:
  - a filter unknown to Pandora → a new rule (name generated from its criteria);
  - a known filter that disappeared → the rule is deleted (disabled rules are skipped by the diff);
  - an edit in Gmail arrives as "one gone + one new" (Gmail recreates filters). When exactly one
    disappears and exactly one appears in the same cycle, it is treated as an edit and keeps name and
    position; otherwise delete + import. *(heuristic, marked `ponytail:` in code — covers editing one
    filter at a time)*
- Connecting a Gmail mailbox imports the existing filters through the same diff.

### 10.4 Titan

Titan's webmail filters have no known API, so Titan rules run only in Pandora's engine, and filters
created in the webmail are not seen. If Titan offers **ManageSieve** (§14), a Sieve-backed
`provider-filter` becomes a later phase.

---

## 11. Screens

- **`mail` — the inbox, three columns.** Left: mailboxes and their labels with unread counts
  (right-click to create/rename/delete a label). Center: the list — virtualized, multi-select, batch
  actions, account filter, search. Right: the reading pane, with the conversation view.
  - Search in v1 is local: from, subject, snippet. Searching bodies at the provider is later.
  - HTML renders in a sandboxed `iframe` (no scripts). Remote images stay blocked until "Show images";
    inline `cid:` images come through the attachment endpoint.
- **Compose** — a drawer, also used for reply and forward.
- **`mail/accounts`** — connect Gmail (uses the Google connection; asks to reconnect if the Gmail
  scopes are missing); add Titan/IMAP (address, password, server prefilled for Titan — saves the
  credential in Integrations, then creates the mailbox); status, backfill progress, "Sync now".
- **`mail/rules`** — rules per mailbox: drag to reorder, enable toggle, a badge for where it runs
  (Gmail / Pandora), `broken` highlighted; condition/action form.

---

## 12. API (`/api/v1/communications`)

| Resource | Endpoints |
|---|---|
| Mailboxes | `GET mailboxes`, `POST mailboxes`, `PATCH mailboxes/{id}`, `DELETE mailboxes/{id}`, `POST mailboxes/{id}/sync` |
| Labels | `GET/POST mailboxes/{id}/labels`, `PATCH/DELETE labels/{id}` |
| Messages | `GET messages` (mailbox, label, unread, text, thread; paginated by date), `GET messages/{id}` (with content), `GET messages/{id}/attachments/{attId}`, `POST messages/actions` (batch: ids + add/remove labels, move, archive, trash, read, star) |
| Send | `POST messages/send` (multipart; optional `replyTo` / `forwardOf`) |
| Rules | `GET/POST mailboxes/{id}/rules`, `PUT/DELETE rules/{id}`, `PUT mailboxes/{id}/rules/order` |

Plus, in Integrations: `PUT /api/v1/integrations/{provider}/password`.

---

## 13. Errors

| Failure | Handling |
|---|---|
| Authentication (`ExternalAccountRevoked`, Google `invalid_grant`, IMAP login refused) | mailbox → `needs-reconnect`, sync paused. For IMAP, Communications publishes `NotifyUserRequested` (template `communications.mailbox-auth-failed`); the OAuth case is already notified by Integrations. |
| Transient (network, 5xx, 429) | retried on the next cycle; after 5 consecutive failures the mailbox goes `error` with `last_error` on screen |
| Gmail history expired (404) / IMAP `UIDVALIDITY` changed | state resync (§7) |
| User action or send fails at the provider | the error goes back to the screen, nothing changes locally; the compose form keeps its content |
| Concurrent sync and action on one mailbox | serialized by the per-mailbox gate (C8) |

---

## 14. Testing and checks

- **Unit (domain):** move translation (exclusive vs. multiple labels), rule matching, rule ⇄ Gmail
  filter conversion both ways, the "one gone + one new = edit" heuristic, IMAP `thread_key`.
- **Integration** (`PandoraWebApplicationFactory` + Testcontainers): the IMAP/SMTP adapter against
  **GreenMail** (a test IMAP/SMTP server in one container). It likely lacks CONDSTORE, so it covers the
  fallback path. Endpoints run against a fake `IMailboxProvider`.
- **Manual:** the Gmail adapter against the user's real account (there is no reliable fake Gmail).
- **To verify on the real Titan** (before M1 closes):
  1. CONDSTORE / QRESYNC support.
  2. Whether SMTP submission already saves to Sent (to avoid duplicates).
  3. Whether ManageSieve is offered (§10.4).

---

## 15. Phases

### M1 — Connect and read

- Integrations: `password` auth kind, `titan`/`imap` descriptors, Gmail scopes.
- Mailboxes, labels (read only), backfill and incremental sync on both adapters, the content cache,
  attachments, `MailMessageReceived` through the outbox.
- Screens: `mail` (list + reading) and `mail/accounts`.
- **Done when:** a Gmail and a Titan mailbox are connected, their headers arrive newest first, a
  message read in the provider's webmail shows as read in Pandora within one cycle, and opening a
  newsletter fires no remote request until "Show images".

### M2 — Organize

- Labels both directions (create, rename, delete), batch actions (read, star, move, archive, trash).
- **Done when:** a label created in Pandora appears in Gmail and in Titan, a folder renamed in the
  Titan webmail shows renamed in Pandora, and a move done in either side is reflected on the other.

### M3 — Send

- Compose, reply, forward, attachments, the sent copy over IMAP.
- **Done when:** a reply sent from Pandora lands in the same Gmail conversation, and a message sent
  from Titan appears exactly once in its Sent folder.

### M4 — Rules

- Rule model, Pandora's engine (IMAP), Gmail filters both directions, `broken` status, `mail/rules`.
- **Done when:** existing Gmail filters show up as rules on connect; a rule created in Pandora
  appears as a Gmail filter and applies on arrival with Pandora stopped; a filter edited in Gmail
  updates its rule in place; a Titan rule moves a new message within one cycle.

### Later (not scheduled)

- ManageSieve for Titan (if offered), AI conditions in rules, Radar consuming `MailMessageReceived`,
  per-mailbox signature, server-side drafts, provider-side body search, "apply to existing messages",
  attachment cache, Gmail push (Pub/Sub) instead of polling, more providers (Outlook via Graph).

---

## 16. Open questions

1. **IMAP message identity across folders.** A `MOVE` gives the message a new UID in the destination.
   M1 keys IMAP messages by (folder, UID) and treats a move done in Pandora as an update of that row
   (the adapter returns the new UID via `COPYUID`). A move done in the webmail looks like "removed
   here + new there"; whether to stitch the two by `rfc822_message_id` is decided in M2. Until then,
   M1 must not fire `MailMessageReceived` for a message whose `rfc822_message_id` the mailbox already
   has, so a webmail move is not taken for a new arrival.
2. **Content cache size cap.** Default to decide in M1 from the real numbers of the first backfill.
