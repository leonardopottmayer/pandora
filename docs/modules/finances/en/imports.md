# Imports

[← Back to index](../README.md) · Aggregates: `ImportFile`, `ImportRow`, `ImportLayout` · Tables: `fin012`–`fin014` · API: `/imports`, `/import-layouts`

---

## Business context

Users bring existing history into Pandora by uploading **bank files** — **OFX** (bank statements and
card statements) and **CSV** (extratos and card faturas). The pipeline parses the file, deduplicates
and reconciles each row against what's already in the ledger, and drops **suggestions** into the
[inbox](recurrences-and-inbox.md) for review. Nothing is posted automatically — the user approves,
edits, rejects, or links each row.

## Pipeline

```
upload → ImportFile(received) → [ImportParsingService job] → parse rows → dedup/reconcile
      → generate suggestions (PendingTransaction) → user reviews in inbox → completed
```

- **Upload** (`POST /imports`, multipart): destination is an **account XOR a card** and an optional
  **cutoff date**. The layout is **routed by the destination's bank + file format + account/card**
  (see [Routing by the destination's bank](#routing-by-the-destinations-bank)); it only falls back to
  content auto-detection when the destination has no bank set. Creates an `ImportFile` in
  `received`, storing the raw bytes (`file_content`) and a `correlation_id` that ties the whole
  import's audit together. `file_hash` (sha256) is stored **informationally** — the UI can warn about
  a duplicate upload, but re-importing the same file is allowed on purpose (to rebuild suggestions).
- **Parsing job** (`ImportParsingService`): picks up `received` files, chooses a parser by format,
  extracts `ImportRow`s (raw preserved in `raw_data`, structured in `parsed_payload`). A row failure
  does not abort the file — the row is marked `error`. Counters (`total/parsed/error/duplicate/
  suggestion_rows`) are updated as it runs; `retry_count` supports retry.
- **Cutoff date:** rows dated **before** `cutoff_date` are skipped (no suggestion) — so importing a
  long historical file at go-live doesn't flood the inbox with pre-onboarding movements. NULL = import
  everything.
- **Status:** `received → parsing → completed` (or `failed`, or `aborted` when the user discards).
  `POST /imports/{id}/abort`, `POST /imports/{id}/retry`.

## Import file status & row status

- **ImportFile:** `received | parsing | completed | failed | aborted`.
- **ImportRow:** `pending | suggestion-created | skipped | error`.

## Layouts (`fin012`)

A **layout** is a parsing profile stored as `config` JSONB, so parsers stay generic and per-bank
quirks live in data. System layouts have `user_id NULL` and a globally unique `layout_code`. Each
layout carries a **`bank_code`** (COMPE) alongside `bank_name`, used for routing.

Seeded system layouts (Brazilian banks):

| Layout code | Bank | COMPE | Format | Target |
|---|---|---|---|---|
| `viacredi-ofx` | Viacredi | 085 | OFX | account |
| `viacredi-account-csv` | Viacredi | 085 | CSV | account |
| `nubank-card-ofx` | Nubank | 260 | OFX | card |
| `nubank-account-ofx` | Nubank | 260 | OFX | account |
| `nubank-card-csv` | Nubank | 260 | CSV | card |
| `nubank-account-csv` | Nubank | 260 | CSV | account |
| `inter-ofx` | Banco Inter | 077 | OFX | account |
| `itau-account-ofx` | Itaú | 341 | OFX | account |
| `itau-card-csv` | Itaú | 341 | CSV | card |

### Routing by the destination's bank

An account stores its bank's COMPE code in `fin001.bank_code` — a value from the `Bank` registry
(domain; 077 Inter, 085 Viacredi, 260 Nubank, 341 Itaú). A card has no bank of its own: it belongs to
an account (`fin006.account_id`) and inherits that account's `bank_code`. On upload, the
`IImportLayoutResolver` picks the layout by the **`(bank_code, file_format, account_type)` key**: it
detects the format (ofx/csv) from the extension/content, derives account/card from the destination
(for a card, the bank comes from the account it belongs to), and looks up the system layout with that
combination (unique index `uq_fin012_system_bank_route`).

- **Match** → use that layout (deterministic, no sniffing).
- **No match** (destination has no bank, or no layout for that combination) → **fall back** to the
  `ILayoutDetector` (content auto-detection, the previous behavior).

This replaces content sniffing as the primary path; the detector becomes a safety net. The matrix of
what each bank supports is derived from the layouts (`GET /import-layouts`, fields `bankCode`,
`fileFormat`, `accountType`), consumed by the frontend to offer the bank on account/card forms.

**OFX config** captures quirks: `descriptionField` (NAME/MEMO), `amountIsAlwaysAbsolute`,
`invertAmount`, `treatPaymentAsDebit`, and a `quirks` list (`multiple-banktranlist`, `comma-decimal`,
`empty-fitid`, `fitid-shared-with-secondary`, `no-closing-tags`, …).

**CSV config** captures structure: `delimiter`, `encoding`, `isMultiSection`, `dateColumn`,
`dateFormat`, `amountColumn`, `amountDecimalSeparator`, `descriptionColumn`, `identifierColumn`,
`signColumn` + `creditSignValue`/`debitSignValue`, `amountIsAlwaysPositive`,
`positiveAmountIsExpense`, and **`installmentPatterns`** (regexes to detect a parcela in the
description, e.g. `(\d+)/(\d+)`, `- Parcela (\d+)/(\d+)`).

User layouts (`user_id` set) are reserved for a future phase; today only system layouts are seeded.

## Deduplication & reconciliation (three levels)

`IDuplicateDetector` classifies each row against existing import rows and transactions (design
decision D9). A dedup key is a sha256 of identity fields: `dest:fitid:<external_id>` when a FITID/
identifier exists, otherwise a content hash `dest:hash:<date>:<amount>:<normalized-desc>`.

| Level | Trigger | Behavior |
|---|---|---|
| **Certain** (`certain`) | Same FITID/`external_id`, or same dedup key, already imported for this user + destination. | A suggestion is still generated but **linked** to the existing entity (`matched_transaction_id`/pending). The UI surfaces the relationship; the user decides. A user-confirmed manual link wins when resolving the link. |
| **Suspected** (`suspected`) | No exact match, but a transaction within **±2 days** and the **same amount** (tolerance 0.01) exists. | A **flagged** suggestion (`duplicate_of_transaction_id`); the user approves (post anyway) or rejects/links. |
| **New** (`new`) | No match. | A normal suggestion. |
| **Matched** (`matched`) | The row is reconciled with an *expected* pending entry (recurrence-generated / scheduled). | A **confirmation** suggestion (`matched_pending_transaction_id`) — approving confirms/links the expected entry instead of duplicating. |

The ±2-day window and amount tolerance are current heuristics (calibration is a known open point).

## Installment marker extraction (implemented)

For a card statement CSV/OFX that carries only the current installment (e.g. `LOJA X 03/12`, R$ 100),
the parser applies the layout's `installmentPatterns` to extract `installment_number = 3` and
`installment_count = 12` into `parsed_payload` and onto the `ImportRow`/suggestion. This part is
implemented (`OFXParser`/`CsvParser`). The user can see these values on review.

That is as far as it goes today: **approving** such a suggestion (`ApprovePendingTransactionCommand`)
ignores `installment_number`/`installment_count`/`matched_installment_plan_id` and just creates one
plain transaction — there is no matcher that links it to an existing `InstallmentPlan`, no creation of
an `origin = import` plan, and no generation of the projected future installments. The design for that
full flow is:

1. On approval, an installment matcher would look for an existing plan on the card with the same
   `normalized_description`, same count, a compatible per-installment value, and a free position:
   found → the approved transaction becomes installment N of that plan; not found → a new plan with
   `origin = import`, `total_amount = value × count` (`total_is_estimate = true`), and a retroactively
   inferred `first_reference_month`.
2. **Future installments** (N+1..count) would be generated as transactions with `origin = projection`
   on the following statements.
3. **Past installments** (1..N−1) would **not** be generated automatically.
4. Next month's import of `LOJA X 04/12` would reconcile with the projected installment instead of
   duplicating it.

None of this (steps 1-4) is implemented — see [Installments](installments.md) and
[Implementation Status](implementation-status.md). The `EntryOrigin.Projection` value and the
`ImportRow`/`PendingTransaction` fields that would support it (`matched_installment_plan_id`, etc.)
already exist in the schema, unused.

## API

| Method | Route | Purpose |
|---|---|---|
| POST | `/imports` | Upload (multipart: destination, optional layout, optional cutoff) |
| GET | `/imports` | List import files |
| GET | `/imports/{id}` | Status + counters |
| GET | `/imports/{id}/rows` | Rows with raw data + dedup outcome |
| POST | `/imports/{id}/abort` | Discard |
| POST | `/imports/{id}/retry` | Re-run parsing |
| GET | `/import-layouts` | System layouts |

## Audit events

The import pipeline records events under the file's `correlation_id` (file received, parsing, row
outcomes, completion). Suggestions produced from rows use the standard `pending.created` events; the
whole import's trail is retrievable by `correlation_id`. See
[Audit & Provenance](audit-and-provenance.md).
