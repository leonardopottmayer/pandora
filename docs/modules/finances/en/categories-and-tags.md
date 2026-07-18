# Categories & Tags

[← Back to index](../README.md) · Aggregates: `SystemCategory`, `UserCategory`, `Tag`, `TagLink` · Tables: `fin002`, `fin003`, `fin004`, `fin005`

---

## Categories

There are **two independent categorization dimensions** (design decision D5). A transaction can
carry a system category **and** a user category at the same time — analyses can cross both.

### System categories (`fin002_system_category`)

- **System-maintained, seeded, global** — identical for every user, with no `user_id`.
- **Hierarchical, 2 levels** (parent → child) via `parent_category_id`.
- Typed by `transaction_nature` (`expense` | `income`).
- Each has a globally unique `code`, plus `color`, `icon`, `display_order`, `is_active`, and
  `is_other` (the group's fallback child, e.g. "Other housing").
- Updated by migration without touching user data. `created_by NULL` marks seed rows.
- **Read-only to users** — exposed via `GET /categories/system` (tree; filter by `nature`,
  `includeInactive`). Read through `ISystemCategoryReader`; no behavior/aggregate.

The seed covers the common personal-finance taxonomy: expense groups (Housing, Food, Transport,
Health, Education, Personal Care, Family, Shopping, Entertainment, Travel, Financial Expenses,
Subscriptions, Work Expenses, Pets, …) and income groups (Primary Income, Investment Income, Sales
Income, Support Income, …), each with a set of children including an `is_other` fallback. Specific
system codes referenced by the app include `credit-card-payment` (used on statement payments).

### User categories (`fin003_user_category`)

- Full CRUD, scoped to `user_id`.
- **Hierarchical, 2 levels**: a child cannot have a child; a child's `transaction_nature` must equal
  its parent's.
- Name unique per `(user_id, name, parent_category_id)`.
- **Non-destructive deactivation** (`is_active`): deactivating a category never breaks existing
  entries (the FK stays); it only disappears from *new* entries.
- API `/categories`: `GET` (list), `POST`, `PUT {id}`, `POST {id}/activate`, `POST {id}/deactivate`.

Audit: `category.created`, `category.updated`, `category.activated`, `category.deactivated`.

## Tags (`fin004_tag` / `fin005_tag_link`)

- **Free labels** owned by the user: `name` (unique per user), `color`.
- **Polymorphic link** (`TagLink`): a tag can attach to any of these `entity_type`s:
  `account`, `card`, `card-statement`, `transaction`, `recurring-transaction`, `pending-transaction`.
- No physical FK on `entity_id` (polymorphic) — the application validates that the target entity
  exists and belongs to the user. A trio `(tag_id, entity_type, entity_id)` is unique (no duplicate
  links).
- Deleting a tag **cascades** its links (`ON DELETE CASCADE`), audited.
- Filterable in listings (e.g. `GET /transactions?tags=`).

### API

| Method | Route | Purpose |
|---|---|---|
| GET / POST / PUT / DELETE | `/tags`, `/tags/{id}` | CRUD |
| GET | `/tags/{id}/links` | Links of a tag |
| POST | `/tags/{id}/links` | Add a link |
| DELETE | `/tags/{id}/links/{entityType}/{entityId}` | Remove a link |
| PUT | `/{entity}/{id}/tags` | Replace the whole tag set of an account/card/statement/transaction |

Audit: `tag.created/updated/deleted/linked/unlinked`.
