# Transactions (The Ledger)

[← Back to index](../README.md) · Aggregate: `Transaction` · Table: `fin008_transaction` · API: `/transactions`

---

## Business context

A **transaction** (lançamento) is an atomic movement in the ledger — the beating heart of the
module. Everything else writes *through* it. The amount is **always positive**; the direction it
moves a balance is a function of its **kind**. A `posted` transaction is immutable in
value/destination/kind — corrections are made with `void` + a new entry, or a `reverse`, keeping the
audit honest (see [Reversibility](reversibility.md)).

## Kinds and signs

`TransactionKind` — the amount's effect on an **account** balance is `Sign`; its effect on a
**statement** total is `StatementSign`.

| Kind | Account sign | Statement sign | Target | Meaning |
|---|:---:|:---:|---|---|
| `opening-balance` | +1 | — | account | Initial balance (no counterparty) |
| `income` | +1 | — | account | Money in |
| `expense` | −1 | +1 | account **or** statement | Money out / card purchase |
| `transfer-in` | +1 | — | account | Destination leg of a transfer |
| `transfer-out` | −1 | — | account | Source leg of a transfer |
| `investment-contribution` | −1 | — | account | Aporte (needs an investment account) |
| `investment-redemption` | +1 | — | account | Resgate (needs an investment account) |
| `yield` | +1 | — | account | Rentabilidade (needs an investment account) |
| `adjustment` | +1 | — | account | Manual correction (fixed +1 sign) |
| `refund` | +1 | −1 | account **or** statement | Credit / chargeback |
| `card-statement-payment` | −1 | — | account | Paying a statement (links `paid_statement_id`) |
| `statement-writeoff` | **0** | — | *neither* | Cashless settlement of a statement (onboarding) |

Derived predicates:
- `CanTargetStatement` → `expense` or `refund` (the only kinds that land on a statement).
- `IsTransferLeg` → `transfer-in`/`transfer-out` (users never pick these directly; `CreateTransfer` does).
- `RequiresInvestmentAccount` → `investment-contribution`/`investment-redemption`/`yield`
  (phase-04 decision: these are restricted to `investment` accounts; use a transfer to move between
  checking and investment).
- `IsStatementPayment` → `card-statement-payment`.

## Destination rule (XOR)

Enforced by `ck_fin008_target_xor`:
- A normal entry targets an **account XOR a card statement** (exactly one).
- A `statement-writeoff` targets **neither** — it only sets `paid_statement_id` (the counter-entry
  of a pre-Pandora debt, like `opening-balance` has no counterparty).
- `paid_statement_id` is set only by `card-statement-payment` (from an account) or
  `statement-writeoff` (from neither).

`currency` always equals the destination's currency.

## Status machine

```
pending ──post──▶ posted ──void──▶ void
pending ──void──▶ void
void ──restore (unvoid)──▶ posted
```

| Status | Meaning |
|---|---|
| `pending` | Scheduled / future — **does not affect the posted balance**. |
| `posted` | Effective — contributes to balances and totals. |
| `void` | Cancelled — kept in the ledger, effect reversed. |

- **`posted` is immutable** in value/destination/kind. Only cosmetic fields (description, payee,
  notes, categories) are editable, always with an audited diff (`UpdateDetails`).
- `void` is reversible via `Restore` (unvoid) — see [Reversibility](reversibility.md).
- `SignedAmount` = `amount × Sign` only when `posted` **and** targeting an account (0 otherwise —
  so pending, void, and statement entries don't move an account balance).

## Balance

- **Posted balance** of an account = Σ `SignedAmount` over its `posted` transactions.
- **Projected balance** = posted + `pending` entries (scheduled/future).
- **Statement total** = Σ `StatementSign × amount` of the statement's transactions, cached in
  `fin007.total_amount` and recomputed transactionally on every change via `StatementAmountSync`.
- Projected installments (`origin = projection`, `status = pending`) count toward a statement's
  *projected* total only, never the posted total or a balance.

Balance is never stored (D1). A "balance correction" is an `adjustment` transaction, auditable like
any other.

## Transfers

A transfer is **two linked transactions** (D3), built atomically by `CreateTransferPair`:
- `transfer-out` on the source + `transfer-in` on the destination, sharing a `transfer_group_id`.
- **Same currency** → equal amounts. **Different currencies** → both amounts supplied plus `fx_rate`.
- Both legs post immediately and must be saved together (failure on one leg persists neither).
- Voiding/reversing a transfer acts on the **whole pair** (see [Reversibility](reversibility.md)).

`POST /transactions/transfer` creates a transfer; `POST /pending-transactions/transfer` creates one
from a pair of inbox suggestions (see [Recurrences & Inbox](recurrences-and-inbox.md)).

## Scheduled entries

Creating a transaction with a future date and `post = false` yields a `pending` entry that does not
affect the posted balance. `POST /transactions/{id}/post` effects it (or the recurrence/statement
job posts due ones automatically).

## System descriptions

System-generated entries (opening balance, statement payment, statement write-off) leave
`Description` empty and carry a `SystemDescription` (JSONB). The display text is rendered from that
descriptor **at read time**, localized (e.g. "Payment — June 2026"). User-entered transactions keep
their text in `Description` and leave `SystemDescription` null.

## API

| Method | Route | Purpose |
|---|---|---|
| GET | `/transactions` | List with rich filters (period, account, kind, status, categories, text, origin) |
| GET | `/transactions/{id}` | Detail |
| POST | `/transactions` | Create (account or card; `installments` for parcelado) |
| POST | `/transactions/transfer` | Create a transfer pair |
| PUT | `/transactions/{id}` | Edit cosmetic fields |
| POST | `/transactions/{id}/post` | Post a scheduled entry |
| POST | `/transactions/{id}/void` | Cancel |
| POST | `/transactions/{id}/unvoid` | Undo a cancel |
| POST | `/transactions/{id}/reverse` | Create a mirror reversal |
| PUT | `/transactions/{id}/tags` | Replace tag set |

## Audit events

`transaction.created`, `transaction.posted`, `transaction.edited`, `transaction.voided`,
`transaction.restored`, `transaction.reversed`. Transfers audit both legs with the same correlation.
