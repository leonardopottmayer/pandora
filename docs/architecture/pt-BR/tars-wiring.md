# Como o Pandora Está Ligado ao Tars

> Documento transversal: não pertence a nenhum módulo, e nenhum arquivo isolado do backend mostra o
> quadro inteiro — esta página é esse quadro.
> 🇺🇸 [English version](../en/tars-wiring.md)

O Pandora consome o `Pottmayer.Tars` como um conjunto de pacotes NuGet, não como uma chamada agregada.
Não existe `AddTars()`. Cada família de pacote é registrada pelos seus próprios métodos
`AddTars*`/`UseTars*`, chamados de qualquer projeto que seja dono daquela responsabilidade, e o
`Program.cs` só compõe as peças na ordem certa. Esta página é o mapa: qual família, quais métodos, qual
arquivo, e por que mora onde mora.

Para o que cada método realmente faz, ver o guia de configuração da própria família em
[`tars/docs`](../../../../tars/docs/README.md) — esta página só responde "onde", não "o quê".

---

## Ordem de composição no `Program.cs`

[`Host/Pottmayer.Pandora.Host/Program.cs`](../../../backend/src/Host/Pottmayer.Pandora.Host/Program.cs)
chama, nesta ordem:

1. `builder.AddPandoraSharedInfrastructure()` — Observability, UserContext, setup do middleware de correlation-id (ver abaixo).
2. `builder.AddPandoraSharedPersistence()` — infraestrutura cross-módulo da família Data (resolvedor de conexão, accessor/factory de contexto, factory de unit-of-work).
3. Por módulo, nesta ordem fixa — **Identity, Channels, Finances, Notes, Agenda, Integrations, Assistant, Communications, Files** — três chamadas cada: `Add<Módulo>Persistence()`, `Add<Módulo>Infrastructure()`, `Add<Módulo>Application()`.
4. `builder.AddPandoraOutbox()` — a família Messaging (outbox in-process). Registrado **depois** de todos os módulos, porque precisa do assembly de contratos e da chave de banco de cada um já conhecidos.
5. `AddTarsLocalization()` + localização por módulo (`AddPandoraLocalization()`, `AddFinancesLocalization()`) e `AddTarsProblemDetails()` — preocupações da família Web/error-mapping.
6. Presentation: `AddControllers()` seguido de um `Add<Módulo>PresentationPart()` por módulo, na mesma ordem; depois API versioning, Swagger, forwarded headers, CORS — não é Tars, mas vem depois dele.

Um módulo novo adiciona as três chamadas no fim do passo 3 e a presentation part no fim do passo 6.

Duas chamadas de middleware (não mostradas acima, na montagem do pipeline): `app.UseTarsCorrelationId()`
e `app.UseTarsUserContext()`.

---

## Família por família: qual arquivo registra o quê

### Observability

- **Arquivo:** [`Shared/Pottmayer.Pandora.Shared.Infrastructure/DI/SharedInfrastructureDI.cs`](../../../backend/src/Shared/Pottmayer.Pandora.Shared.Infrastructure/DI/SharedInfrastructureDI.cs)
- **Chamadas:** `AddTarsObservabilityOptions`, `AddTarsObservabilityResource`, `AddTarsTracing`, `AddTarsAspNetCoreTracing`, `AddTarsHttpClientTracing`, `AddTarsTracingOtlpExporter`, `AddTarsMetrics`, `AddTarsAspNetCoreMetrics`, `AddTarsHttpClientMetrics`, `AddTarsRuntimeMetrics`, `AddTarsMetricsOtlpExporter`, `AddTarsLogging`, `AddTarsLoggingOtlpExporter`, `UseTarsCorrelationId`.
- Segue a ordem canônica documentada em [tars/docs/observability/configuration.md](../../../../tars/docs/observability/configuration.md) — pipeline → instrumentação → exporter, por sinal. `ILogger` nativo, não Serilog.

### User Context

- **Arquivo:** o mesmo `SharedInfrastructureDI.cs`.
- **Chamadas:** `AddTarsClaimsUserResolver`, `AddTarsDefaultUserContextFactory`, `AddTarsUserContextAccessor`, `AddTarsCurrentPrincipalAccessor`. Middleware: `UseTarsUserContext` (no `Program.cs`).
- Um registro só, compartilhado por todo módulo — módulos leem o usuário atual via `IUserContext<T>`, não registram por conta própria.

