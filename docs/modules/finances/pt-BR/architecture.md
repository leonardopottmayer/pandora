# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Visão geral](overview.md)

---

## 1. Estrutura dos projetos

O módulo espelha os módulos Identity/Notifications, dividido em projetos por camada em
`backend/src/Modules/Finances/`:

```
Pottmayer.Pandora.Modules.Finances.
  Abstractions      → contrato público para outros módulos (registro do FinancesModule)
  Application       → Commands, Queries, Dtos, Services, Auditing, DI
  Contracts         → IntegrationEvents (atualmente vazio — ver Jobs e integração)
  Domain            → Aggregates, ValueObjects, Errors, Ports (Repositories + Services)
  Infrastructure    → Jobs (recorrência, ciclo de fatura, parsing de importação), parsers OFX/CSV, DI
  Persistence       → EntityConfigs, Repositories, FinancesDbContext, DI
  Presentation      → Controllers, Requests, DI
```

Estilo de design: **aggregates de DDD** com construtor privado + factory estática, um `TimeProvider`
injetado para toda leitura de tempo, e uma camada de aplicação **command/query** (uma pasta por caso
de uso). Toda escrita passa por um command handler; leituras passam por query handlers que retornam
DTOs.

## 2. Blocos de domínio

### Aggregates (`Domain/Aggregates`)

| Aggregate root | Responsabilidade / invariantes principais |
|---|---|
| **Account** | Config da conta. Moeda imutável após criação; conta arquivada rejeita mutações de negócio. |
| **Card** | Config do cartão. `closing_day`/`due_day` ∈ 1..28; moeda imutável; arquivado rejeita mutações. |
| **CardStatement** | Ciclo da fatura. Máquina de estados `open → closed → partially-paid/paid/overdue`; caches `total_amount`/`paid_amount` recomputados transacionalmente; deriva o status de valores + datas. |
| **Transaction** | Movimento no ledger. Exatamente um destino (conta XOR fatura, exceto `statement-writeoff` que não tem nenhum); `amount > 0`; `posted` imutável em valor/destino/kind; `void` terminal (mas reversível via `Restore`). |
| **InstallmentPlan** | Parcelamento. `count ≥ 2`; parcelas somam o total quando `origin = manual`; divisão de centavos determinística. |
| **UserCategory** | Categoria do usuário: hierarquia de 2 níveis (filho não tem filho); natureza do filho = do pai. |
| **Tag** / **TagLink** | Tag (nome único por usuário) + vínculo polimórfico a qualquer entidade. |
| **RecurringTransaction** | Template + regra. Campos estruturais imutáveis após criação; cursor `next_occurrence_on` só avança; pausada não gera. |
| **PendingTransaction** | Staging. `original_payload` imutável; `pending → approved/rejected` terminal. |
| **ImportFile** / **ImportRow** | Pipeline do arquivo (máquina de status, contadores) + linhas parseadas (bruto preservado). |
| **ImportLayout** | Perfil de parsing; layouts de sistema (`user_id NULL`) são somente-leitura para o usuário. |
| **SystemCategory** | Dado de referência/leitura (seed). Sem comportamento; lido via `ISystemCategoryReader`. |
| **AuditEvent** | Store append-only; gravado via repositório, nunca lido pelo domínio para decisões. |

### Value objects (`Domain/ValueObjects`)

`Money` (valor + moeda), `CurrencyCode` (ISO 4217 ou ticker crypto, validado por forma),
`TransactionKind`, `TransactionStatus`, `TransactionNature`, `AccountType`, `StatementStatus`,
`EntryOrigin`, `PendingTransactionStatus`, `RecurringTransactionStatus`, `RecurrenceFrequency`,
`RecurrenceRule`, `DedupStatus`, `ImportFileStatus`, `ImportRowStatus`, `LayoutFileFormat`,
`ImportLayoutAccountType`, `TaggableEntityType`, `SystemDescription`.

### Ports (`Domain/Ports`)

