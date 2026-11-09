# Admin Module — Product Plan

> **Status:** design approved (2026-11-06); A1–A4 built (Tars packages, Admin module and enforcement
> in §9, the panel in §10). Covers the Tars packages it needs (feature flags and
> authorization) and the Pandora module that uses them.
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
| Tars packages | `Pottmayer.Tars.FeatureFlags{,.Abstractions,.AspNetCore}`, `Pottmayer.Tars.Security.Authorization{,.Abstractions,.AspNetCore}` |
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
catalog.Module(FinancesPermissions.Module, feature: FinancesFeatures.Finances)
    .Resource(FinancesPermissions.Cards.Resource).Crud()            // finances.cards.read/create/update/delete
    .Resource(FinancesPermissions.Imports.Resource, feature: FinancesFeatures.Import)
        .Read().Actions(FinancesPermissions.Actions.Run)
    .Resource(FinancesPermissions.Statements.Resource).Read().Actions(FinancesPermissions.Actions.Close);
```

The keys live as constants in each module's `Abstractions` (`FinancesFeatures`, `FinancesPermissions`),
built from tars' `PermissionActions` for the standard actions, so the declaration, the request marks
and the Assistant tools share them.

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
  `[RequiresPermission(FinancesPermissions.Cards.Delete)]`:
  1. the permission's feature is on, else `Error.NotFound("feature.disabled")`;
  2. the actor holds the key, else `Error.Forbidden("permission.denied")`;
  3. otherwise calls the handler.

  Both come back as a `Result` failure, so the existing `DefaultHttpErrorMapper` turns them into
  404/403 and the bot or the Assistant can reply with a message.
- **Who the actor is** comes from `IAuthorizationActorResolver.Resolve(request)`, registered by the app; subjects are
  strings (the JWT `sub`).
  Pandora's resolver reads `Input.UserId`, which every user command already carries (the controller
  fills it from the token, the bot from the linked user). Requests without `[RequiresPermission]`,
  such as system jobs (`RunRecurrenceGeneration`), are not checked.
- `.AspNetCore` adds a global MVC filter that enforces the same `[RequiresPermission]` on controllers
  and actions, reading the subject from the `sub` claim.

### 5.4 Store

`IAuthorizationStore.GetGrantsAsync(subjectId)` returns the subject's role patterns, direct allows
and direct denials. It only reads; Pandora implements it on `adm002`–`adm005` in its own persistence
and writes grants (with the audit entry) itself, then calls `Invalidate(subjectId)` or
`InvalidateAll()`. Tars ships no EF Core store: one query in Pandora does the job, and a generic one
waits for a second application that needs it.

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

Each module declares its flags and permissions in its `ApplicationDI`, and marks its requests with
the constants of its `<Module>Features` and `<Module>Permissions` classes (§9). Admin
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

`GET /identity/me` gains `features: string[]` (enabled keys) and `permissions: string[]` (the user's
effective keys, wildcards expanded). The rest is under `/api/v{version}/admin`:

| Method | Route | Purpose |
|---|---|---|
| GET | `/catalog` | Modules → resources → actions, with labels, features and dependencies |
| GET | `/features` | Every feature with its state and default |
| PUT | `/features/{key}` | Switch a feature on or off |
| GET · POST | `/roles` | List roles with their patterns and holders; create one |
| PUT · DELETE | `/roles/{id}` | Update or delete a role |
| GET | `/users` | List users, with filters |
| GET | `/users/{id}` | Detail with **effective permissions and where each comes from** |
| POST | `/users/roles` | Add or remove a role for several users at once |
| POST | `/users/approve` | Approve pending accounts (§11) |
| PUT | `/users/{id}/exceptions` | Replace the user's allow/deny exceptions |
| PUT | `/users/{id}/disabled` | Disable or enable the user |
| POST | `/users/{id}/revoke-sessions` | Sign the user out everywhere |
| GET | `/audit` | Paged list, filtered by actor, target and period |

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
| A2 | tars | `Security.Authorization` (catalog builder, resolution with wildcards and deny, cache, behavior, MVC filter) | tests green, docs in `docs/security/` |
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

---

## 9. What A3 built (2026-11-06)

- **Admin module**: `Abstractions`, `Domain`, `Application`, `Persistence`, `Infrastructure` (no
  `Contracts` or `Presentation` yet; the panel's endpoints come with A4). Migrations `adm001`–`adm006`
  plus the seed of `admin` (`*`) and `member` (default, one wildcard per user module), and `member` for
  every account that existed.
- **Stores**: `FeatureFlagStore` (`adm001`) and `AuthorizationStore` (`adm003`–`adm005`), read-only,
  behind the Tars flag service and permission checker.
- **Checkpoint**: `AddAdminInfrastructure` registers the catalogs, the cached services, the gate, the
  actor resolver (`InputUserIdActorResolver`, which reads `Input.UserId`) and the authorization
  behavior. Every module declares its flags and permissions in its own `ApplicationDI`, with
  `CheckUsagesIn` on its assembly.
- **Marks**: `[RequiresPermission]` on every user command and query of Finances, Notes, Agenda,
  Channels, Communications, Files, Integrations and Assistant.
- **Bootstrap**: `AdminBootstrapper` at startup and `AccountActivatedHandler` (default role, plus
  `admin` for a bootstrap e-mail), each grant audited in `adm006` with no actor. Docker: `ADMIN_EMAIL`.
- **`/me`** returns `features` (enabled keys) and `permissions` (effective keys).
- **Assistant**: `IAssistantTool.Permissions`; `Interpret` offers the model only the tools the user
  may use.
- **Identity** gained the `IUserDirectory` port (find an activated account by e-mail).

The catalog as declared:

| Module (flag) | Resources and actions |
|---|---|
| `finances` | `accounts`, `cards`, `recurrences`, `attachments`: CRUD · `statements`: read, close, pay · `transactions`: read, create, update, post, reverse, void · `pending`: read, create, update, approve, reject · `categories`: read, create, update · `tags`: CRUD, assign · `imports` (flag `finances.import`): read, run · `audit`: read |
| `notes` | `spaces`, `pages`, `media`: CRUD · `tags`: read, update |
| `agenda` | `calendars`, `events`, `reminders`, `lists`, `tasks`: CRUD · `alerts`: read, create, delete · `preferences`: read, update |
| `channels` | `channels`: read, create, delete, test · `notifications`: read, update |
| `communications` | `mailboxes`: CRUD · `messages`: read, update, send · `labels`, `rules`: read, update |
| `files` | `roots`: CRUD · `scans`: read, run · `entries`: read · `review`: read, update · `filters`: read, update, delete · `preferences`: read, update |
| `integrations` | `accounts`: CRUD |
| `assistant` | `commands`: read, run · `profile`: read, update |
| — (no flag) | `admin.users`, `admin.roles`: read, manage · `admin.features`: manage · `admin.audit`: read |

Decisions taken while building:

1. **Keys are constants** per module: `<Module>Features` and `<Module>Permissions` in its
   `Abstractions`, one nested class per resource (`FinancesPermissions.Cards.Delete`), with the
   standard actions from tars' `PermissionActions` and the module's own in `<Module>Permissions.Actions`.
   `CheckUsagesIn` still refuses to start the host on a mark the catalog does not declare.
2. **Identity self-service is not permissioned**: password, MFA, devices, preferences and `/me` stay
   open to every signed-in account, so a user can always manage their own account. Sign-in, sign-up,
   tokens and jobs carry no user and are not marked.
3. **Jobs ignore flags**: a background job sends unmarked system commands, so a module switched off
   keeps its jobs running; its user requests and Assistant tools are what turn off.
4. **New accounts get `member` when the outbox delivers `AccountActivated`**, a few seconds after the
   activation link is opened.
5. **Moved to A4**: the panel's endpoints, the MFA requirement for `admin` holders and the guard on the
   last `admin`. Until then the only writes are the bootstrap's; changing grants means editing the
   tables and restarting (the caches are per process).

---

## 10. What A4 built (2026-11-06)

- **API** (`Admin.Presentation`, `AdminController` on `/api/v1/admin`), each request marked with its
  `admin.*` permission:

  | Endpoint | Permission |
  |---|---|
  | `GET catalog` | `admin.roles.read` |
  | `GET features` · `PUT features/{key}` | `admin.features.manage` |
  | `GET roles` | `admin.roles.read` |
  | `POST roles` · `PUT roles/{id}` · `DELETE roles/{id}` | `admin.roles.manage` |
  | `GET users` · `GET users/{id}` | `admin.users.read` |
  | `POST users/roles` (bulk add/remove) · `PUT users/{id}/exceptions` · `PUT users/{id}/disabled` · `POST users/{id}/revoke-sessions` | `admin.users.manage` |
  | `GET audit?targetType&targetId&actorId&before&limit` | `admin.audit.read` |

  `admin.roles.read` was added so the users screen can list roles without managing them. Every write
  is audited in `adm006` with its before and after, then drops the affected caches.
- **MFA for the panel**: `RequireMfaFilter` refuses every panel request with 403 `Admin.MfaRequired`
  while the caller's account has no two-step verification. Checked at the panel, not at sign-in, so
  Identity never learns about roles; an admin without MFA still uses the other modules.
  `Pandora:Admin:RequireMfa` (default `true`) turns the check off; `appsettings.Development.json` sets
  it to `false` for local work.
- **Last admin**: removing `admin` from, or disabling, the last activated and enabled holder is a 409
  `Admin.LastAdmin`.
- **Identity**: `IUserDirectory` lists, finds, disables/enables (ending the sessions) and revokes the
  sessions of accounts; `User.Disable`/`Enable`. Both end the refresh tokens only: the access token in
  hand stays valid until it expires (stateless JWT), so the user is signed out within minutes, not at once.
- **Frontend**:
  - `useFeature`, `usePermission`, `useAllows` and `<Can>` (`modules/identity`) read `/me`; while it is
    unknown nothing is hidden.
  - Every entry in `layouts/navigation.tsx` declares its `feature` and `permission`. The menu drops what
    the user cannot open; a route under a switched-off feature shows the 404 page, one without the
    permission a 403 page.
  - Screens in `modules/admin`: **Users** (search, role filter, bulk add/remove role, detail drawer with
    roles, exceptions and the effective permissions with their origin, disable/enable, end sessions),
    **Roles** (list and a modal with the module → resource → action matrix, tri-state groups, a warning
    for an action without its `read`, resources under a switched-off flag greyed), **Features** (the
    flag tree with switches, confirming before turning one off) and **Audit** (newest first, by target
    type, loading older pages).

Not built: copying permissions from another user, marking a pattern left over from a removed key in
the panel, choosing which role is the default, and paging the users list (all accounts are loaded at
once).

---

## 11. Account approval (2026-11-06)

An option an installation can switch on so that a new account cannot sign in until an administrator
approves it, leaving time to adjust its roles and exceptions first.

- **Option**: `Pandora:Identity:AccountApproval:Required` (Docker: `REQUIRE_ACCOUNT_APPROVAL`), off
  by default.
- **Flow**: sign-up and activation work as before; the activation e-mail says an administrator still
  has to approve the account. On activation the account is marked as awaiting approval
  (`idt001.awaiting_approval_since`) and gets the default role as usual. Signing in with the right
  password answers 403 `Identity.PendingApproval`. Approving clears the mark and e-mails the user;
  rejecting disables the account, so the address cannot sign up again.
- **When the rule applies**: decided at activation. Accounts that existed before, and accounts
  activated while the option is off, are never marked. Switching the option off later does not release
  those already waiting: they are approved in the panel.
- **Bootstrap e-mails** are pre-approved. Identity asks through a port it owns,
  `IAccountApprovalPolicy`, which Admin implements with `Pandora:Admin:BootstrapEmails`, so Identity
  never learns about roles. The startup bootstrap also approves a bootstrap account that is waiting.
- **Administrators are told**: a pending activation e-mails the bootstrap addresses.
- **Panel**: the Users screen lists the accounts waiting first, with a notice counting them, a bulk
  *Approve* and *Approve* / *Reject* in the detail drawer. Approving uses `admin.users.manage`; each
  approval is audited (`user.approved`).
- **Events** (published by Identity on its outbox): `AccountActivationRequested` gains
  `RequiresApproval`; new `AccountApprovalRequested` (with the approvers' addresses) and
  `AccountApproved`. Channels turns them into e-mails.
