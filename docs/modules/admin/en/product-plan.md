# Admin Module — Product Plan

> **Status:** design approved (2026-11-06), nothing built yet. Covers the Tars packages it needs
> (feature flags and authorization) and the Pandora module that uses them.
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related: [Identity](../../identity/README.md) · [Tars wiring](../../../architecture/en/tars-wiring.md)

---

## 1. What the module does

**Admin** decides what is switched on in the system and what each user may do, and gives a master
administrator one place to manage it once Pandora is published.

Two independent axes, checked in this order on every request:

```
request ──► 1. Is the feature on in the system?          no ► 404 (nobody, not even the admin)
              │ yes
              ▼
            2. Does the user hold the action's permission? no ► 403
              │ yes
              ▼
            handler runs
```

- **Feature flags** are global switches: a whole module (`finances`) or one capability
  (`finances.import`). They ignore who the user is.
- **Permissions** are per action: `finances.cards.read`, `finances.cards.create`,
  `finances.cards.delete`, `finances.imports.run`, and so on. Users get them through **roles** and
  through per-user **allow/deny** exceptions.

### What it is not

- **Not authentication.** Sign-in, tokens, MFA and the users themselves stay in
  [Identity](../../identity/README.md). Admin only answers "is this allowed?".
- **Not data ownership.** Every module still filters its data by `UserId`. A permission says the user
  may delete *cards*; it never lets them touch someone else's card.
- **Not multitenancy.** One installation, one set of roles. Tenant-scoped grants are out of scope.

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Tars packages | `Pottmayer.Tars.FeatureFlags{,.Abstractions,.AspNetCore}`, `Pottmayer.Tars.Security.Authorization{,.Abstractions,.AspNetCore,.EntityFrameworkCore}` |
| Backend projects | `Pottmayer.Pandora.Modules.Admin.{Abstractions,Contracts,Domain,Application,Infrastructure,Persistence,Presentation}` |
| PostgreSQL schema | `admin` |
| Table prefix | `admXXX_`, PK `uuid_generate_v7()` |
| Options section | `Pandora:Admin` |
| API base | `/api/v{version}/admin`, JWT, each endpoint guarded by an `admin.*` permission |
| Frontend | `client-web/src/modules/admin`, routes `admin/users`, `admin/roles`, `admin/features`, `admin/audit` |
| Migrations | `migrations/migrations/admin/` |

---

## 3. Principles

1. **Declared in code, changed in the database.** Flags and permissions are declared by the module
   that owns them, with their defaults. The database stores only what an administrator changed. A key
   can never exist in the database without existing in code.
2. **One checkpoint.** The check runs in a mediator pipeline behavior, so HTTP, the Telegram bot, the
   Assistant and background jobs all pass through it. A controller attribute exists only for the few
   endpoints that do not dispatch through the mediator.
3. **Deny wins.** Effective permissions = roles ∪ direct allows − direct denies.
4. **Flags beat permissions.** A disabled feature is off for everyone, the master admin included.
5. **Changes apply at once.** The effective set is cached in memory and dropped on every change; it is
   never baked into the JWT.
6. **Fail fast on typos.** At startup, every key used by `[RequiresPermission]` must exist in the
   catalog, or the host refuses to start.
7. **Frontend hiding is comfort only.** The backend decides; the client hides buttons and routes so
   the user does not hit a 403.

---

## 4. Tars: feature flags

`Pottmayer.Tars.FeatureFlags`

- **Catalog.** Each module contributes its flags with a default and an i18n label key:
  ```csharp
  flags.Add("finances", defaultOn: true);
  flags.Add("finances.import", defaultOn: true, parent: "finances");
  ```
  A child flag is off whenever its parent is off.
- **Store.** `IFeatureFlagStore` only reads overrides; the application writes them (with its audit
  entry) and then calls `IFeatureFlags.Invalidate()`. Tars ships an in-memory store; Pandora
  implements one on Postgres (`adm001`).
- **Use.** `IFeatureFlags.IsEnabled(key)` in code; `[RequiresFeature(key)]` on a controller (from
  `.AspNetCore`). A disabled feature returns `Error.NotFound("feature.disabled", …)`.
