# How Pandora Is Wired to Tars

> Cross-cutting document: it does not belong to any one module, and no single file in the backend
> shows the whole picture — this page is that picture.
> 🇧🇷 [Versão em português](../pt-BR/tars-wiring.md)

Pandora consumes `Pottmayer.Tars` as a set of NuGet packages, not as one aggregate call. There is no
`AddTars()`. Each package family is registered by its own `AddTars*`/`UseTars*` methods, called from
whichever project actually owns that concern, and `Program.cs` only composes the pieces in order. This
page is the map: which family, which methods, which file, and why it lives where it does.

For what each method actually does, see the family's own configuration guide in
[`tars/docs`](../../../../tars/docs/README.md) — this page only answers "where," not "what."

---

## Composition order in `Program.cs`

[`Host/Pottmayer.Pandora.Host/Program.cs`](../../../backend/src/Host/Pottmayer.Pandora.Host/Program.cs)
calls, in this order:

1. `builder.AddPandoraSharedInfrastructure()` — Observability, UserContext, correlation-id middleware setup (see below).
2. `builder.AddPandoraSharedPersistence()` — the Data family's cross-module infrastructure (connection resolver, context accessor/factory, unit-of-work factory).
3. Per module, in this fixed order — **Identity, Channels, Finances, Notes, Agenda, Integrations, Assistant, Communications, Files, Admin** — three calls each: `Add<Module>Persistence()`, `Add<Module>Infrastructure()`, `Add<Module>Application()`.
4. `builder.AddPandoraOutbox()` — the Messaging family (in-process outbox). Registered **after** every module, because it needs every module's contracts assembly and database key already known.
5. `AddTarsLocalization()` + module-level localization (`AddPandoraLocalization()`, `AddFinancesLocalization()`) and `AddTarsProblemDetails()` — Web/error-mapping concerns.
6. Presentation: `AddControllers()` followed by one `Add<Module>PresentationPart()` per module, in the same order; then API versioning, Swagger, forwarded headers, CORS — not Tars, but downstream of it.

A new module adds its three calls at the end of step 3 and its presentation part at the end of step 6.

Two middleware calls (not shown above, in the pipeline build): `app.UseTarsCorrelationId()` and
`app.UseTarsUserContext()`.

---

## Family by family: which file registers what

### Observability

- **File:** [`Shared/Pottmayer.Pandora.Shared.Infrastructure/DI/SharedInfrastructureDI.cs`](../../../backend/src/Shared/Pottmayer.Pandora.Shared.Infrastructure/DI/SharedInfrastructureDI.cs)
- **Calls:** `AddTarsObservabilityOptions`, `AddTarsObservabilityResource`, `AddTarsTracing`, `AddTarsAspNetCoreTracing`, `AddTarsHttpClientTracing`, `AddTarsTracingOtlpExporter`, `AddTarsMetrics`, `AddTarsAspNetCoreMetrics`, `AddTarsHttpClientMetrics`, `AddTarsRuntimeMetrics`, `AddTarsMetricsOtlpExporter`, `AddTarsLogging`, `AddTarsLoggingOtlpExporter`, `UseTarsCorrelationId`.
- Follows the canonical order documented in [tars/docs/observability/configuration.md](../../../../tars/docs/observability/configuration.md) — pipeline → instrumentation → exporter, per signal. Native `ILogger` provider, not Serilog.

### User Context

- **File:** same `SharedInfrastructureDI.cs`.
- **Calls:** `AddTarsClaimsUserResolver`, `AddTarsDefaultUserContextFactory`, `AddTarsUserContextAccessor`, `AddTarsCurrentPrincipalAccessor`. Middleware: `UseTarsUserContext` (in `Program.cs`).
- One registration, shared by every module — modules read the current user via `IUserContext<T>`, they do not register it themselves.

### Data (relational)

