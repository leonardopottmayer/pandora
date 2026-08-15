# Arquitetura

[← Voltar ao índice](README.md) · Relacionados: [Modelo de dados](data-model.md), [Visão geral](overview.md)

---

## 1. Estrutura dos projetos

O módulo espelha o módulo Finances, dividido em projetos por camada sob `backend/src/Modules/Notes/`:

```
Pottmayer.Pandora.Modules.Notes.
  Abstractions      → NotesModule (nome, chave de banco, schema) compartilhado entre as camadas
  Application       → Commands, Queries, Dtos, Services, DI
  Contracts         → IntegrationEvents (vazio — o módulo não publica nenhum)
  Domain            → Aggregates, ValueObjects, Errors, Ports (Repositories)
  Infrastructure    → DI (sem jobs e sem parsers externos — o módulo não tem nenhum dos dois)
  Persistence       → EntityConfigs, Repositories, Storage, NotesDbContext, DI
  Presentation      → Controllers, Requests, DI
```

Estilo de design: **aggregates de DDD** com construtor privado + factories estáticas, um
`TimeProvider` injetado para toda leitura de tempo, e uma camada de aplicação **command/query** (uma
pasta por caso de uso). Toda escrita passa por um command handler dentro de uma unit of work; as
leituras passam por query handlers que devolvem DTOs.

Frontend: `client-web/src/modules/notes`, dividido em `pages/` (rotas), `components/`, `hooks/`
(TanStack Query), `services/` (HTTP), `lib/` (lógica pura, com testes unitários) e `models.ts`
(espelho dos DTOs).

## 2. Blocos do domínio

### Aggregates (`Domain/Aggregates`)

| Aggregate root | Responsabilidade / invariantes principais |
|---|---|
| **Page** | O documento markdown. Slug fixado na criação (um rename nunca quebra um link); `Move` guarda só o caso trivial de ser o próprio pai e delega a checagem real de ciclo a `PageHierarchy`; arquivar e deletar são timestamps, não booleanos. |
| **PageLink** | Uma aresta do grafo wiki. Criada e removida, nunca editada — por isso só carrega `CreatedAt`. Ciclos permitidos. |
| **Tag** | Um rótulo do usuário. A identidade é o `Slug`; `Name` registra a primeira grafia; `Color` é o único campo mutável, e `HasUserMetadata` é o que faz uma tag vazia valer a pena manter. |
| **PageTag** | O fato de uma page carregar uma tag. Derivado e imutável, como `PageLink`. |
| **Attachment** | Metadados de um arquivo subido + o par `(StorageBackend, StorageKey)` que localiza os bytes. Write-once. |

Dois desses são **helpers puros**, não aggregates — lógica sem estado que precisa do conjunto inteiro
em vez de uma linha, mantida no domínio para continuar testável:

- `PageHierarchy.WouldCreateCycle` — sobe a partir do pai pretendido sobre um mapa de links de pai;
  há ciclo se o passeio chegar na page que está sendo movida.
- `PageGraph.Neighborhood` — BFS não-direcionada com visited set, devolvendo as pages dentro de N
  saltos.

### Value objects (`Domain/ValueObjects`)

| Tipo | Papel |
|---|---|
| `PageLinkKind` | `wikilink` \| `embed`. |
| `Slugger` | Título → slug: minúsculas, acentos removidos, não-alfanuméricos colapsados em hífens únicos, teto de 80 chars. A unicidade é do chamador (precisa do repositório). |
| `WikilinkParser` | Corpo → lista de `WikilinkReference(Target, Kind)`, deduplicada por (alvo, tipo). |
| `TagName` | Texto da tag → slug. Mantém `/`, `-` e `_` (ao contrário do `Slugger`), teto de 50, e exige ao menos uma letra — então `#123` não é tag. |
| `TagParser` | Corpo → lista de `TagReference(Name, Slug)`. Remove código cercado e inline antes. |
| `PageSearch` | O que o usuário digitou → `tsquery` (`palavra:*` unidas por `&`), mais o excerpt cortado em volta do primeiro match (160 chars). |

### Ports (`Domain/Ports/Repositories`)

`IPageRepository`, `IPageLinkRepository`, `ITagRepository`, `IPageTagRepository`,
`IAttachmentRepository`. O storage tem o par de ports dele em `Persistence/Storage`: `IFileStorage`
(consumido pela aplicação) com `DatabaseFileStorage` sobre `IFileBlobRepository`.

