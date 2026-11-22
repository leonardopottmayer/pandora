# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [API Reference](api-reference.md)

PostgreSQL schema **`assistant`**. Conventions: PK `uuid DEFAULT uuid_generate_v7()` (the aggregates
also generate v7 ids in code), `TIMESTAMPTZ` timestamps, named constraints (`pk_astXXX`, `fk_astXXX_*`,
`chk_astXXX_*`, `uq_`/`ix_astXXX_*`), enums stored as `VARCHAR` + `CHECK` with hyphenated values.
Every user-owned row carries `user_id`; there are no FKs to other schemas.

Migrations live in `migrations/migrations/assistant/`.

## Table catalog

| # | Table | Contents |
|---|---|---|
| ast001 | `assistant_profile` | One assistant configuration per user |
| ast002 | `conversation` | Short-lived threads (30 min idle timeout), with the last numbered list |
| ast003 | `message` | Turns of a conversation: the utterance and the reply's recap |
| ast004 | `command_invocation` | The audit trail: one row per tool call (or lack of one) |
| — | `tars_outbox_message` | Tars transactional outbox for the replies sent to Channels |

---

## ast001_assistant_profile

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | unique (`uq_ast001_user`) |
| `chat_provider` | varchar(40) NOT NULL | AI client key and Integrations provider (`gemini`) |
| `chat_account_id` | uuid NULL | the `integrations.int001_external_account` (API key) the chat spends; no FK. Null until picked |
| `chat_model` | varchar(100) NOT NULL | e.g. `gemini-3.6-flash` |
| `is_enabled` | boolean NOT NULL DEFAULT false | |
| `locale_override` | varchar(20) NULL | the assistant's language; null → `pt-BR` |
| `confirmation_level` | varchar(20) NOT NULL | `strict\|balanced\|trusting` (`chk_ast001_confirmation_level`) |
| `created_by` / `created_at` / `updated_by` / `updated_at` | | audit columns (interceptor) |

No secret is stored here: the key is read from Integrations per call. Before a first save the API
returns defaults (`gemini`, `gemini-3.6-flash`, disabled, `balanced`) without writing a row.

## ast002_conversation

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `started_at` | timestamptz NOT NULL | |
| `last_activity_at` | timestamptz NOT NULL | touched every turn; idle > 30 min → the next utterance starts a new one (in code) |
| `last_listing` | jsonb NULL | the last numbered list: `[{ kind, id, label, at? }]`, replaced by each new list |

Index `ix_ast002_user_activity (user_id, last_activity_at DESC)` — the user's most recent conversation
is looked up on every utterance. There is no `source` column: one conversation serves web and Telegram.

## ast003_message

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `conversation_id` | uuid NOT NULL | FK → `ast002` `ON DELETE CASCADE` |
| `author` | varchar(20) NOT NULL | `user\|assistant` (`chk_ast003_author`) |
| `content` | text NOT NULL | user: the utterance (or transcript); assistant: the **recap**, not the reply shown |
| `created_at` | timestamptz NOT NULL | |

Index `ix_ast003_conversation_created (conversation_id, created_at)`. These rows are the history
re-sent to the model, so they never carry a read tool's data.

## ast004_command_invocation

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | the id posted to confirm/cancel and carried by Telegram buttons |
| `user_id` | uuid NOT NULL | |
| `conversation_id` | uuid NOT NULL | FK → `ast002` `ON DELETE CASCADE` |
| `utterance` | text NOT NULL | what the user said; `[voice note]` when the audio gave no text |
| `command_name` | varchar(100) NULL | the tool; null for a clarification or a provider error |
| `arguments` | jsonb NULL | the tool call's arguments, with the pinned `ref_id`/`ref_kind`/`ref_label`/`ref_at` when an item was pinned |
| `status` | varchar(20) NOT NULL | see below (`chk_ast004_status`) |
| `result` | text NULL | the user-facing message on success, the question of a held call, or the clarification |
| `error` | text NULL | why it failed or was rejected |
| `provider` / `model` | varchar NOT NULL | from the profile at the time |
| `latency_ms` | bigint NOT NULL | |
| `prompt_tokens` / `completion_tokens` | int NOT NULL DEFAULT 0 | transcription included; only on the first row of a multi-call turn |
| `expires_at` | timestamptz NULL | set only while `pending-confirmation` (created + 10 min) |
| `created_at` | timestamptz NOT NULL | |

Index `ix_ast004_user_created (user_id, created_at DESC)` — the history tab.

**Status** (`InvocationStatus`):

| Value | Meaning |
|---|---|
| `executed` | The tool ran and its use case succeeded. |
| `failed` | The tool ran (or looked for its item) and the use case refused. |
| `clarification` | The model replied in prose; nothing ran. |
| `rejected` | Unknown tool, a `ref` not on the last list, or unreadable arguments. |
| `provider-error` | The provider failed or refused the call. |
| `pending-confirmation` | Held; awaits confirm/cancel until `expires_at`. |
| `cancelled` | The user declined a held call. |
| `expired` | A confirm arrived after `expires_at`. |

Only `pending-confirmation` moves: to `executed`/`failed` (confirm), `cancelled` or `expired`.

## tars_outbox_message

The standard Tars outbox table (`event_id` unique, partial index on pending rows), mapped by
`modelBuilder.AddTarsOutbox(schema: "assistant")` in `AssistantDbContext`. It holds the
`SendAssistantReply` events published by the Telegram subscribers, so a reply leaves only if its unit
of work committed. See [Messaging](../../../architecture/en/messaging.md).
