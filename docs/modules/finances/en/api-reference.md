# API Reference

[← Back to index](../README.md)

Base path: **`/api/v{version}/finances`**. Every endpoint is authenticated and scoped to the token's
user. A resource owned by another user returns **404** (not 403). Controllers live in
`Presentation/Controllers`.

---

## Accounts — `/accounts`

| Method | Route | Purpose |
|---|---|---|
| GET | `/accounts` | List (filter archived) |
| GET | `/accounts/{id}` | Detail |
| POST | `/accounts` | Create (optional opening balance) |
| PUT | `/accounts/{id}` | Update |
| DELETE | `/accounts/{id}` | Delete (only if no history) |
| POST | `/accounts/{id}/archive` · `/unarchive` | Archive / unarchive |
| GET | `/accounts/{id}/balance` | Posted + projected balance |
| GET | `/accounts/{id}/transactions` | Account statement |
| PUT | `/accounts/{id}/tags` | Replace tag set |

## Cards — `/cards`

| Method | Route | Purpose |
|---|---|---|
| GET / POST | `/cards`, `/cards/{id}` (GET) | List / create / detail |
| PUT / DELETE | `/cards/{id}` | Update / delete (only if no history) |
| POST | `/cards/{id}/archive` · `/unarchive` | Archive / unarchive |
| GET | `/cards/{id}/statements` | Statements |
| GET | `/cards/{id}/installment-plans` | Installment plans |
| GET | `/cards/{id}/available-limit` | Limit − unpaid statements |
| PUT | `/cards/{id}/tags` | Replace tag set |

## Statements — `/statements`

| Method | Route | Purpose |
|---|---|---|
| GET | `/statements/{id}` | Detail + entries |
| POST | `/statements/{id}/pay` | Pay from an account |
| POST | `/statements/{id}/settle` | Cashless write-off (onboarding) |
| POST | `/statements/{id}/close` | Manual close |
| POST | `/statements/{id}/reopen` | Reopen to new purchases |
| PUT | `/statements/{id}/tags` | Replace tag set |

## Transactions — `/transactions`

| Method | Route | Purpose |
|---|---|---|
| GET | `/transactions` | List (rich filters) |
| GET | `/transactions/{id}` | Detail |
| POST | `/transactions` | Create (account/card; `installments`) |
| POST | `/transactions/transfer` | Create a transfer pair |
| PUT | `/transactions/{id}` | Edit cosmetic fields |
| POST | `/transactions/{id}/post` | Post a scheduled entry |
| POST | `/transactions/{id}/void` · `/unvoid` · `/reverse` | Cancel / restore / reverse |
| PUT | `/transactions/{id}/tags` | Replace tag set |

## Installment plans — `/installment-plans`

| Method | Route | Purpose |
|---|---|---|
| GET | `/installment-plans/{id}` | Plan detail |

## Categories

| Method | Route | Purpose |
|---|---|---|
| GET | `/categories/system` | System category tree |
| GET / POST | `/categories`, `/categories` | User categories: list / create |
| PUT | `/categories/{id}` | Update |
| POST | `/categories/{id}/activate` · `/deactivate` | Activate / deactivate |

## Tags — `/tags`

| Method | Route | Purpose |
|---|---|---|
| GET / POST | `/tags` | List / create |
| PUT / DELETE | `/tags/{id}` | Update / delete |
| GET | `/tags/{id}/links` | Links of a tag |
| POST | `/tags/{id}/links` | Add a link |
| DELETE | `/tags/{id}/links/{entityType}/{entityId}` | Remove a link |

## Recurring transactions — `/recurring-transactions`

| Method | Route | Purpose |
|---|---|---|
| GET / POST | `/recurring-transactions`, `/{id}` (GET) | List / create / detail |
| PUT / DELETE | `/recurring-transactions/{id}` | Update / delete |
| POST | `/recurring-transactions/{id}/pause` · `/resume` · `/generate` | Control / on-demand generation |

## Pending transactions (inbox) — `/pending-transactions`

| Method | Route | Purpose |
|---|---|---|
| GET | `/pending-transactions` | Inbox (filters) |
| PUT | `/pending-transactions/{id}` | Edit proposal |
| POST | `/pending-transactions/{id}/approve` · `/reject` · `/link` | Decide |
| POST | `/pending-transactions/approve-batch` | Batch approve |
| POST | `/pending-transactions/transfer` | Build a transfer from two suggestions |

## Imports — `/imports`

| Method | Route | Purpose |
|---|---|---|
| POST | `/imports` | Upload (multipart) |
| GET | `/imports` · `/imports/{id}` | List / status + counters |
| GET | `/imports/{id}/rows` | Rows (raw + dedup) |
| POST | `/imports/{id}/abort` · `/retry` | Discard / re-run parsing |

## Import layouts — `/import-layouts`

| Method | Route | Purpose |
|---|---|---|
| GET | `/import-layouts` | System layouts |

## Audit — `/audit`

| Method | Route | Purpose |
|---|---|---|
| GET | `/audit?entityType=&entityId=` | Entity timeline |
| GET | `/audit?correlationId=` | Everything from one operation |
