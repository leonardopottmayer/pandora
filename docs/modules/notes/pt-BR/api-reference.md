# Referência de API

[← Voltar ao índice](README.md)

Base: **`/api/v{version}/notes`** (hoje `v1`). Todo endpoint é autenticado e escopado ao usuário do
token. Recurso de outro usuário devolve **404** (não 403). Os controllers ficam em
`Presentation/Controllers`.

---

## Pages — `/pages`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/pages` | A árvore como lista plana (`PageSummaryDto[]`) |
| GET | `/pages/search` | Busca full-text |
| GET | `/pages/graph` | O grafo wiki inteiro |
| GET | `/pages/{id}` | Detalhe, com o corpo e as tags da page |
| GET | `/pages/{id}/backlinks` | Pages que referenciam esta |
| GET | `/pages/{id}/graph` | O grafo local em volta desta page |
| POST | `/pages` | Criar (raiz ou filha; pode nascer com conteúdo) |
| PUT | `/pages/{id}` | Atualizar título + ícone + corpo (o caminho do autosave) |
| POST | `/pages/{id}/move` | Reparentar e reordenar |
| POST | `/pages/{id}/favorite` · `/unfavorite` | Marcar / desmarcar |
| POST | `/pages/{id}/archive` · `/unarchive` | Arquivar / desarquivar |
| DELETE | `/pages/{id}` | Soft-deletar a page **e toda a sua subárvore** |

### Query params

| Rota | Parâmetro | Padrão | Notas |
|---|---|---|---|
| `GET /pages` | `includeArchived` | `false` | |
| `GET /pages` | `spaceId` | — | Só as pages daquele espaço; sem ele, de todos (o que a resolução de wikilink precisa). |
| `GET /pages` | `tagIds` | — | Repetível. Várias tags **intersectam**. Com filtro, a sidebar vira lista plana. |
| `GET /pages/search` | `q` | — | Vazio + `tagIds` lista as pages daquela tag por título. Com termo, prioriza título, depois relevância, título e id. Filtros antes do teto de 20. |
| `GET /pages/search` | `tagIds` | — | Repetível, intersectando. |
| `GET /pages/search` | `spaceId` | — | Só aquele espaço. |
| `GET /pages/graph` | `tagIds` | — | Corta os nós. |
| `GET /pages/graph` | `spaceId` | — | Só as pages daquele espaço. |
| `GET /pages/{id}/graph` | `depth` | `1` | Clampado em 1..5. |
| `GET /pages/{id}/graph` | `tagIds` | — | Corta os nós **antes** do passeio pela vizinhança. |

### Corpos de requisição

| Rota | Corpo |
|---|---|
| `POST /pages` | `CreatePageRequest { title, parentId?, icon?, contentMarkdown?, spaceId? }` — uma filha fica no espaço da mãe; sem espaço, o padrão |
| `PUT /pages/{id}` | `UpdatePageRequest { title, icon?, contentMarkdown }` |
| `POST /pages/{id}/move` | `MovePageRequest { parentId, orderIndex, spaceId? }` — um move que criaria ciclo é rejeitado |

## Espaços — `/spaces`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/spaces` | Listar (`includeArchived`, padrão `false`), o padrão primeiro, com `pageCount` e `lastEditedAt` |
| POST | `/spaces` | Criar — o primeiro do usuário vira o padrão |
| PUT | `/spaces/{id}` | Nome, ícone, cor, descrição |
| POST | `/spaces/{id}/default` | Tornar padrão (desarquiva se preciso) |
| POST | `/spaces/{id}/archive` · `/unarchive` | Arquivar / desarquivar — o padrão responde **409** |
| DELETE | `/spaces/{id}` | Soft-deletar — o padrão ou um espaço com pages responde **409** |

Corpo de POST e PUT: `SaveSpaceRequest { name, icon?, color?, description? }`; nome vazio ou cor que
não seja `#rrggbb` responde **422**. Ver [Espaços](spaces.md).

## Tags — `/tags`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/tags` | Listar, com `pageCount` por tag |
| PUT | `/tags/{id}/color` | Definir ou limpar a cor |