O módulo **não declara domain services nem jobs** — nada aqui roda em schedule.

### Application services (`Application/Services`)

| Serviço | Papel |
|---|---|
| `PageLinkSynchronizer` | Reconstrói por diff as arestas que saem de uma page, a partir do corpo dela. |
| `PageTagSynchronizer` | O mesmo para tags, mais **criar** as tags que o texto inventou e **varrer** as que ele abandonou. |
| `PageTagReader` | Lê as tags de uma page para os handlers que não tocaram no corpo (abrir, mover, favoritar, arquivar). |
| `TagFilter` | Resolve "quais pages carregam todas essas tags?" — a regra única compartilhada pela sidebar, pela busca e pelo grafo. |

## 3. Decisões de design

| # | Decisão | Motivo (alternativa rejeitada) |
|---|---|---|
| **D1** | Links e tags são **escritos no markdown** e parseados pelo backend no save. | Mantém o corpo portável e o frontend simples. Rejeitado cadastrá-los em formulário/CRUD de junção, o que deixaria o metadado fora do arquivo exportado. |
| **D2** | Linhas derivadas são reconciliadas por **diff**, não apagadas e recriadas. | Mesmo resultado idempotente, e nunca esbarra no índice único apagando e reinserindo a mesma linha dentro de uma transação. |
| **D3** | A árvore e o grafo são sistemas separados sobre uma `Page`. | A árvore é decisão de arquivo (acíclica), o grafo é significado (cíclico). Rejeitado modelar links como arestas da árvore. |
| **D4** | O slug é congelado na criação. | Um rename não pode quebrar `[[links]]` nem URLs. Rejeitado re-sluggar no rename. |
| **D5** | Deletar é **soft delete da subárvore inteira**, feito no comando e não por cascade do banco. | Nenhum filho fica apontando para um pai deletado, e as linhas sobrevivem para a história. Rejeitado `ON DELETE CASCADE` (que é um hard delete da história). |
| **D6** | `search_vector` é uma **coluna gerada STORED**, não mantida por código de aplicação. | O Postgres a mantém em dia, então nenhum caminho de save pode esquecê-la. Configuração `simple` (só lower-case, sem stemming) porque o caderno mistura PT-BR e EN. |
| **D7** | O blob store fica atrás de `IFileStorage`, com o backend e a chave gravados **em cada anexo**. | Ligar o S3 depois não exige migration nem reescrita das linhas antigas — a leitura é auto-descritiva. |
| **D8** | O grafo local é cortado **em memória**, não com passeio recursivo em SQL. | Um caderno pessoal é pequeno; carregar as pages e arestas do usuário e rodar BFS mantém a lógica de profundidade fora do SQL e testável. |
| **D9** | Uma linha de tag sobrevive a perder a última page **só se tiver cor**. | A cor é a única coisa que o texto não recupera; sem ela, uma tag sem uso é só ruído nos filtros. |
| **D10** | Filtro com várias tags **intersecta** (E), igual nas três superfícies. | "Ir estreitando" é o comportamento esperado, e uma regra só em todo lugar é melhor que OU aqui e E ali. |
| **D11** | Anexos são servidos por um **endpoint autenticado**, nunca por path direto. | Os bytes são do usuário. A consequência é que o browser não consegue buscá-los sozinho — ver [Anexos](attachments.md#4-a-consequência-o-browser-não-alcança-um-anexo-sozinho). |

## 4. Regras transversais

- **Multi-tenant por usuário.** `nte001_page` e `nte005_tag` têm `user_id NOT NULL`; as tabelas
  derivadas chegam ao dono pela page delas. Todo endpoint é autenticado e escopado ao usuário do
  token; recurso de outro usuário devolve **404** (não 403).
- **`TimeProvider` em todo lugar.** Nenhum aggregate lê `DateTime.Now` direto, o que é o que torna os
  timestamps de arquivar/deletar testáveis.
- **Sem event log de auditoria.** Diferente do Finances, o módulo não tem tabela de `audit_event`.
  `Page` e `Tag` são `IAuditable` (`created_by/at`, `updated_by/at`); as tabelas de aresta derivada
  carregam só `created_at`, porque são reescritas em vez de editadas.
- **Espelhos dos parsers no frontend.** `lib/wikilinks.ts` e `lib/tags.ts` reimplementam os parsers do
  backend para o preview resolver uma referência exatamente como o próximo save vai resolver. São
  duplicação deliberada, cada uma coberta pelos próprios testes unitários.
