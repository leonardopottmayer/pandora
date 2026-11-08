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

## Comments and block dates

| Method | Route | Purpose |
|---|---|---|
| GET | `/pages/{id}/comments` | The page's threads and replies, oldest first |
| POST | `/pages/{id}/comments` | `{ blockId, quote, body }` starts a thread; `quote` (up to 500) is null for the whole block |
| POST | `/comments/{id}/replies` | `{ body }`; answering a reply answers **422** |
| PUT | `/comments/{id}` | `{ body }`, up to 5000 characters |
| PUT | `/comments/{id}/resolved` | `{ resolved }`, threads only |
| DELETE | `/comments/{id}` | A reply, or a thread with its replies |
| GET | `/pages/{id}/block-stamps` | Each block's `createdAt`, `updatedAt` and `updatedSource` (`edit`, `assistant`, `restore`) |

## Page settings, recent pages and trash

| Method | Route | Purpose |
|---|---|---|
| PUT | `/pages/{id}/appearance` | `{ cover: { mediaId \| gradient, position } \| null, width, font, smallText }`; an unknown value answers **422** |
| PUT | `/pages/{id}/lock` | `{ locked }` |
| PUT | `/pages/{id}/verification` | `{ verified, days }`, `days` 1 to 365 or null for no expiry |
| PUT | `/pages/{id}/public` | `{ public }`; the token comes back in `publicToken` |
| POST | `/pages/{id}/viewed` | Stamps the visit, for the recent pages |
| GET | `/pages/recent?limit=` | The pages opened last (up to 50) |
| GET | `/trash` | Deleted pages, each with how many sub-pages went with it |
| POST | `/trash/{id}/restore` | Brings the page back with its sub-pages |
| DELETE | `/trash/{id}` · `/trash` | Deletes one for good, or empties the trash; answers how many pages went |
| GET | `/api/v1/public/notes/pages/{token}` | The page behind a public link, **no sign-in** |
| GET | `/api/v1/public/notes/pages/{token}/media/{mediaId}[/thumbnail]` | A library item the page uses, **no sign-in**; any other answers **404** |

## Links — `/links`

| Method | Route | Purpose |
|---|---|---|
| GET | `/links/preview?url=` | What a web page says about itself: `title`, `description`, `siteName`, `imageUrl`, `iconUrl`, and `embed` (oEmbed: `type`, `html`, `url`, `width`, `height`) when the site offers one. Not an http(s) link answers **422**; a page that cannot be read **404** |

The server reads the page because the browser cannot across sites. It connects only to public
addresses (loopback, private ranges, link-local and the cloud metadata address are refused, also after
a redirect), follows at most 4 redirects, reads at most 768 KB of HTML and gives up after 8 seconds.
X and Bluesky are asked through their oEmbed endpoints directly; any other site through the oEmbed
link its page declares.

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

## AI — `/ai`

`POST /ai/transform` takes `{ action, text, language?, context? }` and returns `{ text }`.
`action`: `summarize`, `improve`, `translate` or `continue`. Text and context are each limited to 8000
characters; translation requires `language` (up to 80). Continue accepts empty text with nonempty context.
Requires `notes.pages.update` and `assistant.commands.run`, Notes and Assistant features enabled, and
an enabled profile with a selected key. Generates a preview only: no page or conversation writes.
Invalid input, missing configuration and provider failures return **422** with a localized message.

`PUT /pages/{id}` also takes optional `source: "edit" | "assistant"` (default `edit`) for the version
and changed blocks' origin. Other sources return **422**. Page content remains
`content: { version: 1, blocks: [...] }`.

`POST /pages/{id}/ai/summary` takes no body and returns `{ text }`. Requires `notes.pages.read`,
`notes.pages.update` and `assistant.commands.run`. Reads only the user's saved page, including derived
text from tables/properties/chips, without following page links. Missing, foreign or deleted page:
**404**. Empty text or more than 32000 text characters: **422** (`Notes.InvalidPageSummary`), without
calling AI. Reuses the Assistant's enabled profile, model, key and friendly provider errors. This
endpoint does not save a page or a version; acceptance uses the ordinary page PUT with `source: assistant`.

