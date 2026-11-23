# Finances — Product Plan (remaining work)

> **Status:** investments in **planning** (started 2026-11-23). Nothing here is built yet. Built
> features live in the topic docs and in [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)

---

## Investments

### Goal

Know **what I own, where and in what**, with an up-to-date value, across every bank, broker and
country: fixed income (CDB, LCI, LCA, Tesouro…), stocks, REITs (FII), funds, ETFs abroad
(Avenue/Nasdaq), currencies (USD, EUR) and crypto. Flexible enough to take any asset class used today
or later.

### Decisions so far

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

### The parts and their order

| # | Part | What it solves | Depends on |
|---|---|---|---|
| 1 | **Portfolio core** | Assets, positions per account, movements (buy, sell, contribution, redemption, income/dividend, come-cotas, split), average cost, reported values | — |
| 5 | **Data entry** (first: manual + one source) | Manual entry, then one import source | 1 |
| 2 | **Quotes and indexes** | Stock/FII/crypto prices, FX, CDI/IPCA series; fixed income on the curve | 1 |
| 6 | **Reports** | Allocation by class/institution/currency, net-worth evolution, returns vs CDI | 1, 2 |
| 3 | **Multi-currency and abroad** | Avenue/Nasdaq, USD/EUR, crypto, all consolidated in BRL | 1, 2 (FX) |
| 4 | **Taxes** | Income tax per class, loss offsetting, monthly DARF, annual *Bens e Direitos* report | 1, with reliable average cost and sales |

Taxes come last on purpose: they are only right when average cost and sales are right.

### Ideas to keep (not decided yet)

**Flexibility.** Asset class, currency, institution and income type are data, not code, so a new
class (debenture, CRI, Tesouro) needs no structural migration.

**Data entry is a pipeline of connectors**, like the existing statement import (layout per source,
inbox to review). Candidate sources, one connector each:

- manual entry;
- broker trade confirmations (*notas de corretagem*, PDF);
- position and movement statements (CSV/XLSX) from banks and brokers;
- B3 investor area (*Área do Investidor*) exports;
- crypto exchange APIs;
- Avenue / foreign broker statements;
- Open Finance (Pluggy or similar), later.

**Quotes and indexes (part 2).** Prices for B3 stocks/FIIs/ETFs, crypto and foreign tickers; FX
(PTAX); CDI, Selic and IPCA series. Fixed income on the curve has pitfalls: business days, grace
periods, IOF in the first 30 days, pre- vs post-fixed. Funds, COE and pension stay reported.

**Taxes (part 4).** Rules change over time, so each rule is isolated and **dated**, and every one is
checked against the official source before it is built, never from memory. Topics to cover:
regressive table for fixed income, LCI/LCA exemption, stock sales exemption up to a monthly limit,
day trade, FII income and sales, crypto, foreign investments (rules changed in 2024 with Law
14.754/2023), come-cotas, loss offsetting per category, monthly DARF, *Bens e Direitos* for the annual
return.

**Returns (B).** Needs historical quotes from the first purchase, or at least the dated reported
values INV-2 already keeps.

---

## Part 1 — Portfolio core: specification

> **Status:** approved 2026-11-23. Implements INV-1 to INV-8.

### Concepts

| Term | Meaning |
|---|---|
| **Asset class** | What kind of investment (CDB, stock, FII…). Seeded system classes plus the user's own. Each has a **behavior**: `unit` (quantity and average cost) or `application` (fixed income: date, rate, maturity). |
| **Asset** | A thing the user owns: name, optional code (ticker or ISIN), optional issuer, currency, class. |
| **Position** | An asset inside an account. `unit` assets: one open position per asset per account. `application` assets: one position per application. Open while it holds something, closed when it reaches zero. |
| **Movement** | Something that happened to a position: opening, buy, sell, redemption, maturity, income, split, come-cotas. Keeps date, quantity, unit price, gross amount, costs and tax withheld. |
| **Valuation** | What a position is worth at a date, with its source. The latest one is the current value. |

Rules that hold everywhere:

- An asset's currency must equal the account's currency (conversion is part 3).
- An archived account takes no new positions or movements; an account with positions cannot be deleted.
- Every write records an audit event (`fin016`), like the rest of the module.

### Data model

All new tables, so the migrations only add (no change to existing data). Enum values use hyphens.

