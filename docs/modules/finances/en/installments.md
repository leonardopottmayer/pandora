# Installments (Parcelamento)

[← Back to index](../README.md) · Aggregate: `InstallmentPlan` · Table: `fin009_installment_plan` · API: `/installment-plans`, `/cards/{id}/installment-plans`

---

## Business context

Buying in installments (*parcelado*) is essential in Brazil. A single card purchase split into N
installments is modeled as **one plan + N transactions**, one per consecutive statement. Each
installment is a real, committed `expense` charge on its own statement — they are not projections
(except for import-inferred future ones, see below).

## Rules

- **Minimum 2 installments** (`InstallmentCount >= 2`, `MinInstallments = 2`).
- **Origin:**
  - `manual` — created by the user. Installments **sum exactly** to `total_amount`
    (`total_is_estimate = false`).
  - `import` — inferred from a bank file where only the current installment's value is known
    (`total_is_estimate = true`, `total_amount = value × count`). See [Imports](imports.md).
- **Cent-rounding split** (`SplitAmount`): the total is divided into cents-rounded parts, with any
  rounding remainder placed on the **first** installment so the parts sum back exactly.
  Example: `1000.00` in 3× → `333.34 / 333.33 / 333.33`.
- **`normalized_description`**: the description stripped of its installment marker (`3/12`, `03/12`,
  `PARC 3/12`, `3 de 12`) and lower-cased/whitespace-collapsed. This is the **matching key** used to
  reconcile imported installments to an existing plan (Imports phase). It is filled even on manual
  plans, so future imports can match them.
- **`first_reference_month`** (`yyyy-MM`): the reference month of the first installment's statement.
  For import-inferred plans created from installment N, it is inferred retroactively
  (current statement month − (N−1) months).

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

## Projections (import-inferred future installments)

When an installment plan is inferred from an import, the **future** installments (N+1..count) are
created as `pending` transactions with `origin = projection` on the following statements — so the
user sees the commitment on upcoming statements. They count toward a statement's *projected* total,
not its posted `total_amount` or any balance. Past installments (1..N−1) are **not** generated
automatically. See [Imports → installment detection](imports.md#installment-detection--projection).

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
