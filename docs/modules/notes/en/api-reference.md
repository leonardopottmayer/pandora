# API Reference

[← Back to index](../README.md)

Base path: **`/api/v{version}/notes`**. Every endpoint is authenticated and scoped to the token's user;
another user's resource returns **404**. Validation errors answer **422**, conflicts (cycles, a
non-empty space, a stale version) **409**. Each endpoint also needs its `notes.*` permission and the
Notes feature ([Admin](../../admin/README.md)). Controllers live in `Presentation/Controllers`.

---

## Pages — `/pages`

| Method | Route | Purpose |
|---|---|---|
| GET | `/pages?includeArchived=&tagIds=&spaceId=` | The tree as a flat list of summaries (no content) |
| GET | `/pages/{id}` | The page with its document and tags |
| POST | `/pages` | Create: `{ title, parentId?, icon?, content?, spaceId?, folderId? }` |
| PUT | `/pages/{id}` | Save (autosave): `{ title, icon?, content?, source? }`, `source` = `edit` (default) or `assistant` |
| POST | `/pages/{id}/move` | Move: `{ parentId?, orderIndex, spaceId?, folderId? }`; a space change takes the sub-pages |
| POST | `/pages/{id}/favorite` · `/unfavorite` | Favorite flag |
| POST | `/pages/{id}/archive` · `/unarchive` | Archive flag |
| DELETE | `/pages/{id}` | Send to the trash, with the sub-pages |
| PUT | `/pages/{id}/template` | `{ isTemplate }` |
| POST | `/pages/from-template` | `{ templateId, title, spaceId?, folderId? }` |
| GET | `/pages/recent?limit=` | Pages opened last (default 20) |
| POST | `/pages/{id}/viewed` | Stamp `viewed_at` (not an edit) |
| PUT | `/pages/{id}/appearance` | `{ cover?, width?, font?, smallText }` |
| PUT | `/pages/{id}/lock` | `{ locked }` |
| PUT | `/pages/{id}/verification` | `{ verified, days? }` (no `days` = no expiry) |
| PUT | `/pages/{id}/public` | `{ public }`; turning it on makes a new token |
| GET | `/pages/{id}/backlinks` | "Mentioned in" |
| GET | `/pages/search?q=&tagIds=&spaceId=` | Full-text search, 20 results at most; see [Search](search.md) |
| GET | `/pages/graph?tagIds=&spaceId=` | The global graph |
| GET | `/pages/{id}/graph?depth=&tagIds=` | The local graph, depth 1–5 |
| GET | `/pages/{id}/versions?before=` | Up to 100 version summaries, newest first |
| GET | `/pages/{id}/versions/{number}` | One complete snapshot |
| POST | `/pages/{id}/versions/{number}/restore` | `{ expectedVersion }`; see [Version History](version-history.md) |
| POST | `/pages/{id}/ai/summary` | Summary preview of the saved page; see [AI](ai.md) |

## Folders — `/folders`

| Method | Route | Purpose |
|---|---|---|
| GET | `/folders?spaceId=` | Folders of a space (or all) |
| POST | `/folders` | `{ name, icon?, description?, parentId?, spaceId? }` |
| PUT | `/folders/{id}` | Same body |
| POST | `/folders/{id}/move` | `{ parentId?, orderIndex }` |
| DELETE | `/folders/{id}` | Sends its subfolders and pages to the trash |
| GET | `/folders/{id}/properties?includeArchived=&tagIds=` | The table view: direct pages with their properties |

## Tree — `/tree`

| Method | Route | Purpose |
|---|---|---|
| POST | `/tree/move` | Atomic batch move from the sidebar: `{ spaceId, kind, parentId?, folderId?, itemIds }` |

## Spaces — `/spaces`

| Method | Route | Purpose |
|---|---|---|
| GET | `/spaces?includeArchived=` | Default first, then by name, with page count and last edit |
| POST | `/spaces` | `{ name, icon?, color?, description? }` |
| PUT | `/spaces/{id}` | Same body |
| POST | `/spaces/{id}/default` | Make it the default |
| POST | `/spaces/{id}/archive` · `/unarchive` | The default cannot be archived |
| DELETE | `/spaces/{id}` | Only when empty, never the default |

## Trash — `/trash`

| Method | Route | Purpose |
|---|---|---|
| GET | `/trash` | Pages in the trash |
| POST | `/trash/{id}/restore` | Restore a page with its sub-pages |
| DELETE | `/trash/{id}` | Delete one page for good |
| DELETE | `/trash` | Empty the trash |

## Daily notes and preferences

| Method | Route | Purpose |
|---|---|---|
| POST | `/daily` | `{ date? }`: open or create the daily note; see [Pages §9](pages-and-hierarchy.md#9-daily-notes) |
| GET | `/preferences` | The daily template |
| PUT | `/preferences` | `{ dailyTemplateId? }` |

## Tags — `/tags`

| Method | Route | Purpose |
|---|---|---|
| GET | `/tags` | The user's tags with their page counts |
| PUT | `/tags/{id}/color` | `{ color? }`, hex only |

There is no POST and no DELETE: pages create tags and the sweep removes them. See [Tags](tags.md).

## Comments

See [Comments §5](comments.md#5-api).

## Media — `/media`, `/media-folders`

| Method | Route | Purpose |
|---|---|---|
| GET | `/media?folderId=&unsorted=&kind=&q=&unused=` | List items |
| GET | `/media/lookup?ids=` | Many items at once, deleted ones included |
| GET | `/media/{id}/usages` | The pages using an item |
| POST | `/media` | Upload (`multipart/form-data`: `file`, `folderId?`) |
| PUT | `/media/{id}` | Rename: `{ name }` |
| POST | `/media/{id}/move` | `{ folderId? }` |
| DELETE | `/media/{id}` | Soft delete; the bytes stay served |
| GET | `/media/{id}` · `/media/{id}/thumbnail` | The bytes, cached as immutable |
| GET | `/media-folders` | Folders, with `TotalBytes` and `UnusedBytes` |
| POST | `/media-folders` | `{ name, parentId? }` |
| PUT | `/media-folders/{id}` | Rename: `{ name }` |
| POST | `/media-folders/{id}/move` | `{ parentId? }` |
| DELETE | `/media-folders/{id}` | Its contents move up to its parent |

## Links — `/links`

| Method | Route | Purpose |
|---|---|---|
| GET | `/links/preview?url=` | Title, description, image and oEmbed of a URL, for bookmarks and embeds; private addresses refused |

## AI — `/ai`

| Method | Route | Purpose |
|---|---|---|
| POST | `/ai/transform` | `{ action, text, language?, context? }`, action `summarize`, `improve`, `translate` or `continue` |
| POST | `/ai/ask` | `{ question, spaceId? }`: ask your notes |
| POST | `/ai/generate` | `{ prompt }`: the AI block |

See [AI](ai.md).

## Public pages — `/api/v{version}/public/notes/pages/{token}`

No authentication.

| Method | Route | Purpose |
|---|---|---|
| GET | `/` | The page, read-only |
| GET | `/media/{mediaId}` · `/media/{mediaId}/thumbnail` | An item, only if the page uses it |

## Not under `/notes`

`POST /api/v{version}/communications/messages/{id}/notes` saves an email as a page, through Notes'
`INoteImporter`. See [Communications](../../communications/README.md).
