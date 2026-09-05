# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Outbound & Templates](outbound-and-templates.md)

PostgreSQL schema **`channels`** (renamed from `notifications`). Conventions: PK
`uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ` timestamps, audit columns
`created_by/created_at/updated_by/updated_at`, named constraints, enums as `VARCHAR` + `CHECK`.

Migrations live in `migrations/migrations/channels/`.

## Table catalog

| # | Table | Contents |
|---|---|---|
| chn001 | `user_channel` | Where a user can be reached, per channel |
| chn002 | `channel_link_token` | The single-use Telegram linking handshake |
| chn003 | `interaction` | A registered inline button + its route home |
| chn004 | `inbound_update` | Inbound idempotency guard + trail |
| chn005 | `notification_preference` | Delivery policy per category |
| chn006 | `notification` | The durable outbound queue |
| chn007 | `user_notification_setting` | Cross-category settings (quiet hours) |

---

## chn001_user_channel

Where a user can be reached. An address is usable only when both **verified** and **enabled**.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `channel` | varchar(20) NOT NULL | `email \| telegram` |
| `address` | varchar(255) NOT NULL | email address, or Telegram chat id |
| `locale` | varchar(10) NOT NULL | preferred locale for this address |
| `is_verified` / `verified_at` | bool / timestamptz | email inherits Identity activation; Telegram is verified by the handshake |
| `is_enabled` / `disabled_reason` | bool / text | the user's off switch, and the automatic disable after a permanent failure |
| `metadata` | jsonb | Telegram username/first name, shown in settings |

Constraints: `chk_chn001_channel`, `uq_chn001_user_channel (user_id, channel)` (one address per channel
per user), `uq_chn001_channel_address (channel, address)` (an address belongs to one account).

## chn002_channel_link_token

The handshake that ties a chat to an account — single use, short lived. The only thing that authorizes
a chat id.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id`, `channel` | | |
| `token` | varchar(64) NOT NULL | short random token, **unique** (`uq_chn002_token`) |
| `locale` | varchar(10) | |
| `expires_at` / `consumed_at` | timestamptz | single use, ~15-minute TTL |

## chn003_interaction

A registered inline button and its route back. The rendered `callback_data` **is this row's id**, which
is how a 64-byte Telegram callback carries a full `(user, module, action, payload)` without holding any
of it.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | rendered into `callback_data` |
| `user_id` | uuid NOT NULL | checked against the callback's sender |
| `owner_module` | varchar(50) NOT NULL | `agenda`, `assistant`, … — read to build the routing key |
| `action` | varchar(100) NOT NULL | `task_done`, `snooze_1h`, `confirm`, … — opaque here |
| `payload` | text NULL | opaque owner payload, returned intact (text, not jsonb: may be a bare id) |
| `notification_id` | uuid NULL → chn006 | the queued notification that declared the button; null for system messages (`fk_chn003_notification … ON DELETE SET NULL`) |
| `expires_at` / `consumed_at` | timestamptz | single use — a second tap is "expired", not a second command |

## chn004_inbound_update

Every update the bot received, recorded **before** processing. The provider's `update_id` makes
reprocessing harmless: the long-polling offset is confirmed by writing this row.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | surrogate kept (plan called for a composite PK; a unique index is used instead) |
| `provider` | varchar(20) NOT NULL | `telegram` |
| `provider_update_id` | bigint NOT NULL | for Telegram, the `update_id` |
| `raw` | jsonb NULL | raw update for debugging; **nulled** by the retention job once it ages out |
| `user_id` | uuid NULL | resolved from `chn001`; null when the chat is unknown |
| `classification` | varchar(20) NOT NULL | `Interaction \| Command \| Message \| Discarded` |
| `received_at` / `processed_at` | timestamptz | |

Constraints/indexes: `uq_chn004_provider_update (provider, provider_update_id)` (idempotency guard),
`ix_chn004_provider_update_id_desc` (long-polling offset on startup), `ix_chn004_received_at_unpurged`
(partial `WHERE raw IS NOT NULL` — supports the retention scan).

## chn005_notification_preference

Delivery policy per category: which channels a kind of notification goes out on, as an ordered array.
An empty array means the user muted that category. `identity.*` never consults this — security
notifications are mandatory.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id`, `category` | uuid, varchar(100) | e.g. `agenda.reminder`, `finances.statement` |
| `channels` | text[] NOT NULL DEFAULT '{}' | ordered; empty ⇒ muted |

Constraint: `uq_chn005_user_category (user_id, category)`.

> **Quiet hours are not here.** They are a *global* per-user setting, so they live in their own
> one-row-per-user table (`chn007`), not as columns on this per-category table. This table stays
> purely about which channels a category goes out on.

## chn006_notification

The durable queue (formerly `not001_notification`).

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `channel` | varchar(20) NOT NULL | |
| `recipient` | varchar(255) NOT NULL | resolved address |
| `user_id` | uuid NULL | who it is for (null for ad-hoc `SendNotificationRequested`) — drives history |
| `category` | varchar(100) NULL | delivery category — drives history |
| `template_key` | varchar(100) NOT NULL | |
| `locale` | varchar(10) NOT NULL | |
| `payload` | jsonb NOT NULL | the flat render payload |
| `subject` / `body` / `is_html` | varchar/text/bool | email content (kept from the original design) |
| `rendered_payload` | jsonb NULL | structured content for channels email columns can't express (Telegram inline keyboard) |
| `status` | varchar(20) NOT NULL | `Pending \| Sending \| Sent \| Failed \| Dead` |
| `attempt_count` / `max_attempts` / `next_attempt_at` / `last_error` | | retry state |
| `provider` / `provider_message_id` | varchar | provider + its message id after send (enables threaded replies) |
| `correlation_id` | uuid NOT NULL | dedup key |
| `group_id` | uuid NULL | shared by the N rows one request fans out into |

Constraints/indexes: `uq_chn006_correlation_channel (correlation_id, channel)` (dedup is **per
channel** — a fan-out shares one correlation id, so uniqueness includes the channel),
`chk_chn006_status`, `ix_chn006_status_next_attempt_at` (dispatcher scan),
`ix_chn006_user_created_at (user_id, created_at DESC)` (delivery-history read).

## chn007_user_notification_setting

A user's cross-category delivery settings. Today that is quiet hours: one daily "do not disturb"
window, held **globally** rather than per category. The window is two wall-clock times with no date,
evaluated against the user's local time — the IANA zone is resolved from Identity preferences at
delivery time, so no zone is stored here. `identity.*` never consults this — security notifications
are mandatory.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | unique |
| `quiet_hours_start` | time NULL | window start (local wall clock), inclusive |
| `quiet_hours_end` | time NULL | window end (local wall clock), exclusive; may be earlier than start (wraps past midnight) |
| `quiet_hours_behaviour` | varchar(20) NULL | `suppress` \| `deliver-anyway` |

All three `quiet_hours_*` columns are null together when quiet hours are off. Constraint:
`uq_chn007_user (user_id)`. Suppression is applied in `NotifyUserRequestedHandler` before fan-out;
`deliver-anyway` keeps the window on record while still sending.
