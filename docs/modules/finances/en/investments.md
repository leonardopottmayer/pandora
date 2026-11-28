# Investments

[← Back to index](../README.md) · Related: [Accounts](accounts.md), [Transactions](transactions.md), [Data Model](data-model.md#fin020_system_asset_class), [Product Plan](product-plan.md)

> **Status:** part 1, the portfolio core, is built (2026-11-23): positions, movements, reported values and the screens. Quotes, imports, conversion between currencies, returns and taxes are the next parts, in the [Product Plan](product-plan.md).

## Business context

Know **what I own, where and in what**, with an up-to-date value, across every bank, broker and
country: fixed income (CDB, LCI, LCA, Tesouro…), stocks, REITs (FII), funds, ETFs abroad
(Avenue/Nasdaq), currencies (USD, EUR) and crypto. Flexible enough to take any asset class used today
or later.

## Design decisions

| # | Decision | Why |
|---|---|---|
| INV-1 | Build **A, net worth** first: positions, movements and values. **B, returns** (vs CDI/IPCA) and **C, taxes** (capital gains, DARF, annual return) are extensions on top, not part of the first delivery. | A delivers value fast and is the base B and C need. |
| INV-2 | From day one, A keeps what B and C will need: **every movement with date, quantity, unit price and costs** (brokerage, fees, IOF), and **the value of each position at a date**, not only the latest value. | Returns and capital gains are computed from these. Storing only balances would make B and C impossible to backfill. |
| INV-3 | **Positions live inside accounts.** A bank or broker stays a Finances account; its cash balance stays in the ledger, and its positions hang from the account. Buying moves cash into a position; selling moves it back. Account total = cash + positions. | That is how banks and brokers work, and it reuses transfers, imports and the inbox. |
| INV-4 | **A position's current value is reported**, typed or imported from the institution's position report, stored as a value at a date. Automatic quotes come later (part 2) and only *generate* the same dated values. | Works for every class, including ones without a public quote (funds, COE, pension). Halves the first delivery. |
| INV-5 | **What a position is:** stocks, FIIs, ETFs, crypto and currencies are **one position per asset per account**, with average cost. Fixed income (CDB, LCI, LCA, Tesouro…) is **one position per application**, with its own date, rate, index (CDI, IPCA, pre-fixed), maturity and reported value; the screen can group applications by product or issuer. | Matches how banks report fixed income, so imported position reports match line by line; fixed-income tax (C), maturity alerts and the curve (part 2) all need the per-application data. |
| INV-6 | **The portfolio starts from today's snapshot.** Each position gets an **opening**: quantity and average cost at a date for unit-based assets; application date, amount invested, rate, maturity and current value for fixed income. New movements are recorded from there. Full history can replace an opening later, once importers exist (part 5). | Average cost is what the tax rules use, so a correct opening is enough for C; everything can be entered in an afternoon from the institutions' position reports. |
| INV-7 | **Movements drive the cash.** Each movement that moves money creates a linked ledger entry on the same account: buy → `investment-contribution` (amount + costs); sell, redemption, maturity, amortization → `investment-redemption` (net); income → `yield` (net). Opening, come-cotas and splits create none. The linked entry is changed or undone only through its movement, like a statement payment. Account total = ledger cash + latest value of open positions. | Keeps the ledger as the single source of truth for cash, so positions and cash never diverge. |
| INV-8 | **Any account can hold positions**, not only `investment` ones: a CDB bought straight from the Inter checking account lives in that account. The old rule that restricts `investment-contribution`/`-redemption`/`yield` to `investment` accounts no longer applies to entries created by movements. `investment` accounts stay for brokers (XP, Avenue). | Banks sell fixed income straight from the checking account; a separate account would mean two steps per purchase and an artificial balance. |
| INV-9 | **The current value is worked out at read time, like an account's balance (D1).** The latest reported value is the anchor; the movements recorded after it move it: a buy adds what was paid, a sale or come-cotas keeps the share of the units left, a redemption takes the gross out. Nothing derived is stored. | Found while checking the screens: a redemption left the value as it was before the money came out. Storing a value per movement (the alternative) would have to be kept in step with every undo. |
| INV-10 | **On the account page, positions are a section above the statement**, not a tab: the page has no tabs. It shows for accounts with positions and for every `investment` account. | The spec said a tab; the page it lands on is a single column. |

## Concepts

| Term | Meaning |
|---|---|
| **Asset class** | What kind of investment (CDB, stock, FII…). Seeded system classes (`fin020`) plus the user's own (`fin025`), kept apart like system and user categories; an asset has exactly one of the two. Each has a **behavior**: `unit` (quantity and average cost) or `application` (fixed income: date, rate, maturity). |
| **Asset** | A thing the user owns: name, optional code (ticker or ISIN), optional issuer, currency, class. |
| **Position** | An asset inside an account. `unit` assets: one open position per asset per account. `application` assets: one position per application. Open while it holds something, closed when it reaches zero. |
| **Movement** | Something that happened to a position: opening, buy, sell, redemption, maturity, income, split, come-cotas. Keeps date, quantity, unit price, gross amount, costs and tax withheld. |
| **Valuation** | What a position is worth at a date, with its source. The latest one is the current value. |

Rules that hold everywhere:

- An asset's currency must equal the account's currency (conversion is part 3).
- An archived account takes no new positions or movements; an account with positions cannot be deleted.
- Every write records an audit event (`fin016`), like the rest of the module.

The tables are `fin020`–`fin025`, described in the [Data Model](data-model.md#fin020_system_asset_class).

## Movement rules

`unit` positions (quantity q, average cost c):

| Kind | Input | Effect on the position | Ledger entry (INV-7) |
|---|---|---|---|
| `opening` | quantity, average cost, date | q, c set; must be the first movement | none |
| `buy` | quantity, unit price, costs | c = (q·c + qty·price + costs) / (q + qty); q += qty | `investment-contribution`: qty·price + costs |
| `sell` | quantity ≤ q, unit price, costs, tax | q −= qty; c unchanged; realized = qty·price − costs − qty·c; q = 0 closes | `investment-redemption`: qty·price − costs − tax |
| `income` | gross, tax, kind | none | `yield`: gross − tax |
| `split` | factor | q ·= factor; c /= factor | none |
| `come-cotas` | quantity taken, tax | q −= qty; c unchanged | none |

`application` positions (invested amount v):

| Kind | Input | Effect on the position | Ledger entry |
|---|---|---|---|
| `opening` | amount invested, applied on, index, rates, maturity, current value | v set; a valuation is stored with the current value | none |
| `buy` | same as opening, without current value | v set; first valuation = v | `investment-contribution`: v + costs |
| `buy` (contribution, after the first movement) | gross, costs | v += gross | `investment-contribution`: gross + costs |
| `redemption` | gross, tax, costs | share = gross ÷ current value; v −= v·share; realized = gross − v·share; whole value closes | `investment-redemption`: gross − tax − costs |
| `maturity` | gross, tax, costs | total redemption; closes | `investment-redemption` |
| `income` | gross, tax, kind (`coupon`) | none | `yield` |

- A position starts with `opening` (no cash: the money left in the past, INV-6) or `buy` (cash leaves now).
  A new CDB is a new position; contributions to the same application (a scheduled plan, cooperative
  capital quotas, a savings box) are `buy` on it, shown as "Contribution".
- **Undo** removes only a position's **latest** movement: it restores the `*_before` values and voids the
  linked ledger entry with the existing void. Undoing the only movement deletes the position.
- The linked ledger entry cannot be edited, voided or reversed from the ledger: the API answers with an
  error that points to the position.
- Ledger entries created by movements skip the rule that limits those kinds to `investment` accounts
  (INV-8). Entries typed by hand keep the rule.
- The tax fields are **typed**, as the bank or broker shows them; part 1 computes no tax.

## Value and totals

- **Current value** of a position = its latest valuation. Without one: cost (`q·c`, or `v`), shown as
  "no value reported".
- A value older than **30 days** is shown as out of date.
- **Account total** = ledger cash balance + current value of its open positions. `GET /accounts/{id}/balance`
  gains `positionsValue` and `total`; the existing fields keep their meaning.
- Totals are **per currency**; nothing is converted (part 3).
- The **current value** follows INV-9. The date shown next to it is the anchor's; the value already includes the movements recorded after it.

## Screens

Following `client-web/DESIGN.md`:

- **Investments page** (`/finances/investments`, new menu item):
  - figures: total per currency, next maturity;
  - allocation by class and by account;
  - open positions grouped by account; each row shows asset, quantity or amount invested, average cost,
    current value and its date (out of date marked);
  - primary button **Update values**: a table of every open position, one value field per row, saved at
    once with one date.
- **Account page**: a **Positions** section above the statement (INV-10); a *Total* figure shows cash plus positions.
- **Position drawer**: data, movements, valuation history; actions: new movement, report value, undo
  latest movement.
- **Forms**: new position (account, asset picked or created inline, first movement), new movement (fields
  by kind).
- **Assets and classes** (page reached from the Investments page): the assets, with class, code, currency and
  how many open positions each has, so one registered without a position still shows; edit (the currency is fixed;
  the class changes only to one with the same behavior, leaving positions alone) and archive from the row menu.
  Then the user's own classes (rename, archive and, while no asset uses them, delete) and the system's,
  read-only. The system ones include a savings account, a scheduled savings plan and cooperative capital
  quotas, all `application`, for balances that take contributions.
- **Update values** (the Investments page's primary button): every open position in one list with a value field each, one date for all; only the rows given a value are sent (`POST /positions/valuations`). The list is mounted only while open, so it starts empty.

## API (`/api/v{n}/finances`)

| Method | Route | Does |
|---|---|---|
| GET / POST | `/asset-classes` | List (system + own) / create |
| PUT | `/asset-classes/{id}` | Rename (own only) |
| POST | `/asset-classes/{id}/archive` · `/unarchive` | Own only |
| DELETE | `/asset-classes/{id}` | Own only, with no asset at all, archived or not |
| GET / POST | `/assets` | List (search, class) / create |
| PUT | `/assets/{id}` | Edit; an optional `assetClassId` moves it to a class with the same behavior |
| POST | `/assets/{id}/archive` · `/unarchive` | An archived asset opens no new position; existing ones stay |
| GET | `/positions` | List (account, status, class) with current value |
| GET | `/positions/{id}` | Detail, movements and valuations |
| POST | `/positions` | Create with its first movement (`opening` or `buy`) |
| POST | `/positions/{id}/movements` | Add a movement |
| DELETE | `/positions/{id}/movements/latest` | Undo the latest movement |
| POST | `/positions/{id}/valuations` | Report a value at a date (same date replaces) |
| POST | `/positions/valuations` | Report many values at once (the "Update values" screen) |
| GET | `/investments/summary` | Totals per currency, by class, by account; upcoming maturities |

Permissions: `finances.investments.read`, `.create`, `.update`, `.delete`. The default role already holds
`finances.*`, so nobody loses access.

## Audit events

| Entity | Events |
|---|---|
| `asset-class` | `asset-class.created`, `.renamed`, `.archived`, `.unarchived`, `.deleted` |
| `asset` | `asset.created`, `asset.updated`, `.archived`, `.unarchived` |
| `position` | `position.created`, `.movement-added`, `.movement-undone`, `.deleted`, `.valued` |
| `transaction` | `transaction.created` / `transaction.voided` for the entries movements create and undo, with `positionMovementId` in the payload |

## Out of part 1

Imports from any source (part 5, next), automatic quotes and the curve (part 2), conversion to BRL (part 3),
returns (B) and taxes (C), maturity alerts on Telegram, Assistant tools.

## Open questions

- **Come-cotas and average cost:** confirm against the official rule before part 4 whether the tax paid
  changes the cost basis. Part 1 only records it.
- **Partial redemption share:** the proportional rule uses the position's current value (INV-9); check it
  against a real bank statement while using it.
