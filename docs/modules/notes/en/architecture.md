# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Blocks & Editor](blocks.md)

---

## 1. Project layout

The module mirrors the other modules, split into layered projects under `backend/src/Modules/Notes/`:

```
Pottmayer.Pandora.Modules.Notes.
  Abstractions      → NotesModule, NotesFeatures, NotesPermissions, and the ports other modules call:
                      INoteImporter (Communications' "save to Notes"), INoteCapture (Telegram /nota)
  Application       → Commands, Queries, Dtos, Services, Assistant tools, DI
  Contracts         → empty: the module publishes no integration events
  Domain            → Aggregates, ValueObjects, Errors, Ports (Repositories)
  Infrastructure    → ImageSharp thumbnails, the link previewer (HTTP, SSRF-guarded)
  Persistence       → EntityConfigs, Repositories, the version interceptor, NotesDbContext, DI
  Presentation      → Controllers, Requests, DI
```

DDD aggregates with private constructors and static factories, a `TimeProvider` for every time read,
and one folder per use case in the application layer. Every write goes through a command handler in a
unit of work; reads go through query handlers returning DTOs. The module has **no background jobs**.

Frontend: `client-web/src/modules/notes`, split into `pages/` (routes), `components/` (with
`components/visual/` for the editor and `components/library/` for media), `hooks/`, `services/`,
`lib/` (pure logic, unit-tested) and `models.ts`.

## 2. Domain building blocks

### Aggregates (`Domain/Aggregates`)

| Aggregate | Responsibility / key invariants |
|---|---|
| **Space** | A group of pages; at most one default per user. |
| **Folder** | A container in one space, in a tree by `parent_id`; never inside a page. |
| **Page** | The block document plus title, icon, slug (fixed at creation), place in the tree, states (favorite, archived, deleted, template) and settings (appearance, lock, verification, viewed at, public token). `history_number` counts its versions. |
| **PageVersion** | An immutable snapshot of a page after a save that changed it. |
| **PageLink** | A derived edge between two pages, kind `wikilink` or `embed`. |
| **Tag** / **PageTag** | A user label and the derived fact that a page carries it. |
| **PageMedia** | The derived fact that a page uses a library item. |
| **BlockStamp** | When a block was written and last edited, and the source of that edit. |
| **PageComment** | A comment or a reply, anchored to a block and optionally to quoted words. |
| **MediaItem**, **MediaFolder**, **MediaRendition** | The library: items, their folders, their thumbnails. |
| **NotesPreferences** | The daily template and the daily folder. |
| **DailyNote** | Which page is the daily note of a date, per user. |
| **NoteCapture** | A receipt per Telegram capture, so a redelivered event appends nothing. |

Two **pure helpers** sit beside them, kept in the domain so they stay unit-testable:
`PageHierarchy` (cycle check, subtree walk) and `PageGraph.Neighborhood` (undirected BFS for the
local graph).

### Value objects (`Domain/ValueObjects`)

| Type | Role |
|---|---|
| `PageContent` | Parses and validates a document, and derives in one walk: the search text, mentioned page ids, linked page ids (page-link and synced-ref blocks), tags, media ids, block fingerprints, page properties, and a readable text for the Assistant. Also builds a document from plain text, appends documents, and copies a template with fresh ids. |
| `PageAppearance` | Cover (library image or gradient, position), width, font, small text. |
| `PageSnapshot` | What a version stores. |
| `PageVersionSource` | `initial`, `edit`, `assistant`, `restore`. |
| `PageLinkKind` | `wikilink` \| `embed`. |
| `PageProperty` | One row of a properties block, as plain text. |
| `Slugger` | Title → slug: lower-case, accents stripped, non-alphanumerics collapsed to hyphens, 80 chars at most. |
| `TagName` | Tag text → slug: lower-case, accents stripped, keeps `/`, `-` and `_`, 50 chars at most, needs a letter. |
| `PageSearch` | The search box → a safe prefix `tsquery`, plus the excerpt around the first match. |
| `NoteQuestionSearch` | A question → an OR `tsquery` without common question words, for ask your notes. |
| `MediaKind` | `image`, `file`, `video` (video refused on upload for now). |

