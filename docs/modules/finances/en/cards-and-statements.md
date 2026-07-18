# Cards & Statements

[← Back to index](../README.md) · Aggregates: `Card`, `CardStatement` · Tables: `fin006_card`, `fin007_card_statement` · API: `/cards`, `/statements`

---

## Business context

A **credit card** is billed through monthly **statements** (faturas), not by posting directly to an
account (design decision D2). A purchase lands on the statement whose cycle it falls into; only
**paying** (or settling) the statement moves money out of a real account. This matches the
Brazilian mental model of *fatura → fechamento → vencimento → pagamento*.

Debit cards are **not** modeled — a debit is a direct account entry.

## Card

- **Config:** name (unique per user), brand, last four digits, `credit_limit` (`>= 0`, optional),
  `closing_day`, `due_day`, currency, `default_payment_account_id`.
- **`closing_day` / `due_day` ∈ 1..28** — enforced by CHECK to avoid month-length ambiguity.
- **Currency is fixed** at creation (immutable, like accounts).
- **Archiving** works like accounts: archived card is hidden, rejects business mutations, keeps
  history, is reversible via unarchive.

### Available limit

`GET /cards/{id}/available-limit` returns `credit_limit − Σ(unpaid statement totals)` when a limit
is set (otherwise `null`). The limit is **informational** — there is no hard block on exceeding it
(D8: card/statement consistency is a query, not an invariant).

## Statement lifecycle

A statement is its own aggregate (D8), one per `(card_id, reference_month)`. Its status is always
**derived** from the amounts and dates via `SyncAmounts`, never set blindly.

```
open ──(closing_date reached / job / manual close)──▶ closed
closed ──partial payment──▶ partially-paid ──full settle──▶ paid (terminal via payment)
closed | partially-paid ──(past due_date, balance remains)──▶ overdue ──payment──▶ paid
closed | partially-paid | overdue ──reopen──▶ (status recomputed)
```

`SyncAmounts(total, paid, today)` derives status in this order:
1. `remaining <= 0` → **paid** (sets `paid_at`).
2. past `due_date` **and** closed → **overdue** (sets `overdue_at`).
3. closed **and** `paid > 0` → **partially-paid**.
4. closed → **closed**.
5. otherwise → **open**.

`RemainingAmount = max(0, total − paid)`. `IsClosedToNewPurchases` = any status other than `open`.

### Statement resolution (which statement a purchase lands on)

`StatementResolver.Resolve(card, purchaseDate)`:
- If `purchaseDate.Day <= closing_day` → reference month is the purchase month; else the **next**
  month (purchase after closing rolls to the next statement).
- `closing_date` = `closing_day` of the reference month.
- `due_date` = `due_day`; if `due_day <= closing_day`, the due date falls in the month **after** the
  reference month (otherwise the same month).
- The target statement is created on demand if it doesn't exist yet; the user may force a different
  statement.

### Closing & reopening

- **Automatic:** the `StatementLifecycleService` job closes statements whose `closing_date <= today`
  and marks `overdue` past the due date (see [Jobs & Integration](jobs-and-integration.md)).
- **Manual close:** `POST /statements/{id}/close` — no-op unless currently open.
- **Reopen:** `POST /statements/{id}/reopen` — reopens a closed/partially-paid/overdue statement to
  new purchases, clears `closed_at`, and recomputes status. No-op if still open or already fully paid.

## Paying a statement

`POST /statements/{id}/pay` with an account and amount → `PayStatementCommand`:

- Amount must be `> 0`; the account must exist and **not be archived**.
- **Cross-currency:** if the account currency ≠ the card currency, an explicit `fxRate` is required
  (`MissingFxRate` otherwise).
- Creates a `card-statement-payment` transaction on the account (sign −1, money leaves the account),
  linked to the statement via `paid_statement_id`, carrying the system category `credit-card-payment`.
  Without a user description it renders a localized system description (e.g. "Payment — June 2026").
- Applies the payment through `StatementAmountSync` (`paidDelta = +amount`) and re-derives status.
- **Partial and multiple payments are supported** — the statement becomes `partially-paid` until the
  cumulative paid amount clears the total, then `paid`.
- Audit: `transaction.created` + `statement.payment-received`, plus `statement.paid` when this
  payment was the one that fully cleared it.

## Settling without cash (onboarding write-off)

`POST /statements/{id}/settle` → `SettleStatementCommand`:

- Purpose: clear a **pre-Pandora** statement's balance without debiting any account (used when
  onboarding historical statements that were already paid before the user started using Pandora).
- Creates a `statement-writeoff` transaction (sign **0**) with **no account and no statement
  destination** — only `paid_statement_id`. It is a durable ledger row (survives `paid_amount`
  recomputes), the counter-entry of a pre-Pandora debt, mirroring how `opening-balance` has no
  counterparty.
- Settles the **full remaining amount** → drives the statement straight to `paid`. Fails with
  `NothingToSettle` if nothing remains.
- Audit: `transaction.created` + `statement.settled-without-cash`.

## Purchases & refunds on a statement

A card **purchase** is an `expense` (statement sign +1) on the resolved statement; a **refund/credit**
is a `refund` (statement sign −1) that reduces the total. A `closed` statement rejects new purchases
(they roll to the next). The statement's `total_amount` cache is recomputed transactionally on every
mutation through `StatementAmountSync` and always equals the sum of its transactions. See
[Transactions](transactions.md) and [Installments](installments.md).

## API

| Method | Route | Purpose |
|---|---|---|
| GET / POST / PUT / DELETE | `/cards`, `/cards/{id}` | CRUD (delete only without history) |
| POST | `/cards/{id}/archive` · `/unarchive` | Archive/unarchive |
| GET | `/cards/{id}/statements` | Statements of the card |
| GET | `/cards/{id}/installment-plans` | Installment plans of the card |
| GET | `/cards/{id}/available-limit` | Limit − unpaid statements |
| PUT | `/cards/{id}/tags` | Replace tag set |
| GET | `/statements/{id}` | Detail + entries |
| POST | `/statements/{id}/pay` | Pay from an account |
| POST | `/statements/{id}/settle` | Cashless write-off (onboarding) |
| POST | `/statements/{id}/close` | Manual close |
| POST | `/statements/{id}/reopen` | Reopen to new purchases |
| PUT | `/statements/{id}/tags` | Replace tag set |

## Audit events

`card.created/updated/archived/unarchived`; `statement.created`, `statement.closed`,
`statement.reopened`, `statement.payment-received`, `statement.paid`, `statement.overdue`,
`statement.settled-without-cash`.
