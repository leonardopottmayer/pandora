# Referência da API

[← Voltar ao índice](README.md)

Caminho base: **`/api/v{version}/notes`**. Todo endpoint é autenticado e restrito ao usuário do token;
recurso de outro usuário devolve **404**. Erros de validação respondem **422**, conflitos (ciclos, espaço
não vazio, versão desatualizada) **409**. Cada endpoint também exige sua permissão `notes.*` e a feature
do Notes ([Admin](../../admin/pt-BR/README.md)). Os controllers ficam em `Presentation/Controllers`.

---

## Páginas — `/pages`

| Método | Rota | Para quê |
|---|---|---|
| GET | `/pages?includeArchived=&tagIds=&spaceId=` | A árvore como lista plana de resumos (sem conteúdo) |
| GET | `/pages/{id}` | A página com o documento e as tags |
| POST | `/pages` | Criar: `{ title, parentId?, icon?, content?, spaceId?, folderId? }` |
| PUT | `/pages/{id}` | Salvar (autosave): `{ title, icon?, content?, source? }`, `source` = `edit` (padrão) ou `assistant` |
| POST | `/pages/{id}/move` | Mover: `{ parentId?, orderIndex, spaceId?, folderId? }`; trocar de espaço leva as subpáginas |
| POST | `/pages/{id}/favorite` · `/unfavorite` | Marca de favorita |
| POST | `/pages/{id}/archive` · `/unarchive` | Marca de arquivada |
| DELETE | `/pages/{id}` | Manda para a lixeira, com as subpáginas |
| PUT | `/pages/{id}/template` | `{ isTemplate }` |
| POST | `/pages/from-template` | `{ templateId, title, spaceId?, folderId? }` |
| GET | `/pages/recent?limit=` | As páginas abertas por último (padrão 20) |
| POST | `/pages/{id}/viewed` | Carimba `viewed_at` (não é edição) |
| PUT | `/pages/{id}/appearance` | `{ cover?, width?, font?, smallText }` |
| PUT | `/pages/{id}/lock` | `{ locked }` |
| PUT | `/pages/{id}/verification` | `{ verified, days? }` (sem `days` = sem validade) |
| PUT | `/pages/{id}/public` | `{ public }`; ligar gera um token novo |
| GET | `/pages/{id}/backlinks` | "Mencionada em" |
| GET | `/pages/search?q=&tagIds=&spaceId=` | Busca de texto, no máximo 20 resultados; ver [Busca](search.md) |
| GET | `/pages/graph?tagIds=&spaceId=` | O grafo global |
| GET | `/pages/{id}/graph?depth=&tagIds=` | O grafo local, profundidade 1–5 |
| GET | `/pages/{id}/versions?before=` | Até 100 resumos de versão, mais nova primeiro |
| GET | `/pages/{id}/versions/{number}` | Um snapshot completo |
| POST | `/pages/{id}/versions/{number}/restore` | `{ expectedVersion }`; ver [Histórico de versões](version-history.md) |
| POST | `/pages/{id}/ai/summary` | Prévia do resumo da página salva; ver [IA](ai.md) |

## Pastas — `/folders`

| Método | Rota | Para quê |
|---|---|---|
| GET | `/folders?spaceId=` | As pastas de um espaço (ou de todos) |
| POST | `/folders` | `{ name, icon?, description?, parentId?, spaceId? }` |
| PUT | `/folders/{id}` | Mesmo corpo |
| POST | `/folders/{id}/move` | `{ parentId?, orderIndex }` |
| DELETE | `/folders/{id}` | Manda as subpastas e páginas para a lixeira |
| GET | `/folders/{id}/properties?includeArchived=&tagIds=` | A visão de tabela: as páginas diretas com suas propriedades |

## Árvore — `/tree`

| Método | Rota | Para quê |
|---|---|---|
| POST | `/tree/move` | Movimento atômico em lote vindo da barra lateral: `{ spaceId, kind, parentId?, folderId?, itemIds }` |

## Espaços — `/spaces`

