# Audit & Provenance

[← Back to index](../README.md) · Table: `fin016_audit_event` · API: `/audit`

---

## Business context

Every relevant change is answerable after the fact: *who* did *what*, *when*, and *where a
transaction came from*. Two complementary mechanisms cover this (design decision D6).

## 1. Structural provenance (foreign keys)

The FK chain answers "where did this come from" without any free text:

```
Transaction.pending_transaction_id → PendingTransaction.import_row_id → ImportRow.import_file_id
                                   → PendingTransaction.recurring_transaction_id
Transaction.reversed_transaction_id → the original transaction
Transaction.installment_plan_id     → the plan
```

Supporting raw snapshots:
- `ImportRow.raw_data` preserves the original file bytes for the row.
- `PendingTransaction.original_payload` preserves the initial suggestion, immutable.

So from any transaction you can walk back to the exact import line (with its raw content) and the
suggestion the user started from — and from any import file you can list everything it produced.

## 2. Append-only event log (`fin016_audit_event`)

Answers "what happened and what changed". Every relevant mutation writes an event **in the same unit
of work** as the change (audit never lags the data). Each event carries:

- `user_id` (data owner) and `actor_user_id` (who acted; **NULL = system/job**),
- `entity_type` + `entity_id`,
- `event_type`,
- `data` JSONB — a field diff `{ field: { old, new } }` and/or event-specific detail,
- `correlation_id` — groups everything from one operation (e.g. an entire import, a transfer's two
  legs, a reversal's original+new pair),
- `occurred_at`.

The table has **no UPDATE/DELETE** by application policy. Indexes support the two read patterns:
by entity (`entity_type, entity_id, occurred_at`) and by user timeline (`user_id, occurred_at`).

### Example answerable by the model

*"Transaction T came from line 42 of `extrato-nubank-maio.ofx`, suggested with description
`PAG*JoseSilva` and no category; the user edited the description to `Aluguel maio` and set category
`rent` on 2026-06-10 14:32, then approved at 14:33"* — every step is a structured event, no free text
required.

## Event catalog

| Entity type | Event types |
|---|---|
| `account` | `account.created`, `account.updated`, `account.deleted`, `account.archived`, `account.unarchived` |
| `card` | `card.created`, `card.updated`, `card.deleted`, `card.archived`, `card.unarchived` |
| `statement` | `statement.created`, `statement.closed`, `statement.reopened`, `statement.payment-received`, `statement.paid`, `statement.overdue`, `statement.settled-without-cash` |
| `transaction` | `transaction.created`, `transaction.edited`, `transaction.posted`, `transaction.voided`, `transaction.restored`, `transaction.reversed` |
| `installment-plan` | `installment-plan.created`, `installment-plan.voided`, `installment-plan.restored` |
| `recurring-transaction` | `recurring.created`, `recurring.updated`, `recurring.deleted`, `recurring.paused`, `recurring.resumed`, `recurring.finished`, `recurring.occurrence-generated` |
| `pending-transaction` | `pending.created`, `pending.edited`, `pending.approved`, `pending.rejected`, `pending.linked` |
| `user-category` | `category.created`, `category.updated`, `category.activated`, `category.deactivated` |
| `tag` | `tag.created`, `tag.updated`, `tag.deleted`, `tag.linked`, `tag.unlinked` |
| `import-file` / `import-row` | Import pipeline events, all tied by the file's `correlation_id` |

## Implementation notes

- Events are written via the data context's `RecordAsync` from the command handlers, so they enlist
  in the same transaction as the mutation.
- Jobs record with `actor_user_id = user_id` for user-owned data but represent system action; a NULL
  actor denotes pure system events.

## API

| Method | Route | Purpose |
|---|---|---|
| GET | `/audit?entityType=&entityId=` | Timeline of one entity |
| GET | `/audit?correlationId=` | Everything from one operation (e.g. a whole import) |

This is the only reporting-style read currently implemented; broader reports are planned (see
[Implementation Status](implementation-status.md)).
