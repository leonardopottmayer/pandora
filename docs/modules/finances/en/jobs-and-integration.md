# Background Jobs & Integration

[← Back to index](../README.md) · `Infrastructure/Jobs` · `Contracts`

---

## Background jobs

Three hosted `BackgroundService`s drive the module's automatic behavior. All are **idempotent** and
**audited** (system action). Each uses a `PeriodicTimer` driven by the injected `TimeProvider`, and
a `JobConcurrency` guard prevents overlapping runs.

| Job | Interval | Responsibility |
|---|---|---|
| `RecurrenceGenerationBackgroundService` | every 24h (+ on demand) | For each active recurrence with occurrences inside the horizon (default 30 days), generate a `PendingTransaction` — or post directly when `auto_post`. Also posts scheduled `pending` transactions whose date has arrived. Idempotent via the unique `(recurring_transaction_id, occurred_on)` index. |
| `StatementLifecycleBackgroundService` | every 24h | Create upcoming statements, close statements whose `closing_date <= today`, and mark `overdue` past the due date with an outstanding balance. Idempotent (running twice neither duplicates nor re-closes). |
| `ImportParsingBackgroundService` | every 30s (queue polling) | Pick up `received` import files and parse them asynchronously. A row failure does not abort the file. |

The corresponding on-demand commands (`RunRecurrenceGeneration`, `RunStatementLifecycle`,
`RunImportParsing`, `GenerateRecurringTransactionOccurrence`) let the same logic be triggered
synchronously (e.g. right after creating a recurrence, or in tests).

## Idempotency guarantees

- **Recurrence:** the DB unique index `(recurring_transaction_id, occurred_on)` is the hard
  guarantee — a given occurrence can never be generated twice, even under concurrent runs.
- **Statement lifecycle:** a statement's state transitions are derived from amounts/dates via
  `SyncAmounts`, and closing checks `closed_at`, so re-running is a no-op.
- **Import:** the parsing job transitions the file `received → parsing → completed`; only `received`
  files are picked up.

## Integration events (status)

The `Pottmayer.Pandora.Modules.Finances.Contracts` project exists as the intended home for
integration events consumed by other modules (Notifications is the obvious consumer), but it is
currently **empty** — no integration events are published yet.

Planned events (not implemented):

| Event | Trigger | Consumer intent |
|---|---|---|
| `StatementClosed` | A statement closes (value, due date) | "Your statement closed" |
| `StatementDueSoon` | X days before the due date | Reminder |
| `StatementOverdue` | Past due, unpaid | Alert |
| `ImportCompleted` | Import finished with N suggestions | "You have N entries to review" |
| `PendingTransactionsGenerated` | Recurrence job produced suggestions | Review reminder |

When implemented, these would be published by the existing use cases/jobs and subscribed to by the
Notifications module, with idempotency (one event per transition) and `correlation_id` propagated
from the domain event to the notification. See [Implementation Status](implementation-status.md).