- **Cache.** The whole flag map is small: held in memory, reloaded when an override changes.

## 5. Tars: authorization

`Pottmayer.Tars.Security.Authorization`

### 5.1 Catalog

Each module declares its permissions with a short builder. The CRUD helper generates the four usual
actions:

```csharp
catalog.Module("finances", feature: "finances")
    .Resource("cards").Crud()                         // finances.cards.read/create/update/delete
    .Resource("imports", feature: "finances.import").Actions("run", "read")
    .Resource("transactions").Crud().Actions("approve");
```

Every permission carries:
- an i18n label key, built from the key by convention;
- its **feature**: the permission is checked only after that flag;
- its **dependencies**: `create`, `update` and `delete` imply `read`, so granting `delete` without
  `read` is rejected by the panel.

### 5.2 Grants

- **Role**: a named set of permission patterns. A pattern is an exact key or a wildcard
  (`finances.*`, `finances.cards.*`). Wildcards also cover permissions added in later releases.
- **User roles**: a user may hold any number of roles.
- **User exceptions**: `allow` or `deny` on one pattern for one user.
- **System roles**: `admin` holds `*` and cannot be edited or deleted. One role is marked
  **default** and is given to every new account.

### 5.3 Resolution and the check

- `IPermissionChecker.HasAsync(userId, key)` expands the user's roles and exceptions against the
  catalog into a flat set of keys, cached per user. Any change to a role, a user's roles or a user's
  exceptions drops the affected entries (role change → every holder of the role).
- `AuthorizationBehavior<TRequest, TResponse>` runs for requests marked
  `[RequiresPermission("finances.cards.delete")]`:
  1. the permission's feature is on, else `Error.NotFound("feature.disabled")`;
  2. the actor holds the key, else `Error.Forbidden("permission.denied")`;
  3. otherwise calls the handler.

  Both come back as a `Result` failure, so the existing `DefaultHttpErrorMapper` turns them into
  404/403 and the bot or the Assistant can reply with a message.
- **Who the actor is** comes from `IAuthorizationActorResolver<TRequest>`, registered by the app.
  Pandora's resolver reads `Input.UserId`, which every user command already carries (the controller
  fills it from the token, the bot from the linked user). Requests without `[RequiresPermission]`,
  such as system jobs (`RunRecurrenceGeneration`), are not checked.
- `.AspNetCore` adds `[RequiresPermission]` for controllers, through a policy provider with a
  `perm:` prefix, like the existing `DevicePolicyProvider`.

### 5.4 Store

`IAuthorizationStore` reads roles, role patterns, user roles and user exceptions. The
`.EntityFrameworkCore` package ships the entities and configurations with **configurable table
names**, so Pandora maps them to `adm002`–`adm005` and keeps writing its own `.sql` migrations.

---

## 6. Pandora: the Admin module

### 6.1 Data model

| Table | Holds |
|---|---|
| `adm001_feature_flag` | `key` (PK), `enabled`, `updated_by`, `updated_at`: only overridden flags |
| `adm002_role` | `id`, `name`, `description`, `is_system`, `is_default` |
| `adm003_role_permission` | `role_id`, `pattern` |
| `adm004_user_role` | `user_id`, `role_id` |
| `adm005_user_permission` | `user_id`, `pattern`, `effect` (`allow` / `deny`) |
| `adm006_audit_entry` | `id`, `actor_id`, `action`, `target_type`, `target_id`, `before` jsonb, `after` jsonb, `occurred_at` |

Patterns left in the database for a key that no longer exists are ignored and flagged in the panel.

### 6.2 Catalogs

Each module's `Abstractions` declares its flags and permissions (`FinancesPermissions`,
`FinancesFeatures`) as typed constants, used both in the catalog and in `[RequiresPermission]`. Admin
declares its own: `admin.users.read`, `admin.users.manage`, `admin.roles.manage`,
`admin.features.manage`, `admin.audit.read`. Admin access is itself a permission, so it can be
delegated without handing over `*`.

### 6.3 Master admin bootstrap

