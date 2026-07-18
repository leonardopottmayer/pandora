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

- **Upload** (`POST /imports`, multipart): destination is an **account XOR a card**, an optional
  layout (auto-detected if omitted), and an optional **cutoff date**. Creates an `ImportFile` in
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
quirks live in data. System layouts have `user_id NULL` and a globally unique `layout_code`. The
`ILayoutDetector` auto-picks a layout for an uploaded file when none is supplied.

Seeded system layouts (Brazilian banks):

| Layout code | Bank | Format | Target |
|---|---|---|---|
| `viacredi-ofx` | Viacredi | OFX | account |
| `viacredi-account-csv` | Viacredi | CSV | account |
| `nubank-card-ofx` | Nubank | OFX | card |
| `nubank-account-ofx` | Nubank | OFX | account |
| `nubank-card-csv` | Nubank | CSV | card |
| `nubank-account-csv` | Nubank | CSV | account |
| `inter-ofx` | Banco Inter | OFX | account |
| `itau-account-ofx` | Itaú | OFX | account |
| `itau-card-csv` | Itaú | CSV | card |

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

## Installment detection & projection

For a card statement CSV/OFX that carries only the current installment (e.g. `LOJA X 03/12`, R$ 100):

1. The parser applies the layout's `installmentPatterns` to extract `installment_number = 3` and
   `installment_count = 12` into `parsed_payload` and the suggestion. The user can correct or zero
   these on review (false positive — a description that merely *looks* like a fraction).
2. On approval, an installment matcher looks for an existing plan on the card with the same
   `normalized_description`, same count, a compatible per-installment value, and a free position:
   - **found** → the approved transaction becomes installment N of that plan;
   - **not found** → creates a plan with `origin = import`, `total_amount = value × count`
     (`total_is_estimate = true`), and a retroactively inferred `first_reference_month`
     (current statement month − (N−1) months); the approved transaction is installment N.
3. **Future installments** (N+1..count) are generated as `pending` transactions with
   `origin = projection` on the following statements — so the user sees the upcoming commitment.
   They count toward a statement's *projected* total, not the posted `total_amount` or any balance.
4. **Past installments** (1..N−1) are **not** generated automatically.
5. Next month's import of `LOJA X 04/12` reconciles with the projected installment (same plan,
   position 4) → a confirmation suggestion; approving posts the projection with the real values.
   Nothing duplicates. See [Installments](installments.md).

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
