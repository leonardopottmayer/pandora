# Project and code standards

[Documentation index](../../README.md) · [Português](../pt-BR/coding-standards.md)

Read this before implementing or reviewing changes. Inspect an equivalent use case in the affected
module before writing code; follow its conventions rather than introducing a parallel structure.
These are project-wide defaults; module-specific rules live in its documentation. The frontend has
its own page, [Frontend architecture](frontend.md), and the screens follow
[Pandora's look](../../../client-web/DESIGN.md).

## How the work is done

- **Read first.** Trace the flow end to end (controller → handler → aggregate → repository, or page →
  hook → service) and find the closest existing use case before writing anything.
- **The smallest change that solves it.** No abstraction for one use, no option nobody asked for, no
  handling for cases that cannot happen. Reuse what the codebase already has.
- **Surgical.** Every changed line traces to the request. Don't reformat or "improve" what is next to
  it; mention unrelated problems instead of fixing them.
- **Ask when it is the user's call.** When there are real alternatives, present them with a short
  trade-off and a recommendation. Otherwise take the obvious default and say so.
- **Done means verified** (see Verification), not "it compiles".

## Backend: projects

Every module is a family of projects under `backend/src/Modules/<Name>/`, named
`Pottmayer.Pandora.Modules.<Name>.<Layer>`:

| Project | Holds | References |
|---|---|---|
| `Abstractions` | The module's identity (`<Name>Module`: `Name`, `DatabaseKey`, `Schema`), its `Options`, and the **ports other modules may call** with their models | nothing of the module |
| `Contracts` | The **integration events** it publishes or handles (POCO records) | Tars Messaging abstractions |
| `Domain` | Aggregates, value objects, domain errors, repository and service ports | `Shared.Domain`, Tars DDD/primitives |
| `Application` | Commands, queries, handlers, DTOs, application services, event handlers, assistant tools | Domain, Abstractions, Contracts, **other modules' Abstractions/Contracts only** |
| `Persistence` | `DbContext`, EF mappings (`EntityConfigs`), repositories, interceptors | Domain, Abstractions, `Shared.Persistence` |
| `Infrastructure` | Adapters to the outside (providers, parsers, HTTP clients), background jobs, localization | Application, Domain |
| `Presentation` | Controllers and HTTP request contracts | Application |

`backend/src/Shared` holds what every module needs (`UserData`, `Email`, `IAuditable`, file storage,
the auditing interceptor). `backend/src/Host` is the composition root.

**Module boundaries.** A module never references another module's Domain, Application or Persistence
and never reads another module's tables. It talks to another module in two ways only:

- a **port** in the other module's `Abstractions` (`IUserPreferencesReader`,
  `IEffectiveTimeZoneResolver`, `IExternalCredentialProvider`) for a question with an immediate
  answer;
- an **integration event** in a `Contracts` project, published on the outbox, for a fact that
  happened. See [Messaging](messaging.md).

## Backend: responsibilities and files

| Location | Responsibility |
|---|---|
| `Presentation/Controllers` | HTTP binding, authenticated user, mediator dispatch and response mapping |
| `Presentation/Requests` | HTTP request contracts; do not declare them inside controllers |
| `Presentation/Filters` | HTTP filters; keep them out of `Controllers` |
| `Application/Commands/<UseCase>` | Write use case: `<UseCase>Command.cs` and `<UseCase>CommandHandler.cs` |
| `Application/Queries/<UseCase>` | Read use case: `<UseCase>Query.cs` and `<UseCase>QueryHandler.cs` |
| `Application/Dtos`, `Application/Services` | Response DTOs and application orchestration helpers |
| `Application/Assistant` | The module's tools for the Assistant (`IAssistantTool`) |
| `Domain/Aggregates`, `Domain/ValueObjects`, `Domain/Errors` | State and invariants, value types and domain errors |
| `Domain/Ports` | Repository and service interfaces (`Ports/Repositories`, `Ports/Services`) |
| `Persistence/EntityConfigs`, `Persistence/Repositories` | EF mappings and repository implementations |
| `Infrastructure/Jobs` | Background services |
| `*/DI` | One `Add<Name><Layer>()` extension per project |