- **Cross-module infra file:** [`Shared/Pottmayer.Pandora.Shared.Persistence/DI/SharedPersistenceDI.cs`](../../../backend/src/Shared/Pottmayer.Pandora.Shared.Persistence/DI/SharedPersistenceDI.cs) — `AddTarsDataContextAccessor`, `AddTarsDataContextFactory`, `AddTarsRelationalConfigurationConnectionResolver`, `AddTarsUnitOfWorkFactory`. Registered once for the whole host.
- **Per module** (`Modules/<Name>/Pottmayer.Pandora.Modules.<Name>.Persistence/DI/PersistenceDI.cs`, one in every module): `AddTarsRelationalData<...DbContext>(...)` and `AddTarsDataRepositoriesFromAssemblies(...)`. Every module has its own `DbContext` and its own PostgreSQL schema — see the module's own `data-model.md` for the schema.

### Security / Identity

- **File:** [`Modules/Identity/Pottmayer.Pandora.Modules.Identity.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Identity/Pottmayer.Pandora.Modules.Identity.Infrastructure/DI/InfrastructureDI.cs)
- **Calls:** `AddTarsIdentityOptions`, `AddTarsIdentityAspNetCoreOptions`, `AddTarsIdentityJwtTokenIssuer`, `AddTarsIdentityJwtTokenValidator`, `AddTarsIdentityRefreshTokenService`, `AddTarsIdentityInMemoryTokenRevocationStore`, `AddTarsIdentityTokenRevocationService`, `AddTarsIdentityTokenDeliveryPolicy`, `AddTarsIdentityAspNetCoreTokenTransport`, `AddTarsIdentityAspNetCoreJwtBearer`, and `AddTarsIdentityApiKey(DeviceAuthorization.Scheme)` — the device keys (`X-Api-Key`) of Pandora Desktop, a scheme only the endpoints that name it accept.
- Only the Identity module touches this family — it owns JWT issuance/validation and device keys end to end. See [Identity module docs](../../modules/identity/README.md).

### Security / DataProtection

- **File:** [`Modules/Integrations/Pottmayer.Pandora.Modules.Integrations.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Integrations/Pottmayer.Pandora.Modules.Integrations.Infrastructure/DI/InfrastructureDI.cs)
- **Calls:** `AddTarsDataProtectionOptions`, `AddTarsSecretProtector` — `ISecretProtector`, used to encrypt OAuth credentials at rest.
- Only Integrations uses this family, for exactly that reason: it is the only module storing third-party secrets.

### Core (Mediator/CQRS)

- **Files:** every module's own `Modules/<Name>/Pottmayer.Pandora.Modules.<Name>.Application/DI/ApplicationDI.cs` calls `AddTarsMediator(...)`, scoped to that module's own handler assembly.
- There is no single global mediator registration — each module scans its own assembly, so a handler in one module is never accidentally visible to another.

### Localization