### Data (relacional)

- **Arquivo de infra cross-módulo:** [`Shared/Pottmayer.Pandora.Shared.Persistence/DI/SharedPersistenceDI.cs`](../../../backend/src/Shared/Pottmayer.Pandora.Shared.Persistence/DI/SharedPersistenceDI.cs) — `AddTarsDataContextAccessor`, `AddTarsDataContextFactory`, `AddTarsRelationalConfigurationConnectionResolver`, `AddTarsUnitOfWorkFactory`. Registrado uma vez para o host inteiro.
- **Por módulo** (`Modules/<Nome>/Pottmayer.Pandora.Modules.<Nome>.Persistence/DI/PersistenceDI.cs`, um em cada módulo): `AddTarsRelationalData<...DbContext>(...)` e `AddTarsDataRepositoriesFromAssemblies(...)`. Cada módulo tem seu próprio `DbContext` e seu próprio schema PostgreSQL — ver `data-model.md` do módulo.

### Security / Identity

- **Arquivo:** [`Modules/Identity/Pottmayer.Pandora.Modules.Identity.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Identity/Pottmayer.Pandora.Modules.Identity.Infrastructure/DI/InfrastructureDI.cs)
- **Chamadas:** `AddTarsIdentityOptions`, `AddTarsIdentityAspNetCoreOptions`, `AddTarsIdentityJwtTokenIssuer`, `AddTarsIdentityJwtTokenValidator`, `AddTarsIdentityRefreshTokenService`, `AddTarsIdentityInMemoryTokenRevocationStore`, `AddTarsIdentityTokenRevocationService`, `AddTarsIdentityTokenDeliveryPolicy`, `AddTarsIdentityAspNetCoreTokenTransport`, `AddTarsIdentityAspNetCoreJwtBearer` e `AddTarsIdentityApiKey(DeviceAuthorization.Scheme)` — as chaves de dispositivo (`X-Api-Key`) do Pandora Desktop, um esquema que só os endpoints que o nomeiam aceitam.
- Só o módulo Identity mexe nessa família — ele é dono da emissão/validação de JWT e das chaves de dispositivo de ponta a ponta. Ver [docs do módulo Identity](../../modules/identity/pt-BR/README.md).

### Security / DataProtection

- **Arquivo:** [`Modules/Integrations/Pottmayer.Pandora.Modules.Integrations.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Integrations/Pottmayer.Pandora.Modules.Integrations.Infrastructure/DI/InfrastructureDI.cs)
- **Chamadas:** `AddTarsDataProtectionOptions`, `AddTarsSecretProtector` — `ISecretProtector`, usado para encriptar credenciais OAuth em repouso.
- Só o Integrations usa essa família, exatamente por isso: é o único módulo guardando segredo de terceiro.

### Core (Mediator/CQRS)

- **Arquivos:** o `Modules/<Nome>/Pottmayer.Pandora.Modules.<Nome>.Application/DI/ApplicationDI.cs` de cada módulo chama `AddTarsMediator(...)`, restrito ao assembly de handlers daquele módulo.
- Não existe registro global único de mediator — cada módulo varre o próprio assembly, então um handler de um módulo nunca fica visível para outro por acidente.

### Localization

- **Nível host:** `AddTarsLocalization()` no `Program.cs`, mais `AddPandoraLocalization()` (mensagens do host) e `AddFinancesLocalization()` (fonte de mensagens própria do módulo Finances, ligada via `Modules/Finances/Pottmayer.Pandora.Modules.Finances.Infrastructure/DI/LocalizationDI.cs` — o único módulo com fonte de localização própria hoje).
- Tudo mais usa `AddTarsHttpErrorMapper`/`AddTarsMessageSource` de `Host/Pottmayer.Pandora.Host/Localization/LocalizationDI.cs`.

### Messaging (outbox in-process)

