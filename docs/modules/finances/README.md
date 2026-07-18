# Finances Module

> Personal finance management inside the Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Finances** module gives a user full control over their personal finances: accounts and
credit cards, a double-checked ledger of transactions, categories and tags, recurring entries,
statement/installment handling, bank-file imports (OFX/CSV), full reversibility, and an
append-only audit trail.

The guiding rule of the whole module: **the ledger is the single source of truth.** A balance is
never a stored, editable field — it is always the signed sum of posted transactions. Everything
automatic (imports, recurrences) flows through a staging inbox before it touches the ledger, and
every change is auditable back to its origin.

---

## How this documentation is organized

Start with the **Overview** for the business picture and vocabulary, then dive into the topic you
need. Each topic file carries both the *business context* (what it means for the user and why) and
the *technical rules* (aggregates, invariants, schema, endpoints).

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | Business vision, principles, ubiquitous language, scope (in/out) |
| 2 | [Architecture](en/architecture.md) | Project layout, DDD building blocks, ports, key design decisions |
| 3 | [Data Model](en/data-model.md) | Full schema catalog (`fin001`–`fin016`): columns, constraints, indexes |
| 4 | [Accounts](en/accounts.md) | Account types, currency, archiving |
| 5 | [Cards & Statements](en/cards-and-statements.md) | Card config, statement lifecycle, resolver, payment, settle, write-off, reopen, available limit |
| 6 | [Installments](en/installments.md) | Manual & import-inferred installment plans, cent rounding, projections |
| 7 | [Transactions (Ledger)](en/transactions.md) | Kinds & signs, status machine, balance, transfers, scheduled entries |
| 8 | [Categories & Tags](en/categories-and-tags.md) | System vs. user categories, polymorphic tags |
| 9 | [Recurrences & Inbox](en/recurrences-and-inbox.md) | Recurring templates, staging inbox, approve/reject/link/transfer |
| 10 | [Imports](en/imports.md) | OFX/CSV pipeline, layouts, dedup/reconciliation, installment detection, cutoff, retry |
| 11 | [Reversibility](en/reversibility.md) | Void, unvoid, reverse, and delete guards |
| 12 | [Audit & Provenance](en/audit-and-provenance.md) | Structural provenance + append-only event log + event catalog |
| 13 | [Jobs & Integration](en/jobs-and-integration.md) | Background jobs, idempotency, integration events (status) |
| 14 | [API Reference](en/api-reference.md) | Every endpoint under `/api/v{n}/finances` |
| 15 | [Implementation Status](en/implementation-status.md) | What is built vs. planned |

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Finances.*` (.NET 10, DDD, CQRS-style commands/queries).
- **Schema:** PostgreSQL schema `finances`, tables prefixed `finXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/finances` (React + TanStack Query).
- **API base:** `/api/v{version}/finances`, authenticated, scoped to the token's user.
- **Migrations:** `migrations/migrations/finances/`.
