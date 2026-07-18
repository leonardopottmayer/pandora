# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Overview](overview.md)

---

## 1. Project layout

The module mirrors the Identity/Notifications modules, split into layered projects under
`backend/src/Modules/Finances/`:

```
Pottmayer.Pandora.Modules.Finances.
  Abstractions      → public contract for other modules (FinancesModule registration)
  Application       → Commands, Queries, Dtos, Services, Auditing, DI
  Contracts         → IntegrationEvents (currently empty — see Jobs & Integration)
  Domain            → Aggregates, ValueObjects, Errors, Ports (Repositories + Services)
  Infrastructure    → Jobs (recurrence, statement lifecycle, import parsing), OFX/CSV parsers, DI
  Persistence       → EntityConfigs, Repositories, FinancesDbContext, DI
  Presentation      → Controllers, Requests, DI
```

Design style: **DDD aggregates** with private constructors + static factories, a `TimeProvider`
injected for all time reads, and a **command/query** application layer (one folder per use case).
Every write goes through a command handler; reads go through query handlers returning DTOs.

## 2. Domain building blocks

### Aggregates (`Domain/Aggregates`)

| Aggregate root | Responsibility / key invariants |
|---|---|
| **Account** | Account config. Currency immutable after creation; archived account rejects business mutations. |
| **Card** | Card config. `closing_day`/`due_day` ∈ 1..28; currency immutable; archived rejects mutations. |
| **CardStatement** | Billing cycle. State machine `open → closed → partially-paid/paid/overdue`; `total_amount`/`paid_amount` caches recomputed transactionally; derives status from amounts + dates. |
| **Transaction** | Ledger movement. Exactly one destination (account XOR statement, except `statement-writeoff` which has neither); `amount > 0`; `posted` is immutable in value/destination/kind; `void` terminal (but reversible via `Restore`). |
| **InstallmentPlan** | Installment purchase. `count ≥ 2`; installments sum to total when `origin = manual`; deterministic cent split. |
| **UserCategory** | User category: 2-level hierarchy (child has no child); child nature = parent nature. |
| **Tag** / **TagLink** | Tag (unique name per user) + polymorphic link to any entity. |
| **RecurringTransaction** | Template + rule. Structural fields immutable after creation; cursor `next_occurrence_on` only advances; paused doesn't generate. |
| **PendingTransaction** | Staging. `original_payload` immutable; `pending → approved/rejected` terminal. |
| **ImportFile** / **ImportRow** | File pipeline (status machine, counters) + parsed rows (raw preserved). |
| **ImportLayout** | Parsing profile; system layouts (`user_id NULL`) are read-only to users. |
| **SystemCategory** | Reference/read-only data (seeded). No behavior; read via `ISystemCategoryReader`. |
| **AuditEvent** | Append-only store; written via repository, never read by the domain for decisions. |

### Value objects (`Domain/ValueObjects`)

`Money` (amount + currency), `CurrencyCode` (ISO 4217 or crypto ticker, validated by shape),
`TransactionKind`, `TransactionStatus`, `TransactionNature`, `AccountType`, `StatementStatus`,
`EntryOrigin`, `PendingTransactionStatus`, `RecurringTransactionStatus`, `RecurrenceFrequency`,
`RecurrenceRule`, `DedupStatus`, `ImportFileStatus`, `ImportRowStatus`, `LayoutFileFormat`,
`ImportLayoutAccountType`, `TaggableEntityType`, `SystemDescription`.

### Ports (`Domain/Ports`)

- **Repositories:** `IAccountRepository`, `ICardRepository`, `ICardStatementRepository`,
  `ITransactionRepository`, `IRecurringTransactionRepository`, `IPendingTransactionRepository`,
  `IImportFileRepository`, `IImportRowRepository`, `IImportLayoutRepository`,
  `IUserCategoryRepository`, `ISystemCategoryReader`, `ITagRepository`, `ITagLinkRepository`,
  `IInstallmentPlanRepository`, `IAuditEventRepository`.
- **Services:** `IStatementResolver` (purchase + card → target statement), `IImportParser`
  (OFX/CSV strategies), `IDuplicateDetector` (certain / suspected / matched), `ILayoutDetector`
  (auto-pick a layout for an uploaded file).

Application-layer services (`Application/Services`): `StatementAmountSync` (single helper for
statement total/paid recomputation), `StatementResolver`, `StatementMaintenance`,
`InstallmentPlanAssembler`, `TagTargets`.

## 3. Key design decisions

| # | Decision | Rationale (rejected alternative) |
|---|---|---|
| **D1** | Balance is derived from the ledger (Σ signed posted amounts), never a stored field. | A mutable `balance` column is the classic source of inconsistency and is unauditable. |
| **D2** | A card is not an account; the statement is the central entity. Card spend hits the statement; only paying the statement touches an account. | Matches the Brazilian mental model. Rejected "card as a liability account" (worse UX for statements/installments). |
| **D3** | A transfer is two linked transactions (`transfer-out` + `transfer-in`, shared `transfer_group_id`). Each transaction affects exactly one destination. | Keeps every balance a simple per-account sum. Rejected single `from/to` row (breaks the one-transaction-one-destination rule). |
| **D4** | Unified staging for everything automatic: imports and recurrences both produce `PendingTransaction`. | One inbox, one approval flow, one audit path. Rejected per-source staging. |
| **D5** | System and user categories live in separate tables and columns. An entry can carry both. | Separates lifecycle (seed-by-migration vs. user CRUD). Rejected a single table with an `is_system` flag. |
| **D6** | Audit = structural provenance (FK chain) + append-only event log with JSONB diffs. | Covers 100% of the audit requirement without full event sourcing. |
| **D7** | Money as `NUMERIC(20,8)` + a currency code string. | Covers BRL (2 dp) and crypto (8 dp). Rejected integer minor units (bad for variable crypto exponents). |
| **D8** | Statement is its own aggregate (not a child loaded with the card). | Statements change often and independently; loading the whole card per entry would be needless contention. Limit vs. statement consistency is calculated in a query, not an aggregate invariant. |
| **D9** | Reconciliation: an import confirms what was expected instead of duplicating. Three levels — certain duplicate (no suggestion), suspected (flagged suggestion), matched (confirmation of an expected entry). | Rejected binary "new or duplicate", which would systematically duplicate recurrences/projections. |

## 4. Cross-cutting rules

- **Multi-tenant by user.** Every user-owned table has `user_id NOT NULL`. Every endpoint is
  authenticated and scoped to the token's user; another user's resource returns **404** (not 403).
- **Audit on every mutation.** Relevant mutations write a `fin016_audit_event` in the **same unit
  of work** as the change — audit never lags behind the data.
- **`TimeProvider` everywhere.** No aggregate reads `DateTime.Now` directly; time is injected, which
  makes the jobs and state machines testable.