**Não há POST nem DELETE**, por design: a tag é criada pelo markdown que a menciona e removida pela
varredura que a encontra órfã. Corpo: `SetTagColorRequest { color }` — só hex (`#rgb` / `#rrggbb` /
`#rrggbbaa`); valor inválido responde **422**.

## Mídia — `/media` e `/media-folders`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/media` | Listar itens: `folderId` (os da pasta), `unsorted=true`, ou nenhum (todos); `kind`, `q` (trecho do nome) e `unused=true` estreitam. Cada item traz `usageCount` |
| GET | `/media/lookup` | Itens por `ids` (repetível), excluídos incluídos — para os cartões de arquivo de uma page |
| GET | `/media/{id}/usages` | As pages que mostram o item |
| POST | `/media` | Upload (`multipart/form-data`: `file`, `folderId` opcional) — até 25 MB; vídeo responde **422** |
| PUT | `/media/{id}` | Renomear (`{ name }`) |
| POST | `/media/{id}/move` | Mover (`{ folderId }`, null para "Não organizadas") |
| DELETE | `/media/{id}` | Soft delete; os bytes continuam servidos |
| GET | `/media/{id}` · `/media/{id}/thumbnail` | Os bytes, ou os da miniatura (404 quando não há) |
| GET | `/media-folders` | Toda pasta com sua contagem, mais `unsortedCount`, `unusedCount` e `totalCount` |
| POST | `/media-folders` | Criar (`{ name, parentId? }`) |
| PUT | `/media-folders/{id}` | Renomear (`{ name }`) |
| POST | `/media-folders/{id}/move` | Mover (`{ parentId }`) — para dentro de si ou de uma subpasta responde **409** |
| DELETE | `/media-folders/{id}` | Excluir, subindo o que ela tem um nível |

Os bytes saem com `Content-Disposition: inline` (nome original) e
`Cache-Control: private, max-age=31536000, immutable`. Como são autenticados e o token não é cookie, o
cliente busca o blob e entrega um object URL. Ver [Biblioteca de mídia](media-library.md).

## Comentários e datas dos blocos

| Método | Rota | Para quê |
|---|---|---|
| GET | `/pages/{id}/comments` | As conversas e respostas da página, das mais antigas para as novas |
| POST | `/pages/{id}/comments` | `{ blockId, quote, body }` começa uma conversa; `quote` (até 500) é null para o bloco inteiro |
| POST | `/comments/{id}/replies` | `{ body }`; responder uma resposta responde **422** |
| PUT | `/comments/{id}` | `{ body }`, até 5000 caracteres |
| PUT | `/comments/{id}/resolved` | `{ resolved }`, só conversas |
| DELETE | `/comments/{id}` | Uma resposta, ou uma conversa com as respostas |
| GET | `/pages/{id}/block-stamps` | `createdAt`, `updatedAt` e `updatedSource` (`edit`, `assistant`, `restore`) de cada bloco |

## Configurações da página, recentes e lixeira

| Método | Rota | Para quê |
|---|---|---|
| PUT | `/pages/{id}/appearance` | `{ cover: { mediaId \| gradient, position } \| null, width, font, smallText }`; um valor desconhecido responde **422** |
| PUT | `/pages/{id}/lock` | `{ locked }` |
| PUT | `/pages/{id}/verification` | `{ verified, days }`, `days` de 1 a 365 ou null sem validade |
| PUT | `/pages/{id}/public` | `{ public }`; o token volta em `publicToken` |
| POST | `/pages/{id}/viewed` | Grava a visita, para as recentes |
| GET | `/pages/recent?limit=` | As páginas abertas por último (até 50) |
| GET | `/trash` | Páginas excluídas, cada uma com quantas subpáginas foram junto |
| POST | `/trash/{id}/restore` | Traz a página de volta com as subpáginas |
| DELETE | `/trash/{id}` · `/trash` | Exclui uma para sempre, ou esvazia a lixeira; responde quantas páginas saíram |
| GET | `/api/v1/public/notes/pages/{token}` | A página de um link público, **sem login** |
| GET | `/api/v1/public/notes/pages/{token}/media/{mediaId}[/thumbnail]` | Um item da biblioteca que a página usa, **sem login**; qualquer outro responde **404** |

## Links — `/links`

