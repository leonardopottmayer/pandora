# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [API Reference](api-reference.md)

PostgreSQL schema **`communications`**. Conventions: PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ`
for timestamps, named constraints (`pk_comXXX`, `uq_comXXX_*`, `chk_comXXX_*`), enums stored as `VARCHAR`
+ `CHECK` with hyphenated values.

Migrations live in `migrations/migrations/communications/`.

## Table catalog

| # | Table | Contents |
|---|---|---|
| com001 | `mailbox` | One connected mailbox and its sync state |
| com002 | `mail_label` | A Gmail label or an IMAP folder |
| com003 | `mail_message` | The mirrored header of one message |
| com004 | `mail_message_label` | Which labels a message carries |
| com005 | `mail_content` | The cached body of an opened message |
| — | `tars_outbox_message` | The module's outbox (`MailMessageReceived`, the auth-failure notice) |

`com006_mail_rule` arrives with M4.

---

## com001_mailbox

The credential lives in Integrations: `external_account_id` references it only logically, since no foreign
key leaves the schema.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | owner |
| `provider` | varchar(20) NOT NULL | `gmail` \| `titan` \| `imap` |
| `external_account_id` | uuid NOT NULL | the Integrations account |
| `address` | varchar(320) NOT NULL | |
| `display_name` | varchar(100) NULL | |
| `imap_host`, `imap_port`, `smtp_host`, `smtp_port` | NULL | only for `titan` (preset) and `imap`; TLS or STARTTLS follows from the port |
| `status` | varchar(20) NOT NULL | `active` \| `syncing` \| `needs-reconnect` \| `error` |
| `sync_cursor` | text NULL | opaque; null until the backfill starts |
| `backfill_cursor` | text NULL | opaque; null when there is nothing left to read |
| `backfill_completed_at` | timestamptz NULL | |
| `message_total_estimate` | int NULL | the provider's count at backfill start, for the progress bar |
| `last_synced_at` | timestamptz NULL | |
| `last_error` | text NULL | |
| `consecutive_failures` | int NOT NULL DEFAULT 0 | `error` at 5 |
| `created_by`, `created_at`, `updated_by`, `updated_at` | | audit |

**Constraints:** `uq_com001_user_external_account (user_id, external_account_id)`: one mailbox per
credential. **Indexes:** `user_id`; `external_account_id` (a revoked credential → its mailboxes).

## com002_mail_label

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `mailbox_id` | uuid FK → com001, cascade | |
| `provider_label_id` | text NOT NULL | the Gmail label id, or the folder's full path |
| `name` | text NOT NULL | |
| `kind` | varchar(20) NOT NULL | `system` \| `user` |
| `role` | varchar(20) NULL | `inbox` \| `sent` \| `drafts` \| `trash` \| `spam` \| `archive` |
| `is_exclusive` | boolean NOT NULL | false on Gmail, true on IMAP |

**Constraints:** `uq_com002_mailbox_provider_label (mailbox_id, provider_label_id)`.

## com003_mail_message

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `mailbox_id` | uuid FK → com001, cascade | |
| `provider_message_id` | text NOT NULL | Gmail's id; on IMAP `{uidvalidity}:{uid}:{folder path}` |
| `thread_key` | text NOT NULL | `threadId`, or the first Message-ID of `References` |
| `rfc822_message_id` | text NULL | without the angle brackets |
| `from_name`, `from_address` | NULL | |
| `to_recipients`, `cc_recipients` | jsonb NOT NULL DEFAULT `[]` | `[{ "Name", "Address" }]` |
| `subject` | text NOT NULL | |
| `snippet` | text NOT NULL | on IMAP, empty until the message is opened |
| `sent_at` | timestamptz NOT NULL | always UTC |
| `is_unread`, `is_starred`, `has_attachments` | boolean NOT NULL | on Gmail, `has_attachments` means a top-level `multipart/mixed` |
| `size_bytes` | bigint NOT NULL DEFAULT 0 | |

**Constraints:** `uq_com003_mailbox_provider_message (mailbox_id, provider_message_id)`.

**Indexes:** `(mailbox_id, sent_at DESC, id DESC)` for the list; `(mailbox_id, rfc822_message_id)` for "do we
already have this one?"; `(mailbox_id, thread_key)` for a conversation.

## com004_mail_message_label

`message_id` (FK → com003, cascade) + `label_id` (FK → com002, cascade), both the PK. Index
`(label_id, message_id)`. On IMAP a message has exactly one row here: its folder.

## com005_mail_content

| Column | Type | Notes |
|---|---|---|
| `message_id` | uuid PK, FK → com003, cascade | |
| `text_body`, `html_body` | text NULL | HTML as received; the client sanitizes it to render |
| `attachments` | jsonb NOT NULL DEFAULT `[]` | `[{ "Id", "FileName", "ContentType", "SizeBytes", "ContentId" }]`; the list, not the bytes |
| `fetched_at`, `last_accessed_at` | timestamptz NOT NULL | |

**Indexes:** `last_accessed_at`, for the retention delete.

### Content cache

Rows not opened for `ContentCacheRetentionDays` (default 30) are deleted by the job, and fetched again on the
next open. There is no size cap. Measured on 2026-10-30, with one Gmail and one Titan mailbox:

| | Value |
|---|---|
| Messages mirrored | 65,656 (Gmail 64,018, Titan 1,638) |
| Those messages at the providers | about 4.9 GB |
| `com003` + `com004` | 90 MB, about 1.4 kB per message |
| A cached body | 15–20 kB on average (only 3 sampled) |

At that size, opening 1,000 messages within 30 days costs about 20 MB, so a cap would add code for nothing.
The sample is small: add a cap if `com005` ever grows past what the mirror itself takes.