- **Arquivo:** [`Host/Pottmayer.Pandora.Host/OutboxRegistration.cs`](../../../backend/src/Host/Pottmayer.Pandora.Host/OutboxRegistration.cs)
- **Chamadas:** `AddTarsOutboxOptions`, `AddTarsIntegrationEventTypeRegistry` (um evento representativo por assembly de contratos: Identity, Channels, Integrations, Communications), `AddTarsIntegrationEventDispatcher`, `AddTarsIntegrationEventSerializer`, `AddTarsOutboxBus`, `AddTarsOutboxStore`, depois `AddTarsOutboxRelay(...)` uma vez por banco produtor (Identity, Channels, Agenda, Integrations, Assistant, Communications).
- Cada `DbContext` de módulo produtor (`IdentityDbContext`, `ChannelsDbContext`, `AgendaDbContext`, `IntegrationsDbContext`, `AssistantDbContext`, `CommunicationsDbContext`) também chama `AddTarsOutbox(...)` em si mesmo para mapear a tabela de outbox — ver cada arquivo. Um módulo que passa a publicar precisa dos três: o `AddTarsOutbox` no `DbContext`, um relay, e o assembly de contratos no registry se for novo. Nenhum broker é registrado (`AddTarsMassTransitRabbitMq` aparece só num comentário de código em `OutboxRegistration.cs`, documentando como ficaria a troca se um módulo for extraído para um serviço — não é chamado). Mecanismo completo: [Arquitetura de mensageria](messaging.md) e [tars/docs/messaging/outbox.md](../../../../tars/docs/messaging/outbox.md).

### Communication

- **Arquivo:** [`Modules/Channels/Pottmayer.Pandora.Modules.Channels.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Channels/Pottmayer.Pandora.Modules.Channels.Infrastructure/DI/InfrastructureDI.cs)
- **Chamadas:** `AddTarsMailKitEmailOptions`, `AddTarsMailKitEmailSender`, `AddTarsLoggingEmailSender` (a fake, só dev), `AddTarsTelegramOptions`, `AddTarsTelegramHttpClient`, `AddTarsTelegramClientFactory`.
- Só o Channels fala com essa família — é dono do e-mail de saída e do bot do Telegram. O Communications lê e envia o email do próprio usuário (API do Gmail, IMAP/SMTP via MailKit) por adaptadores próprios, não por essa família.

### AI

- **Arquivo:** [`Modules/Assistant/Pottmayer.Pandora.Modules.Assistant.Infrastructure/DI/InfrastructureDI.cs`](../../../backend/src/Modules/Assistant/Pottmayer.Pandora.Modules.Assistant.Infrastructure/DI/InfrastructureDI.cs)
- **Chamadas:** `AddTarsAiChatGeminiOptions`, `AddTarsAiChatGeminiHttpClient`, `AddTarsAiChatCompletionClientGemini`, `AddTarsAiClientFactory`.
- Só o Assistant usa. Nenhuma API key é configurada: a chave Gemini de cada usuário vem do Integrations e vai na requisição; a seção de options (`Tars:Ai:Chat:Gemini`) só leva a URL base e o timeout.

### Web / HTTP

- **Arquivo:** `Host/Pottmayer.Pandora.Host/Localization/LocalizationDI.cs` (`AddTarsHttpErrorMapper`) mais `AddTarsProblemDetails()` no `Program.cs`.
- O Pandora usa um subconjunto mais leve do `Web.Http` do que o exemplo do próprio `application-blueprint.md` do framework — sem filtros de response-wrapping, só error mapping e problem details no formato RFC 7807.

### Caching, Multitenancy

- **Não usados.** Não existe nenhuma chamada `AddTarsMemoryCache*`/`AddTarsRedis*`/`AddTarsTenant*` em lugar nenhum do backend. O Pandora é intencionalmente single-tenant (ver [messaging.md §2](messaging.md#2-a-decisão-in-process-um-processo-só) para o mesmo raciocínio "um usuário, um deploy" aplicado à mensageria).

---

## Mantendo esta página atualizada

Esta página foi gerada lendo o código, não o contrário — se um módulo adicionar ou remover uma chamada
`AddTars*`, esta página fica desatualizada até alguém corrigir. Ainda não existe checagem automática
para isso. Na dúvida, `grep -rhoE "AddTars[A-Za-z]+|UseTars[A-Za-z]+" backend/src --include=*.cs | sort -u`
a partir da raiz do repo é a fonte da verdade.
