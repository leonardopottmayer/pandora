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
| `GraphNodeDto` | `Id, Title, Slug, Icon, IsArchived, Degree` |
| `GraphEdgeDto` | `SourceId, TargetId, Kind` |
| `MediaItemDto` | `Id, FolderId, Kind, Name, OriginalFileName, ContentType, SizeBytes, Width, Height, Url, ThumbnailUrl, UsageCount, IsDeleted, CreatedAt` |
| `MediaUsageDto` | `PageId, SpaceId, Title, Icon, IsArchived` |
| `MediaFolderDto` | `Id, ParentId, Name, ItemCount` |
| `MediaFoldersDto` | `Folders[], UnsortedCount, UnusedCount, TotalCount` |