| Método | Rota | Para quê |
|---|---|---|
| GET | `/links/preview?url=` | O que uma página da web diz de si: `title`, `description`, `siteName`, `imageUrl`, `iconUrl` e `embed` (oEmbed: `type`, `html`, `url`, `width`, `height`) quando o site oferece. Um link que não é http(s) responde **422**; uma página que não pôde ser lida, **404** |

Quem lê a página é o servidor, porque o navegador não consegue entre sites. Ele só conecta em
endereços públicos (loopback, faixas privadas, link-local e o endereço de metadados da nuvem são
recusados, também depois de um redirecionamento), segue no máximo 4 redirecionamentos, lê no máximo
768 KB de HTML e desiste depois de 8 segundos. X e Bluesky são consultados direto pelo oEmbed deles;
os outros sites, pelo link de oEmbed que a página declara.

---

## DTOs de resposta

| DTO | Formato |
|---|---|
| `PageSummaryDto` | `Id, SpaceId, ParentId, Title, Slug, Icon, OrderIndex, IsFavorite, IsArchived` |
| `PageDto` | Os campos do `PageSummaryDto` + `ContentMarkdown, CreatedAt, UpdatedAt, Tags[]` |
| `SpaceDto` | `Id, Name, Icon, Color, Description, IsDefault, IsArchived, PageCount, LastEditedAt, CreatedAt` |
| `PageTagDto` | `Id, Slug, Name, Color` — as tags de uma page, sem contagem |
| `TagDto` | `PageTagDto` + `PageCount` — uma tag colorida pode ficar em zero |
| `PageSearchResultDto` | `Id, SpaceId, Title, Slug, Icon, IsArchived, Excerpt` |
| `BacklinkDto` | `PageId, Title, Slug, Icon, IsArchived, Kind` |
| `PageGraphDto` | `Nodes[]`, `Edges[]` |
| `GraphNodeDto` | `Id, Title, Slug, Icon, IsArchived, Degree, TagIds[]` |
| `GraphEdgeDto` | `SourceId, TargetId, Kind` |
| `MediaItemDto` | `Id, FolderId, Kind, Name, OriginalFileName, ContentType, SizeBytes, Width, Height, Url, ThumbnailUrl, UsageCount, IsDeleted, CreatedAt` |
| `MediaUsageDto` | `PageId, SpaceId, Title, Icon, IsArchived` |
| `MediaFolderDto` | `Id, ParentId, Name, ItemCount` |
| `MediaFoldersDto` | `Folders[], UnsortedCount, UnusedCount, TotalCount, TotalBytes, UnusedBytes` |

## IA — `/ai`

`POST /ai/transform` recebe `{ action, text, language?, context? }` e responde `{ text }`.
`action`: `summarize`, `improve`, `translate` ou `continue`. Texto e contexto têm até 8000 caracteres
cada; tradução exige `language` (até 80). Continuar aceita texto vazio com contexto não vazio.
Exige `notes.pages.update` e `assistant.commands.run`, flags Notes e Assistant ativas e perfil ligado,
com chave selecionada. Só gera uma prévia: não grava página nem conversa. Entrada inválida,
configuração ausente e falhas do provedor retornam **422**, com mensagem localizada.

`PUT /pages/{id}` também aceita `source: "edit" | "assistant"`, opcional, padrão `edit`, para a origem
na versão e nos blocos alterados. Outras origens respondem **422**. O corpo da página continua
sendo `content: { version: 1, blocks: [...] }`.

`POST /pages/{id}/ai/summary` não recebe corpo e responde `{ text }`. Exige `notes.pages.read`,
`notes.pages.update` e `assistant.commands.run`. Lê somente a página salva do usuário, com texto
derivado de tabelas/propriedades/chips, sem seguir links de página. Página ausente, alheia ou excluída:
**404**. Texto vazio ou acima de 32000 caracteres: **422** (`Notes.InvalidPageSummary`), sem chamar IA.
Reutiliza perfil ligado, modelo, chave e erros amigáveis do Assistant. Não salva página nem versão;
aceitar usa o PUT normal da página com `source: assistant`.

