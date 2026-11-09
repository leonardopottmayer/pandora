# Padrões de projeto e código

[Índice da documentação](../../README.md) · [English](../en/coding-standards.md)

Consulte antes de implementar ou revisar alterações. Leia um caso de uso equivalente no módulo
afetado antes de escrever código; siga suas convenções em vez de introduzir uma estrutura paralela.
Estas são as regras gerais; as regras específicas de cada módulo ficam na documentação dele. O
frontend tem página própria, [Arquitetura do frontend](frontend.md), e as telas seguem
[Pandora's look](../../../client-web/DESIGN.md).

## Como o trabalho é feito

- **Ler primeiro.** Seguir o fluxo de ponta a ponta (controller → handler → aggregate → repositório,
  ou página → hook → service) e achar o caso de uso existente mais parecido antes de escrever.
- **A menor mudança que resolve.** Sem abstração para um uso só, sem opção que ninguém pediu, sem
  tratamento para caso impossível. Reutilizar o que o código já tem.
- **Cirúrgico.** Cada linha alterada vem do pedido. Não reformatar nem "melhorar" o que está ao lado;
  apontar problemas sem relação em vez de corrigi-los.
- **Perguntar quando a decisão é do usuário.** Havendo alternativas reais, apresentá-las com um
  trade-off curto e uma recomendação. Senão, seguir o padrão óbvio e dizer qual foi.
- **Pronto é verificado** (ver Validação), não "compila".

## Backend: projetos

Cada módulo é uma família de projetos em `backend/src/Modules/<Nome>/`, com nome
`Pottmayer.Pandora.Modules.<Nome>.<Camada>`:

| Projeto | Guarda | Referencia |
|---|---|---|
| `Abstractions` | A identidade do módulo (`<Nome>Module`: `Name`, `DatabaseKey`, `Schema`), suas `Options` e os **ports que outros módulos podem chamar**, com seus modelos | nada do módulo |
| `Contracts` | Os **eventos de integração** que ele publica ou trata (records POCO) | abstrações de Messaging do Tars |
| `Domain` | Aggregates, value objects, erros de domínio, ports de repositório e de serviço | `Shared.Domain`, DDD/primitivas do Tars |
| `Application` | Commands, queries, handlers, DTOs, serviços de aplicação, handlers de eventos, ferramentas do assistente | Domain, Abstractions, Contracts, **só Abstractions/Contracts de outros módulos** |
| `Persistence` | `DbContext`, mappings EF (`EntityConfigs`), repositórios, interceptors | Domain, Abstractions, `Shared.Persistence` |
| `Infrastructure` | Adaptadores para fora (provedores, parsers, clientes HTTP), jobs em segundo plano, localização | Application, Domain |
| `Presentation` | Controllers e contratos de request HTTP | Application |

`backend/src/Shared` guarda o que todo módulo usa (`UserData`, `Email`, `IAuditable`, armazenamento de
arquivos, o interceptor de auditoria). `backend/src/Host` é a raiz de composição.

**Limites entre módulos.** Um módulo nunca referencia Domain, Application ou Persistence de outro e
nunca lê tabelas de outro. Ele fala com outro módulo só de dois jeitos:

- um **port** no `Abstractions` do outro módulo (`IUserPreferencesReader`,
  `IEffectiveTimeZoneResolver`, `IExternalCredentialProvider`) para uma pergunta com resposta imediata;
- um **evento de integração** num projeto `Contracts`, publicado no outbox, para um fato que
  aconteceu. Ver [Mensageria](messaging.md).

## Backend: responsabilidades e arquivos

| Local | Responsabilidade |
|---|---|
| `Presentation/Controllers` | Binding HTTP, usuário autenticado, envio ao mediator e mapeamento da resposta |
| `Presentation/Requests` | Contratos de entrada HTTP; não declarar dentro da controller |
| `Presentation/Filters` | Filtros HTTP; não colocar em `Controllers` |
| `Application/Commands/<UseCase>` | Escrita: `<UseCase>Command.cs` e `<UseCase>CommandHandler.cs` |
| `Application/Queries/<UseCase>` | Leitura: `<UseCase>Query.cs` e `<UseCase>QueryHandler.cs` |
| `Application/Dtos`, `Application/Services` | DTOs de resposta e auxiliares de orquestração da aplicação |
| `Application/Assistant` | As ferramentas do módulo para o Assistant (`IAssistantTool`) |
| `Domain/Aggregates`, `Domain/ValueObjects`, `Domain/Errors` | Estado e invariantes, tipos de valor e erros de domínio |
| `Domain/Ports` | Interfaces de repositório e de serviço (`Ports/Repositories`, `Ports/Services`) |
| `Persistence/EntityConfigs`, `Persistence/Repositories` | Mappings EF e implementações dos repositórios |
| `Infrastructure/Jobs` | Serviços em segundo plano |
| `*/DI` | Uma extensão `Add<Nome><Camada>()` por projeto |

- Cada novo caso de uso tem sua pasta, inclusive queries distintas de listagem/detalhe. O handler
  fica em arquivo separado. O record `Input` compartilha o arquivo do comando/query, como nos existentes.
- DTOs e tipos de domínio independentes ficam em arquivos próprios e nas pastas correspondentes.
  Não juntar records sem relação no arquivo de um aggregate. Projeções de repositório podem ficar
  junto ao port quando esse for o padrão do módulo.

### O formato de um caso de uso

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
            // ... muda o aggregate, atualiza, grava o evento de auditoria na mesma unidade de trabalho
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

- Controllers são `[ApiController] [Authorize] [ApiVersion("1.0")]` em
  `api/v{version:apiVersion}/<módulo>/<recurso>`, pegam o usuário de `IUserContextAccessor<UserData>`
  e não contêm regra de negócio, repositório nem EF.
- Falhas são `Result` com erros de domínio, nunca exceções para casos esperados. Cada módulo tem
  `Domain/Errors/<Coisa>Errors` com `Error.NotFound/Validation/Conflict("<Coisas>.<Motivo>", "mensagem")`;
  o error mapper transforma o tipo no status HTTP e localiza a mensagem.
- Aggregates mantêm setters privados e são criados por factories; o comportamento fica nos métodos
  deles (`statement.Close(timeProvider)`). Reutilizar os valores/constantes de domínio do módulo para
  estados e origens, sem repetir strings entre camadas.
- Handlers devolvem DTOs (`<Coisa>Dto.From(aggregate)`) ou um primitivo (`bool`, uma contagem, um id),
  nunca aggregates.

## Backend: tempo, options, DI e jobs

- Usar `TimeProvider`; armazenar instantes em UTC e interpretar datas do usuário no fuso dele
  (`IEffectiveTimeZoneResolver.ResolveTodayAsync`). Datas puras de calendário são `DateOnly`. Ler o
  [KI-001](../../known-issues.md) antes de mexer em datas.
- Uma classe ligada a uma seção do `appsettings` é `<Nome>Options`, com uma constante `SectionName`
  (`Pandora:Agenda`); fica no `Abstractions` do módulo.
- Cada projeto se registra pela sua extensão em `DI/<Camada>DI.cs`, que o `Program.cs` chama na ordem
  fixa dos módulos. Usar DI para serviços e interceptors; interceptors de banco ficam em
  `Persistence/Interceptors`. Seguir os lifetimes existentes.
- **Trabalho em segundo plano é um job sobre a tabela do próprio módulo**: um `BackgroundService` em
  `Infrastructure/Jobs` com `PeriodicTimer`, que abre um escopo novo e envia um comando CQRS a cada
  ciclo. O trabalho, as novas tentativas e o resultado ficam nas tabelas do módulo. Sem broker, sem
  agendar para outro módulo. Ver [Mensageria §3](messaging.md#3-assincronia-sem-broker).
- Handlers de eventos de integração são idempotentes (o outbox entrega pelo menos uma vez), de
  preferência por uma chave natural do próprio trabalho.

## Persistência e migrations

- Usar repositórios pela unidade de trabalho do módulo (`IUnitOfWorkFactory.ExecuteAsync(<Módulo>.DatabaseKey, ...)`).
  Gravar alterações relacionadas atomicamente, incluindo auditoria/histórico e eventos do outbox.
  Filtrar dados pelo dono (`FindByIdForUserAsync`); recurso de outro usuário é 404, não 403.
- Cada módulo tem seu `DbContext` e seu schema no PostgreSQL. Seguir os mappings EF e nomes SQL
  existentes: tabelas com o código e número do módulo (`fin008_transaction`), colunas em snake_case,
  constraints/índices nomeados e enums em string com hífen (`pending-confirmation`, `api-key`).
- Migrations são SQL puro rodado pelo migris, em `migrations/migrations/<módulo>/<timestamp>-<ação>/`
  com um `.up.sql` e um `.down.sql` (`20260616120001-create-table-fin008-transaction`).
- Pré-1.0: alterar o schema existente no `CREATE TABLE` original; tabelas novas recebem scripts próprios
  de criação/down. Não criar migrations incrementais de `ALTER` para tabelas existentes. Recriar um
  banco local com dados é uma operação destrutiva separada, não uma etapa automática de implementação.
- Reutilizar a integração com Tars e os limites dos módulos documentados em
  [Tars wiring](tars-wiring.md) e [Mensageria](messaging.md).

## Nomes e idioma

- Identificadores de código, nomes de arquivos e pastas, tabelas, colunas e rotas internas em inglês.
  A UI, as rotas que o navegador mostra e os comentários podem ser em português onde o código ao redor
  já é.
- Métodos C# assíncronos têm sufixo `Async`, inclusive auxiliares de teste e actions de controller.
- Seguir imports, nomes e formatação do entorno. Usar nomes descritivos; evitar instruções comprimidas
  e ternários aninhados que escondam os caminhos de loading, erro e sucesso.
- Comentários dizem **por quê**, não o quê: uma regra de negócio, uma restrição não óbvia, uma decisão.
  Tipos públicos e casos de uso ganham um `/// <summary>` de uma linha.

## Validação

- Criar testes de regressão para bugs de comportamento e testes relevantes para lógica não trivial.
- **Backend:** xUnit. `backend/tests/Pottmayer.Pandora.Modules.<Nome>.Tests` guarda testes unitários de
  aggregates, value objects, parsers e serviços, com fakes escritos à mão (`Fakes/`, ex.:
  `FixedTimeProvider`). `Pottmayer.Pandora.IntegrationTests` sobe o Host real contra PostgreSQL no
  Testcontainers, monta o schema a partir das migrations SQL e limpa os dados com Respawn; precisa de
  Docker. Rodar `dotnet test backend/Pottmayer.Pandora.slnx`, ou o projeto de teste afetado.
- **Frontend:** ver [Arquitetura do frontend § Checagens](frontend.md#checagens-antes-de-dar-uma-mudança-por-pronta).
- Revisar o diff contra estas regras; quantidade de testes não substitui verificar limites e localização
  dos arquivos. Informar o que foi realmente validado, o que não foi (por exemplo, não conferido no
  navegador) e as limitações restantes.

## Documentação

- Atualizar as docs do módulo afetado na mesma mudança: inglês é o principal, com os mesmos tópicos
  espelhados em pt-BR. O `implementation-status.md` do módulo diz o que está construído; o
  `product-plan.md`, só o que ainda falta. Usar o [índice da documentação](../../README.md). Planos de
  execução, handoffs de sessão e registros de construção passo a passo saem de `docs/` quando o trabalho
  termina: os arquivos de tópico descrevem o que existe, e o git guarda o histórico.
- Uma mudança transversal (uma chamada do Tars adicionada ou removida, um evento novo, um job novo)
  também atualiza [Tars wiring](tars-wiring.md) ou [Mensageria](messaging.md).

## Commits

- **Nunca commitar nem dar push sem pedido**, e a aprovação de um commit não vale para o próximo. Nunca
  adicionar trailer `Co-Authored-By`. Depois de um commit, mostrar a mensagem. (Essas regras também
  estão no `AGENTS.md`/`claude.md` da raiz.)
- Fazer stage dos caminhos da mudança pelo nome. Nunca commitar
  `backend/src/Host/Pottmayer.Pandora.Host/appsettings.json` nem `migrations/seed/seed.local.sql`
  (segredos locais), e nunca imprimi-los.
- Assunto: Conventional Commits com o módulo como escopo, no imperativo, minúsculo, sem ponto, em
  inglês: `feat(finances): redesign the recurrences screen and its forms`,
  `fix(notes): keep inline HTML when ...`, `docs: ...`, `perf(client-web): ...`.
- Corpo: prosa em inglês quebrada em cerca de 100 colunas. Primeiro o que estava errado ou faltando,
  depois o que a mudança faz, do ponto de vista do usuário antes do código. Última linha: o que foi
  validado (`Tests: 828 frontend tests, typecheck, eslint and antd lint clean.`).
