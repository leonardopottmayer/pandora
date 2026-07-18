# Accounts

[← Back to index](../README.md) · Aggregate: `Account` · Table: `fin001_account` · API: `/accounts`

---

## Business context

An **account** is a balance repository owned by the user: cash/wallet, checking, savings,
international, crypto, investment, or other. It is where money actually sits. A credit card is
**not** an account (see [Cards & Statements](cards-and-statements.md)). The account's **balance is
never stored** — it is the signed sum of its posted transactions (design decision D1).

## Rules

- **Types** (`AccountType`): `cash`, `checking`, `savings`, `international`, `crypto`, `investment`,
  `other`.
- **Currency** (`CurrencyCode`): an ISO 4217 fiat code or a crypto ticker, validated by shape
  (3–10 uppercase letters), normalized to upper-case. **Fixed at creation** — there is no mutator,
  so it can never change. Rationale: changing an account's currency would invalidate every entry's
  currency and the derived balance.
- **Name** is unique per user (`uq_fin001_user_name`).
- **Type is editable**, but **currency is not** (both are absent from the update path in the design;
  in the current aggregate `Update` re-accepts `type` but not `currency`).
- **Archiving** (`archived_at`): a soft retirement. An archived account:
  - is hidden from the default listing,
  - **rejects business mutations** (`Update` returns `false` when archived),
  - **rejects new transactions**,
  - keeps its full history,
  - can be **unarchived** back into active use.
- **Opening balance:** the account's starting balance is recorded as an `opening-balance`
  transaction (pure ledger, auditable), not a field. It is created together with the account when an
  opening balance is provided.

## Balance

Two figures, both derived (see [Transactions](transactions.md#balance)):
- **Posted balance** = Σ `signed(kind, amount)` over the account's `posted` transactions.
- **Projected balance** = posted + `pending` (scheduled/future) transactions.

## API

| Method | Route | Purpose |
|---|---|---|
| GET | `/accounts` | List (ordered by `display_order`; filter archived) |
| GET | `/accounts/{id}` | Detail |
| POST | `/accounts` | Create (optionally with opening balance) |
| PUT | `/accounts/{id}` | Update mutable config |
| DELETE | `/accounts/{id}` | Delete — **only if the account has no history** (see [Reversibility](reversibility.md#delete-guards)) |
| POST | `/accounts/{id}/archive` | Archive |
| POST | `/accounts/{id}/unarchive` | Unarchive |
| GET | `/accounts/{id}/balance` | Posted + projected balance |
| GET | `/accounts/{id}/transactions` | Account statement (filters) |
| PUT | `/accounts/{id}/tags` | Replace the account's tag set |

## Audit events

`account.created`, `account.updated`, `account.archived`, `account.unarchived` — all with a field diff.