**`fin020_asset_class`**

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NULL | NULL = system class (seeded, read-only) |
| `code` | varchar(40) NULL | system classes only (`cdb`, `stock`…) |
| `name` | varchar(60) NOT NULL | unique per user (system names are unique among themselves) |
| `behavior` | varchar(20) NOT NULL | `unit\|application` |
| `archived_at` | timestamptz NULL | user classes only |

Seeded: `stock`, `fii`, `etf`, `bdr`, `fund`, `pension`, `crypto`, `currency` (`unit`); `cdb`, `lci`, `lca`,
`treasury`, `debenture`, `coe` (`application`); `other` (`unit`).

**`fin021_asset`**

| Column | Type | Notes |
|---|---|---|
| `id` / `user_id` | uuid | |
| `asset_class_id` | uuid NOT NULL → fin020 | |
| `name` | varchar(100) NOT NULL | unique per user |
| `code` | varchar(20) NULL | ticker or product code; unique per user and class when set |
| `isin` | varchar(12) NULL | |
| `issuer` | varchar(100) NULL | e.g. Banco Inter |
| `currency` | varchar(10) NOT NULL | same `CurrencyCode` as accounts |
| `archived_at` | timestamptz NULL | |

**`fin022_position`**

| Column | Type | Notes |
|---|---|---|
| `id` / `user_id` | uuid | |
| `account_id` | uuid NOT NULL → fin001 | |
| `asset_id` | uuid NOT NULL → fin021 | |
| `behavior` | varchar(20) NOT NULL | copied from the class at creation: `unit\|application` |
| `status` | varchar(20) NOT NULL | `open\|closed` |
| `quantity` | numeric(28,10) NULL | `unit` only; fractions allowed (crypto, fund quotas) |
| `average_cost` | numeric(28,10) NULL | `unit` only; per unit, costs included |
| `invested_amount` | numeric(18,2) NULL | `application` only; what is still invested |
| `applied_on` | date NULL | `application` only |
| `index` | varchar(20) NULL | `application`: `cdi\|selic\|ipca\|pre\|other` |
| `index_percent` | numeric(9,4) NULL | e.g. 110 for 110% of CDI |
| `spread_rate` | numeric(9,4) NULL | % a year: IPCA + 6.5, or the pre-fixed rate |
| `maturity_on` | date NULL | `application` |
| `closed_at` | timestamptz NULL | |

Constraints: `unit` rows have quantity and average cost and no application fields; `application` rows the
reverse. One **open** `unit` position per (account, asset): partial unique index. In part 1 the rate fields
are informational; part 2 uses them for the curve.

**`fin023_position_movement`**

| Column | Type | Notes |
|---|---|---|
| `id` / `user_id` / `position_id → fin022` | uuid | |
| `kind` | varchar(20) NOT NULL | `opening\|buy\|sell\|redemption\|maturity\|income\|split\|come-cotas` |
| `occurred_on` | date NOT NULL | |
| `quantity` | numeric(28,10) NULL | units bought, sold or taken by come-cotas |
| `unit_price` | numeric(28,10) NULL | |
| `gross_amount` | numeric(18,2) NULL | |
| `costs` | numeric(18,2) NOT NULL DEFAULT 0 | brokerage, fees |
| `tax_withheld` | numeric(18,2) NOT NULL DEFAULT 0 | IR/IOF withheld at source |
| `net_amount` | numeric(18,2) NULL | what reached or left the cash |
| `income_kind` | varchar(30) NULL | `income`: `dividend\|interest-on-equity\|rent\|coupon\|other` |
| `split_factor` | numeric(18,8) NULL | `split`: 2 = 1→2 split, 0.1 = 10→1 reverse split |
| `realized_result` | numeric(18,2) NULL | `sell`/`redemption`/`maturity`: gain or loss against cost |
| `quantity_before` / `average_cost_before` / `invested_amount_before` | numeric NULL | the position before this movement, for undo |
| `transaction_id` | uuid NULL → fin008, unique | the linked ledger entry (INV-7) |
| `notes` | varchar(255) NULL | |

**`fin024_position_valuation`**

| Column | Type | Notes |
|---|---|---|
| `id` / `user_id` / `position_id → fin022` | uuid | |
| `valued_on` | date NOT NULL | unique per position |
| `value` | numeric(18,2) NOT NULL | total value of the position on that date |
| `source` | varchar(20) NOT NULL | `manual\|import\|quote` |

Migrations: `create-table-fin020-asset-class`, `insert-fin020-system-asset-classes`, `create-table-fin021-asset`,
`create-table-fin022-position`, `create-table-fin023-position-movement`, `create-table-fin024-position-valuation`.
No existing table changes.