- **Host-level:** `AddTarsLocalization()` in `Program.cs`, plus `AddPandoraLocalization()` (host-owned messages) and `AddFinancesLocalization()` (Finances module's own message source, wired through `Modules/Finances/Pottmayer.Pandora.Modules.Finances.Infrastructure/DI/LocalizationDI.cs` — the only module with its own localization source today).
- Everything else uses `AddTarsHttpErrorMapper`/`AddTarsMessageSource` from `Host/Pottmayer.Pandora.Host/Localization/LocalizationDI.cs`.

### Messaging (in-process outbox)

- **File:** [`Host/Pottmayer.Pandora.Host/OutboxRegistration.cs`](../../../backend/src/Host/Pottmayer.Pandora.Host/OutboxRegistration.cs)
- **Calls:** `AddTarsOutboxOptions`, `AddTarsIntegrationEventTypeRegistry` (one representative event per contracts assembly: Identity, Channels, Integrations, Communications), `AddTarsIntegrationEventDispatcher`, `AddTarsIntegrationEventSerializer`, `AddTarsOutboxBus`, `AddTarsOutboxStore`, then `AddTarsOutboxRelay(...)` once per producing database (Identity, Channels, Agenda, Integrations, Assistant, Communications).
- Each producing module's `DbContext` (`IdentityDbContext`, `ChannelsDbContext`, `AgendaDbContext`, `IntegrationsDbContext`, `AssistantDbContext`, `CommunicationsDbContext`) also calls `AddTarsOutbox(...)` on itself to map the outbox table — see each file. A module that starts publishing needs all three: the `AddTarsOutbox` in its `DbContext`, a relay, and its contracts assembly in the registry if it is a new one. No broker is registered (`AddTarsMassTransitRabbitMq` appears only in a code comment in `OutboxRegistration.cs`, as documentation of what the swap would look like if a module is ever extracted to a service — it is not called). Full mechanism: [Messaging architecture](messaging.md) and [tars/docs/messaging/outbox.md](../../../../tars/docs/messaging/outbox.md).

### Communication

- **File:** [`Modules/Channels/Pottmayer.Pandora.Modules.Channels.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Channels/Pottmayer.Pandora.Modules.Channels.Infrastructure/DI/InfrastructureDI.cs)
- **Calls:** `AddTarsMailKitEmailOptions`, `AddTarsMailKitEmailSender`, `AddTarsLoggingEmailSender` (the fake, dev-only), `AddTarsTelegramOptions`, `AddTarsTelegramHttpClient`, `AddTarsTelegramClientFactory`.
- Only Channels talks to this family — it owns outbound email and the Telegram bot. Communications reads and sends the user's own mail (Gmail API, MailKit IMAP/SMTP) through its own adapters, not through this family.

### AI

- **File:** [`Modules/Assistant/Pottmayer.Pandora.Modules.Assistant.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Assistant/Pottmayer.Pandora.Modules.Assistant.Infrastructure/DI/InfrastructureDI.cs)
- **Calls:** `AddTarsAiChatGeminiOptions`, `AddTarsAiChatGeminiHttpClient`, `AddTarsAiChatCompletionClientGemini`, `AddTarsAiClientFactory`.
- Only the Assistant uses it. No API key is configured: each user's Gemini key comes from Integrations and goes on the request; the options section (`Tars:Ai:Chat:Gemini`) only carries the base URL and timeout.

### Feature Flags and Security / Authorization

- **Declarations:** every user module's `ApplicationDI` calls `AddTarsFeatureFlagDefinitions` (its flag,
  plus `finances.import`) and `AddTarsPermissionDefinitions` (its permissions, with `CheckUsagesIn` on
  its assembly); Admin's `ApplicationDI` declares the `admin.*` permissions and registers the two
  stores (`IFeatureFlagStore`, `IAuthorizationStore`).
- **File:** [`Modules/Admin/Pottmayer.Pandora.Modules.Admin.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Admin/Pottmayer.Pandora.Modules.Admin.Infrastructure/DI/InfrastructureDI.cs)
- **Calls:** `AddTarsFeatureFlagCatalog`, `AddTarsFeatureFlagService`, `AddTarsPermissionCatalog`,
  `AddTarsPermissionChecker`, `AddTarsAuthorizationGate`,
  `AddTarsAuthorizationActorResolver<InputUserIdActorResolver>`, `AddTarsAuthorizationBehavior`.
- The behavior is the only mediator pipeline behavior Pandora registers, so it applies to every
  module's requests. Modules never reference Admin: they only mark requests with
  `[RequiresPermission]`. Identity reads `IFeatureFlags` and `IPermissionChecker` for `/me`, and the
  Assistant uses `AuthorizationGate` to offer only the tools a user may use. See
  [Admin](../../modules/admin/README.md).

### Web / HTTP

- **File:** `Host/Pottmayer.Pandora.Host/Localization/LocalizationDI.cs` (`AddTarsHttpErrorMapper`) plus `AddTarsProblemDetails()` in `Program.cs`.
- Pandora uses a lighter subset of `Web.Http` than the framework's own `application-blueprint.md` example — no response-wrapping filters, just error mapping and RFC 7807 problem details.

### Caching, Multitenancy

- **Not used.** No `AddTarsMemoryCache*`/`AddTarsRedis*`/`AddTarsTenant*` call exists anywhere in the backend. Pandora is intentionally single-tenant (see [messaging.md §2](messaging.md#2-the-decision-in-process-one-process) for the same "one user, one deploy" reasoning applied to messaging).

---

## Keeping this page current

This page is generated from reading the code, not the other way around — if a module adds or removes
an `AddTars*` call, this page goes stale until someone updates it. There is no automated check for
that yet. When in doubt, `grep -rhoE "AddTars[A-Za-z]+|UseTars[A-Za-z]+" backend/src --include=*.cs | sort -u`
from the repo root is the ground truth.
