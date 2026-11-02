# Project and code standards

[Documentation index](../../README.md) · [Português](../pt-BR/coding-standards.md)

Read this before implementing or reviewing changes. Inspect an equivalent use case in the affected
module before writing code; follow its conventions rather than introducing a parallel structure.
These are project-wide defaults; module-specific rules live in its documentation.

## Backend: responsibilities and files

| Location | Responsibility |
|---|---|
| `Presentation/Controllers` | HTTP binding, authenticated user, mediator dispatch and response mapping |
| `Presentation/Requests` | HTTP request contracts; do not declare them inside controllers |
| `Presentation/Filters` | HTTP filters; keep them out of `Controllers` |
| `Application/Commands/<UseCase>` | Write use case: `<UseCase>Command.cs` and `<UseCase>CommandHandler.cs` |
| `Application/Queries/<UseCase>` | Read use case: `<UseCase>Query.cs` and `<UseCase>QueryHandler.cs` |
| `Application/Dtos`, `Application/Services` | Response DTOs and application orchestration helpers |
| `Domain/Aggregates`, `Domain/ValueObjects`, `Domain/Errors` | State and invariants, value types and domain errors |
| `Domain/Ports`, `Persistence`, `Infrastructure` | Interfaces, database implementations and external adapters |

- Give each new use case its own folder, including distinct list/detail queries. Keep handlers in
  separate files. An `Input` record may share the command/query file, as existing use cases do.
- Put standalone DTOs and domain types in their own files and appropriate folders. Do not bundle
  unrelated records into an aggregate file. Keep repository projections beside the port when that
  is the module's established pattern.
- Controllers do not contain business rules, repositories or EF code. Use the existing
  `CommandBase`/`QueryBase`, `Result`, domain errors and `ToActionResult` flow.
- Keep aggregate setters private and creation in factories. Use the module's existing domain
  values/constants for states and origins instead of repeating string literals across layers.
- Use DI for services and interceptors. Database interceptors belong in `Persistence/Interceptors`;
  register them through the module's DI setup, following existing lifetimes.
- Use `TimeProvider`; store instants in UTC and interpret user dates in the account's time zone.
  Keep pure calendar dates as `DateOnly` where the domain requires it.

## Persistence and boundaries

- Use repositories through the module's unit of work. Commit related writes atomically, including
  audit/history records. Scope user-owned data by owner; foreign resources follow the module's 404 rule.
- Follow existing EF mappings and SQL names: snake_case columns, module table prefixes, named
  constraints/indexes and existing string-enum conventions (for example `pending-confirmation`).
- Pre-1.0: change existing schema in the original `CREATE TABLE`; new tables get their own create/down
  scripts. Do not add incremental `ALTER` migrations for existing tables. Recreating a populated
  local database is a separate destructive operation, not an automatic implementation step.
- Reuse the Tars integration and module boundaries documented in
  [Tars wiring](tars-wiring.md) and [Messaging](messaging.md).

## Frontend and readability

- Keep route screens in `pages`, UI in `components`, Query hooks in `hooks`, HTTP in `services`, pure
  logic in `lib`, and DTO mirrors in `models.ts`. Add query keys to the module's central factory and
  invalidate affected caches explicitly.
- Follow [Pandora's look](../../../client-web/DESIGN.md). Use theme tokens and en/pt-BR translations;
  check the installed Ant Design version's API before using or changing components.
- Match surrounding imports, naming and formatting. Use descriptive props and helpers; avoid
  compressed statements and nested ternaries that obscure loading, error and success paths.
- Keep code identifiers, paths, tables and routes in English. Suffix C# async methods with `Async`,
  following the existing code, including test helpers.

## Verification and documentation

- Add regression tests for behavior bugs and meaningful tests for nontrivial logic. Run affected
  .NET tests; for frontend changes, run relevant Vitest tests, type/build checks and ESLint.
- Review the diff against these rules; test counts do not replace checking boundaries and file placement.
  Report what was actually verified and any remaining limitations.
- Update affected module docs: English is primary, with the same topics mirrored in pt-BR. Use the
  [documentation index](../../README.md); handoffs record session context and may be outdated.
- Keep changes surgical. Do not print or commit local secrets such as `appsettings.json` or
  `seed.local.sql`. Commit/push rules remain in the root agent instructions.