- `Pandora:Admin:BootstrapEmails` lists the emails that must hold the `admin` role.
- On startup, and whenever an account is activated (`AccountActivated`), a listed user gets the role
  if they do not have it yet. Idempotent; works in Docker through an environment variable.
- Guards: the last holder of `admin` cannot lose it or be disabled, and users holding `admin` must
  have MFA enabled (sign-in sends them to set it up first).

### 6.4 Rollout on the existing install

The first migration seeds:
- the `admin` system role (`*`);
- a `member` role marked default, holding one wildcard per user module (`finances.*`, `notes.*`,
  `agenda.*`, …), never `admin.*`;
- `member` for every existing user.

Nothing changes for anyone until an administrator edits a role.

### 6.5 What other modules consume

- **Admin consumes Identity** through a port in `Identity.Abstractions`: list and search users,
  disable/enable, revoke sessions. Identity stays the owner of users.
- **Modules consume Admin** only through the Tars abstractions (`IFeatureFlags`,
  `IPermissionChecker`) and their own `[RequiresPermission]` marks. No module references the Admin
  projects.
- **The Assistant** filters the tools it offers to the model by the user's permissions, so it never
  proposes an action it would be refused.

### 6.6 API

- `GET /identity/me` gains `features: string[]` (enabled keys) and `permissions: string[]` (the
  user's effective keys, wildcards expanded).
- `GET /admin/catalog`: modules → resources → actions, with labels, features and dependencies.
- Users: list with filters, detail with **effective permissions and where each comes from**, bulk
  add/remove role, copy permissions from another user, exceptions, disable/enable, revoke sessions.
- Roles: CRUD, patterns, holders.
- Features: list with state and default, toggle.
- Audit: paged list, filter by actor, target and period.

Every write goes into `adm006` with the before and after state.

### 6.7 Frontend

- `usePermission(key)`, `useFeature(key)` and `<Can permission="…">` read from `/me`. The menu shows a
  module when its flag is on and the user holds any permission in it; routes do the same and show a
  403 page otherwise.
- **Roles screen**: a matrix, module → resource in rows and actions in columns. Module and resource
  rows have a tri-state checkbox that toggles everything below. Search by name, permissions under a
  disabled feature shown greyed, count of holders, warning when a dependency is missing.
- **Users screen**: the standard list (fills the window height, grouped panel), multi-select with
  bulk *add role* / *remove role*. The detail drawer lists effective permissions with their origin
  ("via Finance – read", "direct exception") and edits exceptions in place.
- **Features screen**: the flag tree with switches; turning a parent off shows what goes with it.
- **Audit screen**: read-only timeline.

---

## 7. Delivery

| Step | Repository | Content | Done when |
|---|---|---|---|
| A1 | tars | `FeatureFlags` (catalog, stores, `IFeatureFlags`, `[RequiresFeature]`) | tests green, docs in `docs/feature-flags/` |
| A2 | tars | `Security.Authorization` (catalog builder, resolution with wildcards and deny, cache, behavior, ASP.NET attribute, EF store) | tests green, docs in `docs/security/` |
| A3 | pandora | Admin module, `adm001`–`adm006`, catalogs for every module, `[RequiresPermission]` on every user command and query, Pandora actor resolver, bootstrap, `/me` with features and permissions, Assistant tool filter | existing tests green, new tests for 403/404 and bootstrap; nothing changes for the current user |
| A4 | pandora | Frontend hooks, menu and route guards, screens for roles, users, features and audit | checked in the browser by the user |

## 8. Decisions

| Decision | Choice |
|---|---|
| Permission model | Roles + per-user allow/deny, deny wins |
| Where the check runs | Mediator pipeline behavior, controller attribute only for the rest |
| Flag store | Database, toggled in the panel |
| Master admin bootstrap | `Pandora:Admin:BootstrapEmails`, applied at startup and on activation |
| Freshness | Database lookup cached in memory, dropped on change; nothing in the JWT |
| Response when off / denied | 404 `feature.disabled` / 403 `permission.denied` |

Out of scope for now: impersonation, per-module usage numbers, system health page, tenant-scoped
grants, plans or billing.
