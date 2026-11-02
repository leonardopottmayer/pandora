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
| `GET /pages` | `spaceId` | — | That space's pages only; without it, every space's (what wikilink resolution needs). |
| `GET /pages` | `tagIds` | — | Repeatable. Several tags **intersect**. With a filter the sidebar is a flat list. |
| `GET /pages/search` | `q` | — | Empty + `tagIds` lists that tag's pages by title. Otherwise title matches first, then relevance, title and id. Filters apply before the cap of 20. |
| `GET /pages/search` | `tagIds` | — | Repeatable, intersecting. |
| `GET /pages/search` | `spaceId` | — | That space only. |
| `GET /pages/graph` | `tagIds` | — | Cuts the nodes. |
| `GET /pages/graph` | `spaceId` | — | That space's pages only. |
| `GET /pages/{id}/graph` | `depth` | `1` | Clamped to 1..5. |
| `GET /pages/{id}/graph` | `tagIds` | — | Cuts the nodes **before** the neighborhood walk. |

### Request bodies

| Route | Body |
|---|---|
| `POST /pages` | `CreatePageRequest { title, parentId?, icon?, contentMarkdown?, spaceId? }` — a child stays in its parent's space; with none, the default |
| `PUT /pages/{id}` | `UpdatePageRequest { title, icon?, contentMarkdown }` |
| `POST /pages/{id}/move` | `MovePageRequest { parentId, orderIndex, spaceId? }` — a move that would create a cycle is rejected |

## Spaces — `/spaces`

| Method | Route | Purpose |
|---|---|---|
| GET | `/spaces` | List (`includeArchived`, default `false`), the default first, with `pageCount` and `lastEditedAt` |
| POST | `/spaces` | Create — the user's first one becomes the default |
| PUT | `/spaces/{id}` | Name, icon, color, description |
| POST | `/spaces/{id}/default` | Make default (unarchiving it if needed) |
| POST | `/spaces/{id}/archive` · `/unarchive` | Archive / unarchive — the default answers **409** |
| DELETE | `/spaces/{id}` | Soft-delete — the default or a space with pages answers **409** |

Body of POST and PUT: `SaveSpaceRequest { name, icon?, color?, description? }`; a blank name or a
color that is not `#rrggbb` answers **422**. See [Spaces](spaces.md).

## Tags — `/tags`

| Method | Route | Purpose |
|---|---|---|
| GET | `/tags` | List, with `pageCount` per tag |
| PUT | `/tags/{id}/color` | Set or clear the color |

There is **no POST and no DELETE**, by design: a tag is created by the markdown that mentions it and
removed by the sweep that finds it orphaned. Body: `SetTagColorRequest { color }` — hex only
(`#rgb` / `#rrggbb` / `#rrggbbaa`); an invalid value answers **422**.

## Media — `/media` and `/media-folders`

| Method | Route | Purpose |
|---|---|---|
| GET | `/media` | List items: `folderId` (one folder's own), `unsorted=true`, or neither (all); `kind`, `q` (name fragment) and `unused=true` narrow. Each item carries `usageCount` |
| GET | `/media/lookup` | Items by `ids` (repeatable), deleted ones included — for a page's file cards |
| GET | `/media/{id}/usages` | The pages that show the item |
| POST | `/media` | Upload (`multipart/form-data`: `file`, optional `folderId`) — up to 25 MB; video answers **422** |
| PUT | `/media/{id}` | Rename (`{ name }`) |
| POST | `/media/{id}/move` | Move (`{ folderId }`, null for "Unsorted") |
| DELETE | `/media/{id}` | Soft-delete; the bytes keep being served |
| GET | `/media/{id}` · `/media/{id}/thumbnail` | The bytes, or the thumbnail's (404 when there is none) |
| GET | `/media-folders` | Every folder with its item count, plus `unsortedCount`, `unusedCount` and `totalCount` |
| POST | `/media-folders` | Create (`{ name, parentId? }`) |
| PUT | `/media-folders/{id}` | Rename (`{ name }`) |
| POST | `/media-folders/{id}/move` | Move (`{ parentId }`) — into itself or a subfolder answers **409** |
| DELETE | `/media-folders/{id}` | Delete, moving what it holds up a level |

The bytes come with `Content-Disposition: inline` (original file name) and
`Cache-Control: private, max-age=31536000, immutable`. Because they are authenticated and the token is
not a cookie, the client fetches the blob and hands over an object URL. See [Media Library](media-library.md).

---

## Response DTOs

| DTO | Shape |
|---|---|
| `PageSummaryDto` | `Id, SpaceId, ParentId, Title, Slug, Icon, OrderIndex, IsFavorite, IsArchived` |
| `PageDto` | `PageSummaryDto` fields + `ContentMarkdown, CreatedAt, UpdatedAt, Tags[]` |
| `SpaceDto` | `Id, Name, Icon, Color, Description, IsDefault, IsArchived, PageCount, LastEditedAt, CreatedAt` |
| `PageTagDto` | `Id, Slug, Name, Color` — one page's tags, no count |
| `TagDto` | `PageTagDto` + `PageCount` — a colored tag may sit at zero |
| `PageSearchResultDto` | `Id, SpaceId, Title, Slug, Icon, IsArchived, Excerpt` |
| `BacklinkDto` | `PageId, Title, Slug, Icon, IsArchived, Kind` |
| `PageGraphDto` | `Nodes[]`, `Edges[]` |
| `GraphNodeDto` | `Id, Title, Slug, Icon, IsArchived, Degree, TagIds[]` |
| `GraphEdgeDto` | `SourceId, TargetId, Kind` |
| `MediaItemDto` | `Id, FolderId, Kind, Name, OriginalFileName, ContentType, SizeBytes, Width, Height, Url, ThumbnailUrl, UsageCount, IsDeleted, CreatedAt` |
| `MediaUsageDto` | `PageId, SpaceId, Title, Icon, IsArchived` |
| `MediaFolderDto` | `Id, ParentId, Name, ItemCount` |
| `MediaFoldersDto` | `Folders[], UnsortedCount, UnusedCount, TotalCount, TotalBytes, UnusedBytes` |
