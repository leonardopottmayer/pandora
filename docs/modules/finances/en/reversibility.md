# Reversibility & Consistency

[← Back to index](../README.md) · Commands: `VoidTransaction`, `UnvoidTransaction`, `ReverseTransaction`, `DeleteAccount`, `DeleteCard`

---

## Business context

The module is **append-only for money**: anything already `posted` stays in the ledger forever.
There is **no hard delete** of a posted transaction. Three operations undo mistakes, each for a
different situation, and all keep a full audit trail.

| Operation | What it does | When to use |
|---|---|---|
| **Void** (cancel) | Flips a transaction to `void`, keeps the row, reverses its effect on the statement/balance cache. No new row. | A mistake found after the fact, or cancelling something that shouldn't have taken effect yet. A trace must remain, but there's no new "fact" to record. |
| **Unvoid** (restore) | Turns `void` back into `posted`, re-applies the effect. | "I cancelled the wrong one." |
| **Reverse** (estorno) | Creates a **new** opposite-effect transaction, dated **today**, linked to the original via `reversed_transaction_id`. The original is **never changed**. | Something that is already a fact (I paid, I bought, I received) whose correction is itself a new fact — a bank chargeback, a return, a refund. Also to "undo" a data-entry error without deleting anything: original + reversal net to zero, both stay visible. |

## Void

`POST /transactions/{id}/void` — `VoidTransactionCommand`. Terminal at the aggregate level
(`Void` returns `false` if already void → `AlreadyVoid`). The handler reverses cache effects:

- **Standalone card purchase/refund** (`card_statement_id` set): undo its effect on the statement
  total (`totalDelta = −(amount × StatementSign)`).
- **Statement payment** (`paid_statement_id` set): undo its contribution to the statement's paid
  amount (`paidDelta = −amount`).
- **Transfer** (`transfer_group_id` set): the whole **pair** is voided together, in the same unit of
  work.
- **Installment** (`installment_plan_id` set): see below.

All handled through the single `StatementAmountSync` helper so every cache adjustment stays
consistent. Audit: `transaction.voided` (per leg, shared correlation).

### Voiding installments

`VoidTransaction` with `VoidEntirePlan`:
- **Single installment:** only allowed if its statement is still **open**
  (`InstallmentInClosedStatement` otherwise — a billed installment cannot be cancelled).
- **Whole plan:** cancels only installments on **open** statements; installments on closed/paid
  statements are left in place. Each affected statement total is adjusted in the same DB transaction.
  Audit: per-installment `transaction.voided` + `installment-plan.voided`.

## Unvoid (restore)

`POST /transactions/{id}/unvoid` — restores `void → posted` (`Restore` no-ops unless void), re-applying
the same cache deltas with inverted sign. Audit: `transaction.restored` (+ `installment-plan.restored`
where applicable).

> **Accepted limit:** if the statement changed *after* the void (e.g. paid again), unvoid still
> applies the inverse delta and may produce a negative `RemainingAmount` ("credit"). This is
> accepted — the user sees the number and resolves it manually, rather than adding complexity to
> detect and block it. The same rule applies to reverse.

## Reverse (estorno)

`POST /transactions/{id}/reverse` — `ReverseTransactionCommand`. Generalizes `refund` to any posted
transaction. The reversal is a new row, dated today, `origin = reversal`,
`reversed_transaction_id = original.id`, default description `"Estorno: {original.Description}"`
(overridable). The original stays `posted`, untouched.

**Validations:**
- Original must be `posted` (`NotPosted` otherwise).
- The original must not already have a reversal (`uq_fin008_reversed_transaction_id` →
  `AlreadyReversed`). To "undo a reversal", reverse the *reversal* transaction — chaining is allowed,
  unbounded.
- `ReversalKind` must be defined for the kind/case (`ReversalNotSupported` otherwise).

**Reversal kind mapping** (`TransactionKind.ReversalKind`):

| Original | Target | Reversal kind |
|---|---|---|
| `income` | account | `expense` |
| `expense` | account | `income` |
| `expense` | statement | `refund` |
| `refund` | statement | `expense` |
| `investment-contribution` | account | `investment-redemption` |
| `investment-redemption` | account | `investment-contribution` |
| `card-statement-payment` | account (+ paid statement) | `refund` |
| `opening-balance`, `adjustment`, `yield`, `statement-writeoff` | — | **not supported** |

**Per-case behavior:**
- **Plain account entry** (income/expense/investment-*): a same-account, opposite-kind mirror.
- **Transfer pair:** creates a new pair flowing in the opposite direction, with a **new**
  `transfer_group_id`; each new leg links back (`reversed_transaction_id`) to the original leg on the
  same account.
- **Statement payment:** refunds the money to the paying account (`refund`) and reduces the paid
  amount of the paid statement **as it stands today** (may go negative — accepted).
- **Standalone card purchase/refund:** the mirror lands on the **current open statement**
  (resolved/created like a new purchase), **not** the original — mirroring how a real refund shows up
  on this month's statement regardless of when the original was billed.
- **Installment** (`installment_plan_id` set): **not supported** in v1 — use void/void-plan instead.

Audit: `transaction.reversed` on the original (`{ reversalTransactionId }`) + `transaction.created`
on the new one (`origin = reversal`, `reversedTransactionId`), sharing a correlation. Tags are **not**
inherited by the reversal (they are different facts).

## Delete guards

`DELETE /accounts/{id}` and `DELETE /cards/{id}` **only physically delete an entity that has no
history**. If any transaction/statement references it, the command returns a domain conflict
(`AccountErrors.HasHistory` / `Cards.HasHistory`) instead of letting the FK blow up — the message
directs the user to **archive** instead (fully reversible). So a hard delete is only possible for
mistaken registrations that were never used; anything with money is archived, voided/unvoided, or
reversed — always reversible, never erased.

## Out of scope / decided

- **Hard delete of transactions:** discarded. No command erases a `fin008` row with monetary effect.
- **Reversal of an installment / `opening-balance` / `adjustment` / `yield` / `statement-writeoff`:**
  not supported in v1 (no defined opposite kind). Corrections go through void or a new manual entry.
- **Reversing cosmetic edits** (`UpdateTransaction`): not included; the audit already keeps old/new
  for manual correction.