| Método | Rota | Para quê |
|---|---|---|
| GET | `/spaces?includeArchived=` | O padrão primeiro, depois por nome, com quantidade de páginas e última edição |
| POST | `/spaces` | `{ name, icon?, color?, description? }` |
| PUT | `/spaces/{id}` | Mesmo corpo |
| POST | `/spaces/{id}/default` | Tornar padrão |
| POST | `/spaces/{id}/archive` · `/unarchive` | O padrão não pode ser arquivado |
| DELETE | `/spaces/{id}` | Só vazio, nunca o padrão |

## Lixeira — `/trash`

| Método | Rota | Para quê |
|---|---|---|
| GET | `/trash` | As páginas na lixeira |
| POST | `/trash/{id}/restore` | Restaurar uma página com as subpáginas |
| DELETE | `/trash/{id}` | Excluir uma página de vez |
| DELETE | `/trash` | Esvaziar a lixeira |

## Notas diárias e preferências

| Método | Rota | Para quê |
|---|---|---|
| POST | `/daily` | `{ date? }`: abre ou cria a nota diária; ver [Páginas §9](pages-and-hierarchy.md#9-notas-diárias) |
| GET | `/preferences` | O modelo diário |
| PUT | `/preferences` | `{ dailyTemplateId? }` |

## Tags — `/tags`

| Método | Rota | Para quê |
|---|---|---|
| GET | `/tags` | As tags do usuário com a quantidade de páginas |
| PUT | `/tags/{id}/color` | `{ color? }`, só hex |

Não há POST nem DELETE: as páginas criam as tags e a varredura as remove. Ver [Tags](tags.md).

## Comentários

Ver [Comentários §5](comments.md#5-api).

## Mídia — `/media`, `/media-folders`

| Método | Rota | Para quê |
|---|---|---|
| GET | `/media?folderId=&unsorted=&kind=&q=&unused=` | Listar itens |
| GET | `/media/lookup?ids=` | Vários itens de uma vez, inclusive excluídos |
| GET | `/media/{id}/usages` | As páginas que usam um item |
| POST | `/media` | Upload (`multipart/form-data`: `file`, `folderId?`) |
| PUT | `/media/{id}` | Renomear: `{ name }` |
| POST | `/media/{id}/move` | `{ folderId? }` |
| DELETE | `/media/{id}` | Soft delete; os bytes continuam servidos |
| GET | `/media/{id}` · `/media/{id}/thumbnail` | Os bytes, em cache como imutáveis |
| GET | `/media-folders` | As pastas, com `TotalBytes` e `UnusedBytes` |
| POST | `/media-folders` | `{ name, parentId? }` |
| PUT | `/media-folders/{id}` | Renomear: `{ name }` |
| POST | `/media-folders/{id}/move` | `{ parentId? }` |
| DELETE | `/media-folders/{id}` | O conteúdo sobe para a pasta-pai |

## Links — `/links`

| Método | Rota | Para quê |
|---|---|---|
| GET | `/links/preview?url=` | Título, descrição, imagem e oEmbed de uma URL, para bookmarks e embeds; endereços privados recusados |

## IA — `/ai`

| Método | Rota | Para quê |
|---|---|---|
| POST | `/ai/transform` | `{ action, text, language?, context? }`, ação `summarize`, `improve`, `translate` ou `continue` |
| POST | `/ai/ask` | `{ question, spaceId? }`: pergunte às suas notas |
| POST | `/ai/generate` | `{ prompt }`: o bloco de IA |

Ver [IA](ai.md).

## Páginas públicas — `/api/v{version}/public/notes/pages/{token}`

Sem autenticação.

| Método | Rota | Para quê |
|---|---|---|
| GET | `/` | A página, somente leitura |
| GET | `/media/{mediaId}` · `/media/{mediaId}/thumbnail` | Um item, só se a página o usa |

## Fora de `/notes`

`POST /api/v{version}/communications/messages/{id}/notes` salva um email como página, pelo
`INoteImporter` do Notes. Ver [Communications](../../communications/pt-BR/README.md).