- Give each new use case its own folder, including distinct list/detail queries. Keep handlers in
  separate files. The `Input` record shares the command/query file, as existing use cases do.
- Put standalone DTOs and domain types in their own files and appropriate folders. Do not bundle
  unrelated records into an aggregate file. Keep repository projections beside the port when that
  is the module's established pattern.

### The shape of a use case

```csharp
// Commands/CloseStatement/CloseStatementCommand.cs
public sealed record CloseStatementInput(Guid UserId, Guid StatementId);

/// <summary>Manually closes a statement to new purchases ahead of its scheduled closing date.</summary>
public sealed class CloseStatementCommand(CloseStatementInput input)
    : CommandBase<CloseStatementInput, CardStatementDto>(input);
```

```csharp
// Commands/CloseStatement/CloseStatementCommandHandler.cs
public sealed class CloseStatementCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CloseStatementCommand, CardStatementDto>
{
    protected override async Task<Result<CardStatementDto>> HandleAsync(CloseStatementCommand request, CancellationToken ct)
    {
        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var statement = await statements.FindByIdForUserAsync(request.Input.StatementId, request.Input.UserId, token);
            if (statement is null)
                return Result<CardStatement>.Failure([StatementErrors.NotFound]);
            // ... change the aggregate, update, record the audit event in the same unit of work
            return Result<CardStatement>.Success(statement);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardStatementDto.From(result.Value!));
    }
}
```

```csharp
// Presentation/Controllers/StatementsController.cs
[HttpPost("{id:guid}/close")]
public async Task<IActionResult> CloseAsync(Guid id, CancellationToken ct)
{
    var result = await sender.Send(new CloseStatementCommand(new CloseStatementInput(UserId, id)), ct);
    return result.ToActionResult(errorMapper);
}
```

- Controllers are `[ApiController] [Authorize] [ApiVersion("1.0")]` on
  `api/v{version:apiVersion}/<module>/<resource>`, take the user from
  `IUserContextAccessor<UserData>`, and contain no business rules, repositories or EF code.
- Failures are `Result` with domain errors, never exceptions for expected cases. Each module has
  `Domain/Errors/<Thing>Errors` with `Error.NotFound/Validation/Conflict("<Things>.<Reason>", "message")`;
  the error mapper turns the kind into the HTTP status and localizes the message.
- Aggregates keep their setters private and are created by factories; behavior lives in their
  methods (`statement.Close(timeProvider)`). Use the module's existing domain values/constants for
  states and origins instead of repeating string literals across layers.
- Handlers return DTOs (`<Thing>Dto.From(aggregate)`) or a primitive (`bool`, a count, an id), never
  aggregates.

## Backend: time, options, DI and jobs

- Use `TimeProvider`; store instants in UTC and interpret user dates in the user's time zone
  (`IEffectiveTimeZoneResolver.ResolveTodayAsync`). Keep pure calendar dates as `DateOnly`. See
  [KI-001](../../known-issues.md) before touching dates.
- A class bound to an `appsettings` section is `<Name>Options` with a `SectionName` constant
  (`Pandora:Agenda`); it lives in the module's `Abstractions`.
- Each project registers itself through its own `DI/<Layer>DI.cs` extension, which `Program.cs`
  calls in the fixed module order. Use DI for services and interceptors; database interceptors
  belong in `Persistence/Interceptors`. Follow the existing lifetimes.