`POST /ai/ask` recebe `{ question, spaceId? }` e responde `{ answer, sources[] }`. Exige
`notes.pages.read` e `assistant.commands.run` e suas flags. Pergunta: 1–1000 caracteres, não só espaços.
Busca até cinco páginas do dono, não excluídas, no espaço opcional (arquivadas incluídas). Cada fonte é
`{ pageId, spaceId, title, icon, excerpt }`; trechos têm até 4000 caracteres mais indicadores de corte.
Sem resultados ou evidência suficiente responde `{ answer: null, sources: [] }`; sem resultados não
chama o provedor. Saída malformada/sem citações ou id não consultado: **422** (`Notes.InvalidAnswer`);
pergunta inválida: **422** (`Notes.InvalidQuestion`). Nada é persistido. Falhas de provedor/perfil usam
os erros amigáveis do Assistant. Os ids são validados; a precisão da resposta depende do modelo e dos trechos.

`POST /ai/generate` recebe `{ prompt }` (1–8000 caracteres, não só espaços) e responde `{ text, generatedAt }`,
com `generatedAt` UTC do `TimeProvider` do servidor. Exige `notes.pages.update` e, via `ITextCompletion`,
permissão de execução, flag/perfil ligado e chave selecionada do Assistant. Só o prompt é enviado;
sem contexto da página ou tools. Prompt inválido: **422** (`Notes.InvalidPrompt`). Falhas de provedor
reutilizam os erros amigáveis do Assistant. A rota não grava nada; aceitar salva `props.output` como
inlines de texto e `props.generatedAt` pelo PUT normal da página com `source: assistant`.
Prompt e resultado fazem parte do conteúdo público de uma página compartilhada.

## Captura Telegram (port interno)

`/nota <texto>` é aceito como mensagem só de texto no bot `assistant` vinculado, também com `@botname`.
`INoteCapture` em Notes.Abstractions envia o `CaptureNoteCommand` autorizado: exige
`assistant.commands.run`, `notes.pages.read/create/update` e `notes.spaces.read`, com suas flags.
Não exige perfil/chave de IA. O corpo é texto literal de 1–8000 caracteres, acrescentado com data/hora
de recebimento no fuso da conta. Cria página Captura dedicada no espaço padrão no primeiro uso.
Renomear/mover mantém destino; caixa travada recusa; excluída é substituída. O id estável do evento
fica em `nte015_note_capture`, evitando duplicação de conteúdo/versão mesmo após purgar a página.
Respostas usam o fluxo existente de outbox Channels. Não há rota HTTP nova.

## Modelos de página (B10)

- `PUT /pages/{id}/template { isTemplate }`: página do usuário, Pages.Update. Retorna a página. A configuração não cria versão de conteúdo.
- `GET /pages`: resumos incluem `isTemplate`; filtros de espaço/arquivo/tag continuam valendo. O seletor usa todos os modelos ativos do usuário, entre espaços.
- `POST /pages/from-template { templateId, title, spaceId?, folderId? }`: Pages.Read e Pages.Create. Exige modelo marcado, ativo e do usuário; indisponível retorna 404. Destino validado pelo fluxo normal. Copia apenas conteúdo e ícone, com IDs novos nos blocos aninhados e sem âncoras de comentários. Expande `{{date}}` em AAAA-MM-DD no fuso da conta e `{{title}}` no título escolhido; o próprio título aceita `{{date}}`. Cria histórico, tags, links, mídia e datas dos blocos pelo fluxo normal. Modelo original preservado.

## Notas diárias e preferências Notes (B10)

`POST /daily { date? }` retorna PageDto existente/novo por usuário e data de calendário
(AAAA-MM-DD). Data ausente usa hoje no fuso da conta. Exige Pages.Read/Create, Folders.Create e
Spaces.Read. A primeira nota vai para a pasta Diário do espaço padrão; ver implementation-status
para renomear/lixeira/concorrência. Data inválida retorna 400.

`GET /preferences` exige notes.preferences.read e retorna `{ dailyTemplateId: null | uuid }`.
Ausência de registro retorna padrões sem gravar.

`PUT /preferences { dailyTemplateId }` exige notes.preferences.update e notes.pages.read. Modelo
deve ser ativo, marcado e do usuário; senão 404. Null limpa a escolha. Notas diárias existentes são
preservadas. Recursos vinculados à feature Notes.

## Arraste atômico da árvore (B10)

