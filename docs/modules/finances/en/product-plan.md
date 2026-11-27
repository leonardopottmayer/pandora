# Finances — Product Plan (remaining work)

> **Status:** investments part 1 (the portfolio core) is **built** (2026-11-23) and documented in [Investments](investments.md). What is left are parts 2 to 6 below.
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)

---

## Investments

### Goal

Know **what I own, where and in what**, with an up-to-date value, across every bank, broker and
country: fixed income (CDB, LCI, LCA, Tesouro…), stocks, REITs (FII), funds, ETFs abroad
(Avenue/Nasdaq), currencies (USD, EUR) and crypto. Flexible enough to take any asset class used today
or later.

### The parts and their order

| # | Part | What it solves | Depends on |
|---|---|---|---|
| 1 | **Portfolio core** — built, see [Investments](investments.md) | Assets, positions per account, movements (buy, sell, contribution, redemption, income/dividend, come-cotas, split), average cost, reported values | — |
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

### Open questions

- **Come-cotas and average cost:** confirm against the official rule before part 4 whether the tax paid
  changes the cost basis. Part 1 only records it.
- **Partial redemption share:** the proportional rule uses the position's current value (INV-9); check it
  against a real bank statement while using it.