`POST /ai/ask` takes `{ question, spaceId? }` and returns `{ answer, sources[] }`. Requires
`notes.pages.read` and `assistant.commands.run` and their features. Question: 1–1000 nonblank characters.
Retrieves at most five matching owner-scoped, nondeleted pages in the optional space (archived included).
Each source is `{ pageId, spaceId, title, icon, excerpt }`; passages are capped at 4000 characters plus
truncation indicators. No hits or insufficient evidence returns `{ answer: null, sources: [] }`;
no hits makes no provider call. Malformed/uncited model output or an unconsulted source id returns
**422** (`Notes.InvalidAnswer`); invalid question: **422** (`Notes.InvalidQuestion`). Nothing is persisted.
Provider/profile errors use the existing friendly Assistant errors. Source ids are validated, while
answer accuracy remains subject to the model and the supplied passages.

`POST /ai/generate` takes `{ prompt }` (1–8000 nonblank characters) and returns `{ text, generatedAt }`,
with `generatedAt` in UTC from the server's `TimeProvider`. Requires `notes.pages.update`, plus the
Assistant's run permission, enabled feature/profile and selected key through `ITextCompletion`.
Only the prompt is sent; no page context or tools. Invalid prompt: **422** (`Notes.InvalidPrompt`).
Provider errors reuse the existing friendly Assistant errors. This route saves nothing; accepting
an AI block result saves its `props.output` as text inlines and `props.generatedAt` through the normal
page PUT with `source: assistant`. Prompt and output are included in a page's public content.

## Telegram capture (internal port)

`/nota <text>` is accepted as text-only input on the linked `assistant` bot, also with `@botname`.
`INoteCapture` in Notes.Abstractions sends the authorized `CaptureNoteCommand`: requires
`assistant.commands.run`, `notes.pages.read/create/update` and `notes.spaces.read`, with their features.
No AI profile/key is needed. The body is literal text, 1–8000 characters, appended with the received
date/time in the account zone. Creates a dedicated Capture page in the default space on first use.
Renames/moves keep the destination; a locked inbox refuses capture; a deleted one is replaced.
The stable inbound event id is recorded in `nte015_note_capture`, preventing duplicate content/version
even after page purge. Replies use Channels' existing outbox flow. There is no new HTTP route.

## Page templates (B10)

- `PUT /pages/{id}/template { isTemplate }`: owner-scoped, Pages.Update. Returns the page. A setting change does not create a content version.
- `GET /pages`: summaries include `isTemplate`; the existing space/archive/tag filters still apply. The creation dialog uses all active owned templates, across spaces.
- `POST /pages/from-template { templateId, title, spaceId?, folderId? }`: Pages.Read and Pages.Create. Requires an owned, nondeleted, unarchived marked template; unavailable templates return 404. Destination ownership follows normal page creation. Copies content and icon only, with fresh nested block IDs and no comment anchors. Text expands `{{date}}` to account-local YYYY-MM-DD and `{{title}}` to the chosen title; the title itself accepts `{{date}}`. Normal history, tags, links, media and block stamps are created. The source remains untouched.

## Daily notes and Notes preferences (B10)

`POST /daily { date? }` returns the existing/new PageDto for that owner and calendar date
(YYYY-MM-DD). Omitted date resolves the account-local day. Requires Pages.Read/Create,
Folders.Create and Spaces.Read. The first page is created in the default space's Daily folder; see
implementation-status for rename/trash/concurrency behavior. An invalid date is 400.

`GET /preferences` requires notes.preferences.read and returns `{ dailyTemplateId: null | uuid }`.
No row returns defaults without writing.

