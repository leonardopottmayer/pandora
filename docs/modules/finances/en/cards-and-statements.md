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

- **Config:** name (unique per user), brand, last four digits, `account_id` (the account the card
  belongs to, **required**; its `bank_code` routes card-bill imports to the right layout — see
  [Imports](imports.md#routing-by-the-destinations-bank)), `credit_limit` (`>= 0`, optional),
  `closing_day`, `due_day`, currency.
- **`closing_day` / `due_day` ∈ 1..28** — enforced by CHECK to avoid month-length ambiguity.
- **Currency is fixed** at creation (immutable, like accounts).
- **Archiving** works like accounts: archived card is hidden, rejects business mutations, keeps
  history, is reversible via unarchive.

### Cards screen

The cards list follows the Accounts screen: a heading and New card action above one bordered panel.
Rows combine the name, brand and last digits; the billing cycle moves beneath the name on small
screens, and the configured credit limit stays right-aligned. Clicking a row opens its detail page;
the named ellipsis menu offers Edit, Archive/Unarchive and Delete without navigating. Only archived
rows carry a status tag. Archiving and deletion ask for confirmation naming the card.

Show archived remains available above the list. Desktop holds the table inside the viewport with
a sticky header and a count footer; phones scroll the page without horizontal overflow. Empty
lists explain what is missing. This screen does not calculate available credit or total limits.

The create/edit modal places required identity/account/currency and billing days first, in paired
fields from `sm` up. Brand, last digits and credit limit fold into additional options, initially open
when editing a card with any of those values. The limit shows the selected currency; currency stays
locked when editing. Long forms scroll inside the modal. Defaults, validation and request contracts
remain unchanged.

### Card detail screen

The detail uses the same heading and StatTile figures as account details: card name, brand/final,
billing days, configured and available limits, and an Edit card action reusing the existing form.
Archived cards carry a status tag. Limits remain informational; missing values show a dash and
negative available credit uses the error token.

Statements and installment plans are separate tabs, starting on Statements. Each keeps its filters
and sorting while switching tabs. Status and due-date range stay visible for statements; description
and category stay visible for plans. Amount ranges open in an elevated overlay above the table,
with an active-filter badge and a Clear filters action scoped to that tab. Zero bounds are valid.
Dates display as DD/MM/YYYY while filtering compares ISO dates.

Statement rows combine month and status; due date and remaining amount move into secondary lines
on small screens. Clicking the row or its month opens the existing statement page. Installment rows
combine description/category with paid/total progress, total and remaining amounts. Each tab has
one bordered table, a count footer and a sticky header with internal scrolling on desktop. On
phones the page scrolls without horizontal overflow. Empty results and unavailable cards have
explicit messages. No backend contracts or statement detail/payment flows change.

The statement detail table also adapts to phones: description and amount remain as columns,
while date, installment, category, kind and status move below the description as their columns
hide. Long descriptions truncate; header actions wrap and summary figures stack on phones.

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

The payment form pairs the required account and amount and folds optional date/description
under a named section. It keeps the remaining balance pre-filled and sends null for optional
fields left empty. The 640 px modal stacks on phones and has a scrolling body when needed.

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

## Statement detail screen

The detail follows the shared screen patterns: reference month, closing/due dates and status in the
heading, total/paid/remaining figures, and a compact transaction table. Payment remains the primary
action; closing, reopening and confirmed cashless settlement preserve their existing availability.
Totals use the card currency, including statements with no transactions.

Search, period and status stay visible. Kind, category and amount bounds fold into an overlay with
an active-filter count. Filters are controlled, survive closing the overlay and clear together.
The table retains multi-column sorting, shows all filtered entries with a count footer, and holds
its header in the desktop scroll panel. Phones scroll the page and show secondary metadata below
the description. Attachments open in a separate drawer and load only when requested; the existing
upload, queue-assignment, opening and removal actions remain available there. Attachment controls
wrap on narrow screens; long filenames and notes truncate while retaining their full title text.

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