- **Background work is a job over the module's own table**: a `BackgroundService` in
  `Infrastructure/Jobs` with a `PeriodicTimer`, which opens a fresh scope and sends a CQRS command
  each tick. The work, its retries and its outcome live in the module's tables. No broker, no
  scheduling for another module. See [Messaging §3](messaging.md#3-asynchrony-without-a-broker).
- Integration event handlers are idempotent (the outbox delivers at least once), preferably through a
  natural key on the work itself.

## Persistence and migrations

- Use repositories through the module's unit of work (`IUnitOfWorkFactory.ExecuteAsync(<Module>.DatabaseKey, ...)`).
  Commit related writes atomically, including audit/history records and outbox events. Scope
  user-owned data by owner (`FindByIdForUserAsync`); another user's resource is a 404, not a 403.
- Every module has its own `DbContext` and PostgreSQL schema. Follow existing EF mappings and SQL
  names: tables prefixed by the module's code and number (`fin008_transaction`), snake_case columns,
  named constraints/indexes, and string enums with hyphens (`pending-confirmation`, `api-key`).
- Migrations are plain SQL run by migris, in `migrations/migrations/<module>/<timestamp>-<action>/`
  with a `.up.sql` and a `.down.sql` (`20260616120001-create-table-fin008-transaction`).
- Pre-1.0: change existing schema in the original `CREATE TABLE`; new tables get their own create/down
  scripts. Do not add incremental `ALTER` migrations for existing tables. Recreating a populated
  local database is a separate destructive operation, not an automatic implementation step.
- Reuse the Tars integration and module boundaries documented in
  [Tars wiring](tars-wiring.md) and [Messaging](messaging.md).

## Naming and language

- Code identifiers, file and folder names, tables, columns and internal routes are in English. The
  UI, routes the browser shows, and comments may be Portuguese where the code around them already is.
- Suffix C# async methods with `Async`, including test helpers and controller actions.
- Match surrounding imports, naming and formatting. Use descriptive names; avoid compressed
  statements and nested ternaries that obscure loading, error and success paths.
- Comments say **why**, not what: a business rule, a non-obvious constraint, a decision. Public types
  and use cases get a one-line `/// <summary>`.

## Verification

- Add regression tests for behavior bugs and meaningful tests for nontrivial logic.
- **Backend:** xUnit. `backend/tests/Pottmayer.Pandora.Modules.<Name>.Tests` holds unit tests of
  aggregates, value objects, parsers and services, with hand-written fakes (`Fakes/`, e.g.
  `FixedTimeProvider`). `Pottmayer.Pandora.IntegrationTests` boots the real Host against PostgreSQL in
  Testcontainers, builds the schema from the SQL migrations and resets data with Respawn; it needs
  Docker. Run `dotnet test backend/Pottmayer.Pandora.slnx`, or the affected test project.
- **Frontend:** see [Frontend architecture § Checks](frontend.md#checks-before-calling-a-change-done).
- Review the diff against these rules; test counts do not replace checking boundaries and file
  placement. Report what was actually verified, what was not (for example, not checked in the
  browser), and any remaining limitations.

## Documentation

- Update the affected module docs in the same change: English is primary, with the same topics
  mirrored in pt-BR. A module's `implementation-status.md` says what is built; `product-plan.md` only
  what is still missing. Use the [documentation index](../../README.md); handoffs record session
  context and may be outdated.
- A cross-cutting change (a Tars call added or removed, a new event, a new job) also updates
  [Tars wiring](tars-wiring.md) or [Messaging](messaging.md).

## Commits

- **Never commit or push without being asked**, and approval for one commit does not carry over to the
  next. Never add a `Co-Authored-By` trailer. After a commit, show its message. (These rules are also
  in the root `AGENTS.md`/`claude.md`.)
- Stage the paths of the change by name. Never commit `backend/src/Host/Pottmayer.Pandora.Host/appsettings.json`
  or `migrations/seed/seed.local.sql` (local secrets), and never print them.
- Subject: Conventional Commits with the module as scope, imperative, lower case, no period:
  `feat(finances): redesign the recurrences screen and its forms`, `fix(notes): keep inline HTML when ...`,
  `docs: ...`, `perf(client-web): ...`.
- Body: plain prose wrapped at about 100 columns. First what was wrong or missing, then what the
  change does, from the user's point of view before the code's. Last line: what was verified
  (`Tests: 828 frontend tests, typecheck, eslint and antd lint clean.`).
