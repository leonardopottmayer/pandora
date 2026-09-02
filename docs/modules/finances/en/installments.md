# Installments (Parcelamento)

[← Back to index](../README.md) · Aggregate: `InstallmentPlan` · Table: `fin009_installment_plan` · API: `/installment-plans`, `/cards/{id}/installment-plans`

---

## Business context

Buying in installments (*parcelado*) is essential in Brazil. A single card purchase split into N
installments is modeled as **one plan + N transactions**, one per consecutive statement. Each
installment is a real, committed `expense` charge on its own statement.

Only **manually created** plans exist today. The `InstallmentPlan` aggregate's own XML doc is explicit
about this: *"In this phase only manual plans exist... import-inferred plans (estimated total,
projected future installments) arrive in phase 10."* The schema and `Origin` value object already
allow an `import` origin, and the OFX/CSV parsers already extract an installment marker from the
description into `ImportRow` (see [Imports](imports.md#installment-marker-extraction-implemented)),
but nothing yet turns that into an `InstallmentPlan` — approving such a suggestion just creates a
single plain transaction. See [Implementation Status](implementation-status.md).

## Rules

- **Minimum 2 installments** (`InstallmentCount >= 2`, `MinInstallments = 2`).
- **Origin:** only `manual` is created today — by the user, via `InstallmentPlan.CreateManual`.
  Installments **sum exactly** to `total_amount` (`total_is_estimate = false`). The `import` origin
  value exists in the domain/schema for a future phase (inferred from a bank file where only the
  current installment's value is known: `total_is_estimate = true`, `total_amount = value × count`)
  but no code path creates it yet.
- **Cent-rounding split** (`SplitAmount`): the total is divided into cents-rounded parts, with any
  rounding remainder placed on the **first** installment so the parts sum back exactly.
  Example: `1000.00` in 3× → `333.34 / 333.33 / 333.33`.
- **`normalized_description`**: the description stripped of its installment marker (`3/12`, `03/12`,
  `PARC 3/12`, `3 de 12`) and lower-cased/whitespace-collapsed. Written on every plan (manual today)
  as the future matching key for reconciling imported installments — not consumed anywhere yet.
- **`first_reference_month`** (`yyyy-MM`): the reference month of the first installment's statement.

## Creating an installment purchase

`POST /transactions` with `installments = N` (N ≥ 2):
1. Creates an `InstallmentPlan` (`origin = manual`).
2. Creates N `expense` transactions (`installment_number` 1..N) distributed across consecutive
   statements via `StatementResolver`, using the deterministic cent split.
3. All of it is atomic; the affected statements' totals are recomputed in the same DB transaction.

## Read model

`InstallmentPlanAssembler` builds the plan view: each installment with its statement's reference
month and status, the **remaining amount** (sum of not-yet-paid, non-void installments), and the
count of **paid** installments (installments whose statement is `paid`). Void installments are
excluded from both figures.

## Projections (planned, not implemented)

The design calls for an import-inferred plan to generate its **future** installments (N+1..count) as
transactions with `origin = projection` on the following statements, so the user sees the commitment
ahead of time. The `EntryOrigin.Projection` value already exists in code for this, but nothing creates
a transaction with it today — it is unused. See [Imports](imports.md) and
[Implementation Status](implementation-status.md).

## Cancelling

Voiding an installment requires an explicit decision — void the single installment, or void the
whole plan (which cancels installments still on **open** statements; installments on closed/paid
statements are not cancellable). See [Reversibility](reversibility.md).

## API

| Method | Route | Purpose |
|---|---|---|
| GET | `/installment-plans/{id}` | Plan detail (read model) |
| GET | `/cards/{id}/installment-plans` | Plans of a card |

Installment purchases are created through `/transactions` (with `installments`), not a dedicated
endpoint.

## Audit events

`installment-plan.created` (with origin), and each installment carries the normal
`transaction.created` event correlated to the plan.
