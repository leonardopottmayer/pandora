# API Reference

[← Back to index](../README.md)

Base path: **`/api/v{version}/notes`** (today `v1`). Every endpoint is authenticated and scoped to the
token's user. A resource owned by another user returns **404** (not 403). Controllers live in
`Presentation/Controllers`.

---

## Pages — `/pages`

| Method | Route | Purpose |
|---|---|---|
| GET | `/pages` | The tree as a flat list (`PageSummaryDto[]`) |
| GET | `/pages/search` | Full-text search |
| GET | `/pages/graph` | The whole wiki graph |
| GET | `/pages/{id}` | Detail, including the body and the page's tags |
| GET | `/pages/{id}/backlinks` | Pages that reference this one |
| GET | `/pages/{id}/graph` | The local graph around this page |
| POST | `/pages` | Create (root or child; may be born with content) |
| PUT | `/pages/{id}` | Update title + icon + body (the autosave path) |
| POST | `/pages/{id}/move` | Reparent and reorder |
| POST | `/pages/{id}/favorite` · `/unfavorite` | Flag / unflag |
| POST | `/pages/{id}/archive` · `/unarchive` | Archive / unarchive |
| DELETE | `/pages/{id}` | Soft-delete the page **and its whole subtree** |

### Query parameters

| Route | Parameter | Default | Notes |
|---|---|---|---|
| `GET /pages` | `includeArchived` | `false` | |
| `GET /pages` | `tagIds` | — | Repeatable. Several tags **intersect**. With a filter the sidebar is a flat list. |
| `GET /pages/search` | `q` | — | Empty + `tagIds` lists that tag's pages. Cap 20, ordered by title. |
| `GET /pages/search` | `tagIds` | — | Repeatable, intersecting. |
| `GET /pages/graph` | `tagIds` | — | Cuts the nodes. |
| `GET /pages/{id}/graph` | `depth` | `1` | Clamped to 1..5. |
| `GET /pages/{id}/graph` | `tagIds` | — | Cuts the nodes **before** the neighborhood walk. |

### Request bodies

| Route | Body |
|---|---|
| `POST /pages` | `CreatePageRequest { title, parentId?, icon?, contentMarkdown? }` |
| `PUT /pages/{id}` | `UpdatePageRequest { title, icon?, contentMarkdown }` |
| `POST /pages/{id}/move` | `MovePageRequest { parentId, orderIndex }` — a move that would create a cycle is rejected |

## Tags — `/tags`

| Method | Route | Purpose |
|---|---|---|
| GET | `/tags` | List, with `pageCount` per tag |
| PUT | `/tags/{id}/color` | Set or clear the color |

There is **no POST and no DELETE**, by design: a tag is created by the markdown that mentions it and
removed by the sweep that finds it orphaned. Body: `SetTagColorRequest { color }` — hex only
(`#rgb` / `#rrggbb` / `#rrggbbaa`); an invalid value answers **422**.

## Attachments — `/attachments`

| Method | Route | Purpose |
|---|---|---|
| POST | `/attachments` | Upload (`multipart/form-data`: `file`, optional `pageId`) |
| GET | `/attachments/{id}` | Download the bytes |

The download responds with the stored `Content-Type` and `Content-Disposition: inline` carrying the
original filename. Because it is authenticated and the token is not a cookie, a browser navigation or
a bare `<img src>` will not reach it — the client fetches the blob and hands over an object URL. See
[Attachments](attachments.md#4-the-consequence-the-browser-cannot-fetch-an-attachment-by-itself).

---

## Response DTOs

| DTO | Shape |
|---|---|
| `PageSummaryDto` | `Id, ParentId, Title, Slug, Icon, OrderIndex, IsFavorite, IsArchived` |
| `PageDto` | `PageSummaryDto` fields + `ContentMarkdown, CreatedAt, UpdatedAt, Tags[]` |
| `PageTagDto` | `Id, Slug, Name, Color` — one page's tags, no count |
| `TagDto` | `PageTagDto` + `PageCount` — a colored tag may sit at zero |
| `PageSearchResultDto` | `Id, Title, Slug, Icon, IsArchived, Excerpt` |
| `BacklinkDto` | `PageId, Title, Slug, Icon, IsArchived, Kind` |
| `PageGraphDto` | `Nodes[]`, `Edges[]` |
| `GraphNodeDto` | `Id, Title, Slug, Icon, IsArchived, Degree` |
| `GraphEdgeDto` | `SourceId, TargetId, Kind` |
| `AttachmentDto` | `Id, PageId, FileName, ContentType, SizeBytes, Url, CreatedAt` |