### Movement rules

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
| `redemption` | gross, tax, costs | share = gross ÷ current value; v −= v·share; realized = gross − v·share; whole value closes | `investment-redemption`: gross − tax − costs |
| `maturity` | gross, tax, costs | total redemption; closes | `investment-redemption` |
| `income` | gross, tax, kind (`coupon`) | none | `yield` |

- A position starts with `opening` (no cash: the money left in the past, INV-6) or `buy` (cash leaves now).
  A new fixed-income application is always a new position.
- **Undo** removes only a position's **latest** movement: it restores the `*_before` values and voids the
  linked ledger entry with the existing void. Undoing the only movement deletes the position.
- The linked ledger entry cannot be edited, voided or reversed from the ledger: the API answers with an
  error that points to the position.
- Ledger entries created by movements skip the rule that limits those kinds to `investment` accounts
  (INV-8). Entries typed by hand keep the rule.
- The tax fields are **typed**, as the bank or broker shows them; part 1 computes no tax.

### Value and totals

- **Current value** of a position = its latest valuation. Without one: cost (`q·c`, or `v`), shown as
  "no value reported".
- A value older than **30 days** is shown as out of date.
- **Account total** = ledger cash balance + current value of its open positions. `GET /accounts/{id}/balance`
  gains `positionsValue` and `total`; the existing fields keep their meaning.
- Totals are **per currency**; nothing is converted (part 3).

### API (`/api/v{n}/finances`)

| Method | Route | Does |
|---|---|---|
| GET / POST | `/asset-classes` | List (system + own) / create |
| PUT | `/asset-classes/{id}` | Rename (own only) |
| POST | `/asset-classes/{id}/archive` · `/unarchive` | Own only |
| GET / POST | `/assets` | List (search, class) / create |
| PUT | `/assets/{id}` | Edit |
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

### Screens

Following `client-web/DESIGN.md`:

- **Investments page** (`/finances/investments`, new menu item):
  - figures: total per currency, next maturity;
  - allocation by class and by account;
  - open positions grouped by account; each row shows asset, quantity or amount invested, average cost,
    current value and its date (out of date marked);
  - primary button **Update values**: a table of every open position, one value field per row, saved at
    once with one date.
- **Account page**: a **Positions** tab; the header shows cash, positions and total.
- **Position drawer**: data, movements, valuation history; actions: new movement, report value, undo
  latest movement.
- **Forms**: new position (account, asset picked or created inline, first movement), new movement (fields
  by kind), asset classes (small page reached from the Investments page).

### Out of part 1

Imports from any source (part 5, next), automatic quotes and the curve (part 2), conversion to BRL (part 3),
returns (B) and taxes (C), maturity alerts on Telegram, Assistant tools.

### Tests

- Domain: average cost on buy, realized result on sell, proportional redemption, split, come-cotas, undo
  restoring the previous state, closing at zero.
- Handlers: the linked ledger entry and its amount for each kind, the ledger guard, currency mismatch,
  archived account.
- Integration (Testcontainers): position → movement → ledger → undo round trip.
- Front: the forms send the right body per kind; the Update values screen.

### Delivery

Four slices, in order. Each one is built and tested without a commit, reviewed by the user (in the
browser when it has screens), and committed only after approval.

| # | Slice | Contents | Done when |
|---|---|---|---|
| I1 | **Domain and schema** | Migrations `fin020`–`fin024` and the seed of system classes; `AssetClass`, `Asset` and `Position` aggregates (the position owns its movements and valuations), the movement rules and undo; repositories and EF mappings | Domain tests for every rule pass; apply → rollback → apply runs on the local database |
| I2 | **Application and API** | Commands and queries, the linked ledger entry per kind, the ledger guard, the INV-8 exception, `positionsValue`/`total` on the account balance, `/investments/summary`, `finances.investments.*`, audit events | Handler tests and the Testcontainers round trip pass |
| I3 | **Screens** | Investments page, Positions tab and account header, position drawer, position and movement forms, asset classes page | Typecheck, lint and front tests pass; checked in the browser |
| I4 | **Update values and docs** | The bulk Update values screen; the spec moves to the topic docs (`investments.md`, data model, API reference, implementation status) and leaves this plan | Checked in the browser; this plan keeps only parts 2 to 6 |

### Open questions

- **Come-cotas and average cost:** confirm against the official rule before part 4 whether the tax paid
  changes the cost basis. Part 1 only records it.
- **Partial redemption share:** the proportional rule uses the latest reported value; check it against a
  real bank statement while testing.
