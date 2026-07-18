# Overview — Business & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. What the module does

The **Finances** module is a complete personal-finance manager living inside the Pandora modular
monolith. It lets a single user:

- Register **accounts** (cash, checking, savings, international, crypto, investment, other) and
  **credit cards** with a full **statement** (fatura) cycle.
- Record **transactions** (lançamentos) directly against an account or against a card statement:
  income, expense, transfer, investment contribution/redemption, yield, statement payment, refund,
  adjustment, and cashless statement write-off.
- Classify entries with **system categories** (seeded, hierarchical) *and* **user categories**
  (custom, hierarchical) — two independent dimensions, both usable on the same entry.
- Attach free-form **tags** to any entity.
- Define **recurring transactions** that generate suggestions automatically.
- **Import bank files** (OFX and CSV, with per-bank layouts) into accounts and cards.
- Review everything automatic in a single **inbox** of *pending transactions* that the user edits,
  approves, rejects, or links to an existing entry.
- Undo mistakes safely: **void / unvoid / reverse**, never a hard delete of posted money.
- Trust a complete **audit trail and provenance** chain for every relevant change.

## 2. Core principles

1. **The ledger is the source of truth.** A balance is never an editable field — it is the signed
   sum of `posted` transactions. Nothing "adjusts the balance by hand"; corrections are themselves
   transactions (`adjustment`), auditable like any other. *(Design decision D1.)*
2. **Nothing automatic enters the ledger without a trace.** Imports and recurrences pass through
   staging (or are auto-posted with an explicit user flag), and the provenance chain is *structural*
   (foreign keys), not just a text log. *(D4.)*
3. **Append-only for money.** Anything already `posted` stays in the ledger forever. `void`/`unvoid`
   flip a row's status; `reverse` adds a new mirror row. Audit events are never altered or deleted.
4. **The model is built to evolve** (budgets, goals, transaction split, open finance) without
   rewriting the core.

## 3. Ubiquitous language (glossary)

| Term | Meaning |
|---|---|
| **Account** (Conta) | A balance repository owned by the user: cash/wallet, checking, savings, international, crypto, investment, other. Has a fixed currency. |
| **Card** (Cartão) | A credit card. **Not an account**: purchases go to the statement; only paying the statement moves an account. Debit cards are not modeled — a debit is a direct account entry. |
| **Statement** (Fatura) | A card's monthly cycle: groups transactions between closings; has a closing date, due date, and status (open → closed → paid/partially-paid/overdue). |
| **Transaction** (Lançamento) | An atomic ledger movement. Targets exactly **one** destination: an account **or** a card statement. A transfer is two linked transactions. |
| **Transfer group** | A pair of transactions (`transfer-out` on the source + `transfer-in` on the destination) linked by a shared id. Supports different currencies (with a recorded FX rate). |
| **Installment plan** (Parcelamento) | A card purchase split into N installments: one plan + N transactions, one per statement. |
| **System category** | A system-maintained, seeded, hierarchical (parent → child) category, typed by nature (expense/income). Global, identical for every user. |
| **User category** | A user-created, also hierarchical category. Separate column: an entry can carry a system category **and** a user category at once. |
| **Tag** | A free user label, applicable to any module entity via a polymorphic link. |
| **Recurring transaction** (Recorrência) | A movement template + a repetition rule. Generates pending transactions (or posts directly, if `auto_post`). |
| **Pending transaction** (Transação pendente / sugestão) | A staging record: a proposed entry from import or recurrence. Editable; approval creates the real transaction; rejection ends it. Keeps an immutable snapshot of the original suggestion. |
| **Import file / Import row** | An uploaded bank file (OFX/CSV) and each parsed line/record from it, with the raw content preserved. |
| **Import layout** | A parsing profile for OFX quirks or CSV structure per bank: column mapping, date format, decimal separator, sign convention, installment-detection patterns. |
| **Reconciliation** | Matching an imported row to an already-existing or *expected* entry (scheduled, recurrence-generated). Approving reconciles: it confirms/posts the existing entry with the real values, without duplicating. |
| **Audit event** | An append-only record of any relevant change: who, when, on what, what changed (diff), with a correlation id. |

## 4. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

Accounts, cards & statements (with onboarding write-off/settle and reopen), the full transaction
ledger, transfers, installments (manual & import-inferred with projections), recurrences + inbox,
OFX & CSV imports with layout auto-detection, dedup/reconciliation, tags, system + user categories,
reversibility (void/unvoid/reverse + delete guards), and the audit trail.

### Out of scope / future

| Feature | Status |
|---|---|
| **Categorization rules** (auto-categorize imports, `fin015`) | Designed, not yet implemented. |
| **Reports** (cash-flow, by-category, balance-history, upcoming agenda) | Not yet implemented (only the audit timeline exists). |
| **Notifications integration events** (statement closed/due/overdue, import completed) | Contracts project exists but is empty. |
| **Transaction split** (one entry, several categories) | Future — additive child table. |
| **Budgets / goals** | Future. |
| **Attachments / receipts** | Future. |
| **Balance snapshots** (performance on large history) | Future — the ledger stays the truth. |
| **Open finance / auto-sync** (Pluggy etc.) | Future — enters through the same import pipeline. |
| **Multi-currency consolidation** (converted totals) | Future — needs an FX provider. |
| **Multi-user / shared households** | Future — `user_id` everywhere leaves the door open. |
