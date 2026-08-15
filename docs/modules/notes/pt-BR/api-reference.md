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
| `GET /pages` | `tagIds` | — | Repetível. Várias tags **intersectam**. Com filtro, a sidebar vira lista plana. |
| `GET /pages/search` | `q` | — | Vazio + `tagIds` lista as pages daquela tag. Teto de 20, ordenado por título. |
| `GET /pages/search` | `tagIds` | — | Repetível, intersectando. |
| `GET /pages/graph` | `tagIds` | — | Corta os nós. |
| `GET /pages/{id}/graph` | `depth` | `1` | Clampado em 1..5. |
| `GET /pages/{id}/graph` | `tagIds` | — | Corta os nós **antes** do passeio pela vizinhança. |

### Corpos de requisição

| Rota | Corpo |
|---|---|
| `POST /pages` | `CreatePageRequest { title, parentId?, icon?, contentMarkdown? }` |
| `PUT /pages/{id}` | `UpdatePageRequest { title, icon?, contentMarkdown }` |
| `POST /pages/{id}/move` | `MovePageRequest { parentId, orderIndex }` — um move que criaria ciclo é rejeitado |

## Tags — `/tags`

| Método | Rota | Propósito |
|---|---|---|
| GET | `/tags` | Listar, com `pageCount` por tag |
| PUT | `/tags/{id}/color` | Definir ou limpar a cor |

**Não há POST nem DELETE**, por design: a tag é criada pelo markdown que a menciona e removida pela
varredura que a encontra órfã. Corpo: `SetTagColorRequest { color }` — só hex (`#rgb` / `#rrggbb` /
`#rrggbbaa`); valor inválido responde **422**.

## Anexos — `/attachments`

| Método | Rota | Propósito |
|---|---|---|
| POST | `/attachments` | Upload (`multipart/form-data`: `file`, `pageId` opcional) |
| GET | `/attachments/{id}` | Baixar os bytes |

O download responde com o `Content-Type` guardado e `Content-Disposition: inline` carregando o nome
original. Como é autenticado e o token não é cookie, uma navegação do browser ou um `<img src>` cru
não chegam nele — o cliente busca o blob e entrega um object URL. Ver
[Anexos](attachments.md#4-a-consequência-o-browser-não-alcança-um-anexo-sozinho).

---

## DTOs de resposta

| DTO | Formato |
|---|---|
| `PageSummaryDto` | `Id, ParentId, Title, Slug, Icon, OrderIndex, IsFavorite, IsArchived` |
| `PageDto` | Os campos do `PageSummaryDto` + `ContentMarkdown, CreatedAt, UpdatedAt, Tags[]` |
| `PageTagDto` | `Id, Slug, Name, Color` — as tags de uma page, sem contagem |
| `TagDto` | `PageTagDto` + `PageCount` — uma tag colorida pode ficar em zero |
| `PageSearchResultDto` | `Id, Title, Slug, Icon, IsArchived, Excerpt` |
| `BacklinkDto` | `PageId, Title, Slug, Icon, IsArchived, Kind` |
| `PageGraphDto` | `Nodes[]`, `Edges[]` |
| `GraphNodeDto` | `Id, Title, Slug, Icon, IsArchived, Degree` |
| `GraphEdgeDto` | `SourceId, TargetId, Kind` |
| `AttachmentDto` | `Id, PageId, FileName, ContentType, SizeBytes, Url, CreatedAt` |
