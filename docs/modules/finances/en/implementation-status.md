# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is designed but not yet implemented. Use it
to tell the difference between "documented because it exists" and "documented as a plan".

---

## Implemented

| Area | Notes |
|---|---|
| **Module scaffold + audit** | All layered projects, `finances` schema, `fin016` audit trail wired into every mutation. |
| **Categories** | System categories (seeded, 2-level tree) + user categories CRUD (`fin002`, `fin003`). |
| **Accounts** | CRUD, types, immutable currency, archive/unarchive, opening balance (`fin001`). |
| **Ledger** | Transactions, 11 kinds incl. `statement-writeoff`, signs, status machine, derived balance, transfers (`fin008`). |
| **Cards & statements** | Card CRUD, statement lifecycle, resolver, pay, **settle (write-off)**, close, **reopen**, available limit (`fin006`, `fin007`). |
| **Installments** | Manual plans, cent split, void single/plan; import-inferred plans + projections (`fin009`). |
| **Tags** | Tag CRUD + polymorphic links (`fin004`, `fin005`). |
| **Recurrences & inbox** | Recurring templates + rule engine, staging inbox, approve/reject/link/transfer-from-pending, generation job (`fin010`, `fin011`). |
| **Imports** | OFX **and** CSV, seeded bank layouts + auto-detection, three-level dedup/reconciliation, installment detection & projection, cutoff date, retry (`fin012`–`fin014`). |
| **Reversibility** | Void, unvoid, reverse (all cases), delete guards on account/card. |
| **Audit reads** | `/audit` timeline by entity or correlation id. |
| **Frontend** | React module (`client-web/src/modules/finances`) covering accounts, cards, statements, transactions, transfers, categories, tags, recurring, inbox, imports, audit. |

### Additions beyond the original design

The implementation evolved past the first design proposal. Notable additions:

- **`statement-writeoff`** kind + `SettleStatement` — cashless settlement of pre-Pandora statements
  at onboarding.
- **`ReopenStatement`** — reopen a closed statement to new purchases.
- **Import cutoff date** — skip rows before a date to avoid flooding the inbox at go-live.
- **Import retry** + raw-byte storage for re-parsing.
- **Layout auto-detection** (`ILayoutDetector`) for uploaded files.
- **CSV import** with per-bank layouts (originally a later phase).
- **Transfer from pending** and **link-to-existing** on the inbox.
- **`auto_generate`** flag on recurrences (opt out of automatic generation).
- `import_file.file_hash` is **informational, not unique** — deliberate re-import is allowed.

## Not yet implemented (designed / planned)

| Area | Status |
|---|---|
| **Categorization rules** (`fin015`) | Auto-categorize import suggestions ("description contains UBER → Transport"). Table and use cases not created. |
| **Reports** | Cash-flow, by-category, balance-history, and the upcoming-agenda endpoints are not implemented — only the audit timeline exists. |
| **Notifications integration events** | The `Contracts` project is empty; `StatementClosed`/`StatementDueSoon`/`StatementOverdue`/`ImportCompleted`/`PendingTransactionsGenerated` are not published, and there are no Notifications subscribers/templates yet. |
| **User-created import layouts** | Only system layouts are seeded; user layouts + preview endpoint are reserved for a future phase. |
| **Transaction split, budgets, goals, attachments, balance snapshots, multi-currency consolidation, open finance, multi-user households** | Future — the model leaves room without a core rewrite. |

## Known open points

1. **Duplicate heuristic calibration** — the ±2-day window and amount tolerance for suspected
   duplicates are initial guesses; calibrate against real statements (possibly make them configurable
   per user/layout).
2. **`file_content` retention** — raw bytes are kept in the DB (`bytea`); revisit moving to object
   storage or expiring after N months.
3. **Auto-post of a recurrence on a card** — whether posting straight to a statement without review
   is desirable, or a card should always go through staging.
4. **Cross-currency statement payment** — v1 records the payment in the account currency + `fx_rate`;
   revisit with multi-currency consolidation.
5. **Backfill of past installments** — when a plan is created from installment N via import, whether
   to optionally generate installments 1..N−1 (default: no).