### Ports

`I…Repository` per aggregate in `Domain/Ports/Repositories`, plus `IImageProcessor` (ImageSharp) and
the link previewer. Bytes go through the shared `IFileStorage` port keyed by `NotesModule.DatabaseKey`;
its database backend writes `nte003_file_blob`. AI calls go through `ITextCompletion` in
`Assistant.Abstractions`.

### Application services (`Application/Services`)

| Service | Role |
|---|---|
| `PageLinkSynchronizer` | Rebuilds the edges leaving a page from its document, by diff. |
| `PageTagSynchronizer` | The same for tags, creating tags the document introduced and sweeping uncolored ones no page carries any more. |
| `PageMediaSynchronizer` | The same for media use (blocks and the cover). |
| `BlockStampSynchronizer` | Compares each block's fingerprint with the stored one and stamps new and edited blocks. |
| `PageDeletion` | Trash and delete-for-good for a page and its sub-pages. |
| `SpaceResolver`, `SpaceValidation` | The default space, and the checks for a page's or folder's space. |
| `TagFilter` | "Which pages carry all of these tags?" — one rule for the sidebar, search and graph. |
| `PageTagReader` | The page's tags for handlers that did not touch the document. |
| `NoteImporter`, `NoteCaptureService` | The `INoteImporter` and `INoteCapture` ports. |
| `NoteAnswerParser` | Validates the model's answer for ask your notes. |

### Versions without a call in every handler

`PageVersionSaveChangesInterceptor` (Persistence) watches the unit of work: whenever a page's
versioned fields change, it adds a snapshot in the same transaction and bumps `history_number`. So
every path writes history the same way: the editor, the Assistant, moves of a subtree, a restore, a
capture.

## 3. Key design decisions

| # | Decision | Rationale (rejected alternative) |
|---|---|---|
| **D1** | One `jsonb` document per page, stable id per block. | A page loads and saves as one unit, and derived data is rebuilt from it. Rejected one row per block. |
| **D2** | TipTap in the frontend, with a converter (`lib/blocks.ts`) between TipTap's JSON and Pandora's blocks. | The database does not depend on the editor. Rejected writing an editor engine. |
| **D3** | Derived rows are reconciled by **diff**, not wiped and re-inserted. | Same idempotent result, and it never trips a unique index inside one transaction. |
| **D4** | Links point at page ids. | A rename never breaks a link; nothing resolves titles. |
| **D5** | The backend validates the shape, not each block type. | New blocks are frontend-only work. |
| **D6** | The tree (folders, parent pages) and the links are separate systems. | The tree is filing (acyclic), links are meaning (cyclic). |
| **D7** | Deleting is soft and goes to the trash, done in the command, not by a database cascade. | Sub-pages leave and come back together, and history survives. |
| **D8** | `search_vector` is a generated column over `title` and `content_text`. | Postgres keeps it in sync; configuration `simple` because the notebook mixes Portuguese and English. |
| **D9** | Bytes sit behind `IFileStorage`, with backend and key on each media row. | Adding a bucket later needs no migration. |
| **D10** | The local graph is cut in memory. | A personal notebook is small; BFS in C# stays testable. |
| **D11** | Media are served by an authenticated endpoint; a public page serves only the items it uses. | The bytes are the user's. |
| **D12** | A synced copy is read-only; the original is edited where it lives. | Two pages never write the same block. |
| **D13** | Live blocks (Agenda, Finances) store only their configuration. | Figures never enter the document, its versions or a public page. |

## 4. Cross-cutting rules

- **Multi-tenant by user.** Every aggregate table carries `user_id`, or reaches it through its page.
  Every endpoint is scoped to the token's user; another user's resource returns **404**.
- **Permissions and features.** Every command and query declares its `notes.*` permission
  (`NotesPermissions`) and the Notes feature; actions that touch another module also need that
  module's permission and feature. See [Admin](../../admin/README.md).
- **`TimeProvider` everywhere.** No aggregate reads `DateTime.Now`.
- **Limits.** A document is at most 2 MB, 10,000 blocks and 24 levels deep.