`PUT /preferences { dailyTemplateId }` requires notes.preferences.update and notes.pages.read. A
model must be owned, active and marked as template; otherwise 404. Null clears it. Existing daily
pages are unaffected. These resources belong to the Notes feature.

## Atomic tree drops (B10)

`POST /tree/move { spaceId, kind, parentId?, folderId?, itemIds }` returns true. Kind is page or
folder; itemIds is the final ordered list of visible siblings (zero-based order_index). Items and
destination must belong to the user and named space. For pages, parentId means a page and folderId a
folder; they are mutually exclusive. For folders, parentId means a folder and folderId must be null.

Requires notes.spaces.read and the selected kind's notes.pages.update or notes.folders.update.
Invalid shape, duplicate/empty IDs or unknown kind returns 422; foreign/missing/out-of-space
resources 404; cycles 409; missing permission 403. Every item is validated before writes, then the
batch and page versions commit together. No partial moves on failure. The API does not move items
across spaces; the existing individual page move still handles that.

## Read-only folder properties (B10)

`GET /folders/{id}/properties?includeArchived=false&tagIds={uuid}&tagIds={uuid}` requires
notes.folders.read and notes.pages.read. An unavailable/foreign/deleted folder returns 404; missing
permission returns 403. Tags intersect, and archived pages are excluded by default. Only direct
nondeleted pages are returned, in order_index/id order, without subfolders/subpages.

Response: `{ columns: ["Status", "Owner"], pages: [{ page: PageSummaryDto, properties: { "Status":
"Pending", "Owner": "Ana" } }] }`. The union includes only selected pages. Pages without properties
stay in the result with an empty object; an empty folder/filter yields empty arrays. Keys are
trimmed and matched case-insensitively to the first column label. Repeated values join with
newlines. Properties are plain text, including the visible text of chips. The request does not save
content or create versions.

## Module text imports (B11)

Communications calls `Notes.Abstractions.Import.INoteImporter` with authenticated owner, title,
literal text, internal source path/title and optional destination. Notes creates ordinary paragraphs
and one source bookmark through ImportNoteCommand/CreatePageCommand. It returns only the committed
page id. This is a synchronous application port, not an integration event; it neither exposes Notes
internals nor accepts HTML/attachment bytes. See the Communications API's POST /messages/{id}/notes.

## Live Agenda block (B11)

No new Notes route. Stored block shape: `{ id, type: "agenda-list", props: { mode: "today" } }`, or
`{ mode: "tasks", listId }`. The private frontend reads the existing `/api/v1.0/agenda/today`,
`/api/v1.0/agenda/task-lists` and `/api/v1.0/agenda/tasks?listId=...` resources using normal authentication and
source-specific read permissions. Query results stay in the Agenda query cache and are never written
into page content. Public rendering issues no module query. See implementation status for
source/permission behavior.

## Live Finance block (B11)

No new Notes endpoint. Block type finance-summary stores mode account/card/expenses with
accountId/cardId or optional month (YYYY-MM). Private views use existing
/api/v1.0/finances/accounts, /accounts/{id}/balance, /cards, /cards/{id}/available-limit,
/transactions?from=...&to=...&kind=expense&status=posted and /categories/system /categories
(includeInactive=true). All transaction pages are fetched. Read permissions are source-specific;
expenses require transactions.read and categories.read. API authentication/owner scoping is
unchanged. Public views issue no Finance queries; results never enter page content.

## Upcoming Agenda events (B11)

No new Notes endpoint. The private events mode reads /api/v1.0/agenda/calendars and
/api/v1.0/agenda/events?from=<UTC>&to=<UTC>&calendarIds=<id>&calendarIds=<id>. Active owner
calendars are validated before mounting the events query. Permissions: agenda.events.read and
agenda.calendars.read, with Agenda enabled. Existing server expansion supplies
recurring/overridden occurrences. Notes applies an exclusive window end and hides ended/cancelled
events. See implementation status for account-zone/DST semantics and persisted filters. Public
views issue no source queries.