- **Repositórios:** `IAccountRepository`, `ICardRepository`, `ICardStatementRepository`,
  `ITransactionRepository`, `IRecurringTransactionRepository`, `IPendingTransactionRepository`,
  `IImportFileRepository`, `IImportRowRepository`, `IImportLayoutRepository`,
  `IUserCategoryRepository`, `ISystemCategoryReader`, `ITagRepository`, `ITagLinkRepository`,
  `IInstallmentPlanRepository`, `IAuditEventRepository`.
- **Serviços:** `IStatementResolver` (compra + cartão → fatura-alvo), `IImportParser`
  (estratégias OFX/CSV), `IDuplicateDetector` (certain / suspected / matched), `ILayoutDetector`
  (auto-escolha de layout para um arquivo enviado).

Serviços da camada de aplicação (`Application/Services`): `StatementAmountSync` (helper único de
recomputação de total/pago da fatura), `StatementResolver`, `StatementMaintenance`,
`InstallmentPlanAssembler`, `TagTargets`.

## 3. Decisões de design

| # | Decisão | Racional (alternativa descartada) |
|---|---|---|
| **D1** | Saldo é derivado do ledger (Σ valores postados com sinal), nunca campo armazenado. | Uma coluna `balance` mutável é a fonte clássica de inconsistência e é inauditável. |
| **D2** | Cartão não é conta; a fatura é a entidade central. Gasto de cartão vai para a fatura; só o pagamento toca uma conta. | Bate com o mental do usuário brasileiro. Descartado "cartão como conta de passivo" (pior UX de fatura/parcelamento). |
| **D3** | Transferência = duas transações ligadas (`transfer-out` + `transfer-in`, mesmo `transfer_group_id`). Cada transação afeta exatamente um destino. | Mantém todo saldo um somatório simples por conta. Descartada linha única `from/to` (quebra a regra uma-transação-um-destino). |
| **D4** | Staging unificado para tudo que é automático: importação e recorrência produzem `PendingTransaction`. | Um inbox, um fluxo de aprovação, uma auditoria. Descartado staging por origem. |
| **D5** | Categorias de sistema e do usuário em tabelas e colunas separadas. Um lançamento pode ter as duas. | Separa o ciclo de vida (seed por migration × CRUD do usuário). Descartada tabela única com flag `is_system`. |
| **D6** | Auditoria = proveniência estrutural (cadeia de FK) + event log append-only com diffs JSONB. | Cobre 100% do requisito sem event sourcing completo. |
| **D7** | Dinheiro como `NUMERIC(20,8)` + string de código de moeda. | Cobre BRL (2 casas) e crypto (8 casas). Descartado inteiro em unidades mínimas (ruim para expoentes variáveis de crypto). |
| **D8** | Fatura é aggregate próprio (não filho carregado com o cartão). | Faturas mudam com frequência e independentemente; carregar o cartão inteiro a cada lançamento seria contenção desnecessária. Consistência de limite × fatura é query, não invariante. |
| **D9** | Conciliação: importação confirma o que era esperado em vez de duplicar. Três níveis — duplicata certa (sem sugestão), suspeita (sugestão sinalizada), casada (confirmação de um lançamento esperado). | Descartado o binário "novo ou duplicata", que duplicaria recorrências/projeções sistematicamente. |

## 4. Regras transversais

- **Multi-tenant por usuário.** Toda tabela de dado do usuário tem `user_id NOT NULL`. Todo endpoint é
  autenticado e escopado ao usuário do token; recurso de outro usuário retorna **404** (não 403).
- **Auditoria em toda mutação.** Mutações relevantes gravam um `fin016_audit_event` na **mesma unidade
  de trabalho** da mudança — a auditoria nunca fica atrás do dado.
- **`TimeProvider` em todo lugar.** Nenhum aggregate lê `DateTime.Now` direto; o tempo é injetado, o
  que torna jobs e máquinas de estado testáveis.