`POST /tree/move { spaceId, kind, parentId?, folderId?, itemIds }` retorna true. Kind = page ou
folder; itemIds contém irmãos visíveis na ordem final (order_index a partir de zero). Itens e
destino devem ser do usuário e do espaço informado. Para página, parentId = página e folderId =
pasta; mutuamente exclusivos. Para pasta, parentId = pasta e folderId deve ser null.

Exige notes.spaces.read e notes.pages.update ou notes.folders.update conforme tipo. Formato
inválido, IDs vazios/repetidos ou tipo desconhecido: 422; recurso ausente/alheio/fora do espaço:
404; ciclo: 409; sem permissão: 403. Valida tudo antes de escrever; lote e versões de página são
gravados juntos. Falha não deixa movimento parcial. A rota não move entre espaços; movimento
individual de página continua atendendo esse caso.

## Propriedades da pasta, somente leitura (B10)

`GET /folders/{id}/properties?includeArchived=false&tagIds={uuid}&tagIds={uuid}` exige
notes.folders.read e notes.pages.read. Pasta ausente/alheia/excluída retorna 404; sem permissão,
403. Tags intersectam; arquivadas ficam de fora por padrão. Retorna apenas páginas diretamente na
pasta, não excluídas, ordenadas por order_index/id, sem subpastas/subpáginas.

Resposta: `{ columns: ["Status", "Responsável"], pages: [{ page: PageSummaryDto, properties: {
"Status": "Pendente", "Responsável": "Ana" } }] }`. União inclui somente páginas selecionadas.
Páginas sem propriedades permanecem com objeto vazio; pasta/filtro vazio retorna arrays vazios.
Nomes aparados e agrupados sem distinguir maiúsculas/minúsculas, usando primeiro rótulo. Valores
repetidos separados por quebras de linha. Valores em texto, inclusive texto visível de chips.
Consulta não salva conteúdo nem cria versões.

## Importação de texto de módulos (B11)

Communications chama `Notes.Abstractions.Import.INoteImporter` com dono autenticado, título, texto
literal, caminho/título de origem interna e destino opcional. Notes cria parágrafos comuns e
bookmark de origem pelo ImportNoteCommand/CreatePageCommand. Retorna apenas ID da página gravada.
Porta síncrona de aplicação, não evento de integração; sem expor internos do Notes ou aceitar
HTML/bytes de anexos. Ver POST /messages/{id}/notes na API Communications.

## Bloco da Agenda (B11)

Sem rota Notes nova. Bloco salvo: `{ id, type: "agenda-list", props: { mode: "today" } }` ou `{
mode: "tasks", listId }`. Front privado consulta recursos existentes `/api/v1.0/agenda/today`,
`/api/v1.0/agenda/task-lists` e `/api/v1.0/agenda/tasks?listId=...`, com autenticação e permissões da origem.
Resultados ficam no cache Agenda, sem gravar no conteúdo da página. Render público não consulta o
módulo. Ver status de implementação para origens/permissões.

## Bloco de Finanças (B11)

Sem endpoint Notes novo. Tipo finance-summary salva mode account/card/expenses com
accountId/cardId ou month opcional (YYYY-MM). Visão privada usa /api/v1.0/finances/accounts,
/accounts/{id}/balance, /cards, /cards/{id}/available-limit,
/transactions?from=...&to=...&kind=expense&status=posted e /categories/system /categories
(includeInactive=true). Busca todas as páginas de lançamentos. Permissões por origem; despesas
exigem transactions.read e categories.read. Autenticação/escopo do dono existentes. Página pública
não consulta Finanças; resultados não entram no conteúdo.

## Próximos eventos da Agenda (B11)

Sem endpoint Notes novo. Modo privado consulta /api/v1.0/agenda/calendars e
/api/v1.0/agenda/events?from=<UTC>&to=<UTC>&calendarIds=<id>&calendarIds=<id>. Valida calendários
ativos do dono antes da consulta de eventos. Permissões: agenda.events.read e
agenda.calendars.read, com Agenda habilitada. Servidor existente expande recorrências/edições.
Notes aplica fim exclusivo e oculta encerrados/cancelados. Ver status para fuso/horário de verão e
filtros persistidos. Página pública não consulta origem.
