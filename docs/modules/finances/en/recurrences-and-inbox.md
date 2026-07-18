# Recurrences & Inbox

[← Back to index](../README.md) · Aggregates: `RecurringTransaction`, `PendingTransaction` · Tables: `fin010`, `fin011` · API: `/recurring-transactions`, `/pending-transactions`

---

## Business context

Two connected ideas:
- A **recurring transaction** is a template for a movement that repeats on a schedule (rent, a
  subscription, salary).
- The **inbox** is a single queue of **pending transactions** (suggestions) awaiting the user's
  decision. It is the *only* place anything automatic lands before touching the ledger (design
  decision D4) — recurrences and imports both feed it.

## Recurring transactions (`fin010`)

### Template + rule

- **Template:** destination `account_id` **XOR** `card_id`; `kind`; `amount` (NULL = variable/no
  value); `amount_is_estimate`; `description`; `payee`; system/user categories.
- **Rule** (`RecurrenceRule`): `frequency` (`daily|weekly|monthly|yearly`), `interval (>=1)`,
  `day_of_month (1..31)`, `weekday (0..6)`, `start_date`, `end_date`, `max_occurrences`.
- **Execution:** `status` (`active|paused|finished`), `auto_post`, `auto_generate` (default true),
  `next_occurrence_on` (cursor), `occurrences_count`.

### Rules & lifecycle

- **Structural fields are immutable** after creation: destination, frequency, interval, day anchors,
  and start date cannot change (keeps past occurrences consistent). Only future-facing fields
  (`end_date`, `max_occurrences`, `auto_post`, `auto_generate`) and cosmetic fields are editable via
  `UpdateTemplate`.
- **Next-occurrence math** (`NextOccurrenceAfter`): daily/weekly add days; monthly/yearly clamp
  `day_of_month` to the last valid day of the target month (e.g. the 31st in February → 28/29).
- **Termination** (`IsTerminated`): stop when `occurrences_count >= max_occurrences`, or the next
  date exceeds `end_date`. `AdvanceCursor` increments the count, moves the cursor, and flips the
  status to `finished` when terminated.
- **State machine:** `active ⇄ paused`, `active → finished`. A paused recurrence does not generate.
- `POST {id}/pause`, `POST {id}/resume`, `POST {id}/generate` (generate an occurrence on demand).

### Generation (the job)

The `RecurrenceGenerationService` job runs daily (and on demand): for each active recurrence with
occurrences inside the horizon (default 30 days), it generates a `PendingTransaction`
(source = recurrence) — or **posts directly** if `auto_post` — then advances the cursor.
**Idempotent:** the unique index `(recurring_transaction_id, occurred_on)` guarantees a given
recurrence never generates the same date twice. `auto_generate = false` opts a recurrence out of
automatic generation (it only produces occurrences on demand). See
[Jobs & Integration](jobs-and-integration.md).

## Pending transactions / the inbox (`fin011`)

A pending transaction is a proposed movement. It stays editable while `pending`; once decided the
outcome is terminal.

### Invariants

- **`original_payload` is immutable** — a JSON snapshot of the initial suggestion, never mutated.
  The editable payload lives in the regular columns; the diff between the two is always available.
- Source is `recurrence` (⇒ `recurring_transaction_id`) or `import` (⇒ `import_row_id`).
- Status `pending → approved | rejected` is terminal (`Approve`/`Reject` return `false` if already
  decided).

### Actions

| Action | Effect |
|---|---|
| **Edit** (`PUT {id}`) | Replaces the editable payload (`UpdatePayload`); `original_payload` untouched. Each edit audits a diff. |
| **Approve** (`POST {id}/approve`) | Creates the real `Transaction` from the current payload and links it back (`transaction_id`). Terminal. |
| **Reject** (`POST {id}/reject`) | Ends the suggestion with an optional reason. Terminal. |
| **Link to existing** (`POST {id}/link`) | Resolves the suggestion by pointing it at an existing transaction the user identified as the same movement — no new transaction created. Marked `rejected` + `dedup_status = matched`, reason `linked-to-existing-transaction`, records the matched transaction so the relationship is auditable. |
| **Approve batch** (`POST /approve-batch`) | Approves several at once. |
| **Transfer from pending** (`POST /transfer`) | Turns a pair of inbox suggestions into a transfer pair (e.g. a `transfer-out` seen on one account's import and the `transfer-in` on another). |

### Approval details

`Approve` copies **every field from the suggestion as it currently stands** (edits already baked in):
- **Account target** → creates a posted account transaction.
- **Card target** → resolves/creates the statement (honoring `suggested_statement_id` when present,
  e.g. an imported row already matched to a cycle), creates the statement transaction, and re-syncs
  the statement total.
- The new transaction is linked back to its origin: `MarkAsImport(pending.Id)` or
  `MarkAsRecurrence(recurringId, pending.Id)`, setting `origin` accordingly — so provenance is
  traceable from either side.

Suggestions from imports carry dedup/reconciliation badges (duplicate/suspected/matched) and links
to the related transaction; see [Imports](imports.md).

## API

| Method | Route | Purpose |
|---|---|---|
| GET / POST / PUT / DELETE | `/recurring-transactions`, `/{id}` | CRUD |
| POST | `/recurring-transactions/{id}/pause` · `/resume` · `/generate` | Control & on-demand generation |
| GET | `/pending-transactions` | Inbox (filters: source, account/card, period, status) |
| PUT | `/pending-transactions/{id}` | Edit proposal |
| POST | `/pending-transactions/{id}/approve` · `/reject` · `/link` | Decide |
| POST | `/pending-transactions/approve-batch` | Batch approve |
| POST | `/pending-transactions/transfer` | Build a transfer from two suggestions |

## Audit events

`recurring.created/updated/deleted/paused/resumed/finished/occurrence-generated`;
`pending.created/edited/approved/rejected/linked`.
