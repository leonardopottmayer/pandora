# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Overview](overview.md)

---

## 1. Project layout

The module mirrors the Finances module, split into layered projects under
`backend/src/Modules/Notes/`:

```
Pottmayer.Pandora.Modules.Notes.
  Abstractions      → NotesModule (name, database key, schema) shared across the layers
  Application       → Commands, Queries, Dtos, Services, DI
  Contracts         → IntegrationEvents (empty — the module publishes none)
  Domain            → Aggregates, ValueObjects, Errors, Ports (Repositories)
  Infrastructure    → DI (no jobs and no external parsers — the module has neither)
  Persistence       → EntityConfigs, Repositories, Storage, NotesDbContext, DI
  Presentation      → Controllers, Requests, DI
```

Design style: **DDD aggregates** with private constructors + static factories, a `TimeProvider`
injected for all time reads, and a **command/query** application layer (one folder per use case).
Every write goes through a command handler inside a unit of work; reads go through query handlers
returning DTOs.

Frontend: `client-web/src/modules/notes`, split into `pages/` (routes), `components/`, `hooks/`
(TanStack Query), `services/` (HTTP), `lib/` (pure logic, unit-tested) and `models.ts` (DTO mirrors).

## 2. Domain building blocks

### Aggregates (`Domain/Aggregates`)

| Aggregate root | Responsibility / key invariants |
|---|---|
| **Page** | The markdown document. Slug fixed at creation (a rename never breaks a link); `Move` guards the trivial self-parent case and delegates the real cycle check to `PageHierarchy`; archive and delete are timestamps, not booleans. |
| **PageLink** | An edge of the wiki graph. Created and removed, never edited — so it carries only `CreatedAt`. Cycles allowed. |
| **Tag** | A user label. Identity is the `Slug`; `Name` records the first spelling; `Color` is the only mutable field, and `HasUserMetadata` is what makes an empty tag worth keeping. |
| **PageTag** | The fact that a page carries a tag. Derived, immutable, like `PageLink`. |
| **Attachment** | Metadata of an uploaded file + the `(StorageBackend, StorageKey)` pair locating the bytes. Write-once. |

Two of these are **pure helpers**, not aggregates — stateless logic that needs the whole set rather
than one row, kept in the domain so it stays unit-testable:

- `PageHierarchy.WouldCreateCycle` — walks upward from the prospective parent over a map of parent
  links; a cycle exists if the walk ever reaches the moving page.
- `PageGraph.Neighborhood` — undirected BFS with a visited set, returning the pages within N hops.

### Value objects (`Domain/ValueObjects`)

| Type | Role |
|---|---|
| `PageLinkKind` | `wikilink` \| `embed`. |
| `Slugger` | Title → slug: lower-cased, accents stripped, non-alphanumerics collapsed to single hyphens, capped at 80 chars. Uniqueness is the caller's job (it needs the repository). |
| `WikilinkParser` | Body → `WikilinkReference(Target, Kind)` list, deduplicated per (target, kind). |
| `TagName` | Tag text → slug. Keeps `/`, `-` and `_` (unlike `Slugger`), caps at 50, and requires at least one letter so `#123` is not a tag. |
| `TagParser` | Body → `TagReference(Name, Slug)` list. Strips fenced and inline code first. |
| `PageSearch` | What the user typed → `tsquery` (`word:*` joined by `&`), plus the excerpt cut around the first match (160 chars). |

### Ports (`Domain/Ports/Repositories`)

`IPageRepository`, `IPageLinkRepository`, `ITagRepository`, `IPageTagRepository`,
`IAttachmentRepository`. Storage has its own port pair in `Persistence/Storage`: `IFileStorage`
(consumed by the application) with `DatabaseFileStorage` over `IFileBlobRepository`.

The module declares **no domain services and no jobs** — nothing here runs on a schedule.

### Application services (`Application/Services`)

| Service | Role |
|---|---|
| `PageLinkSynchronizer` | Rebuilds the edges leaving a page from its body, by diff against what is stored. |
| `PageTagSynchronizer` | The same for tags, plus **creating** tags the text invented and **sweeping** the ones it abandoned. |
| `PageTagReader` | Reads a page's tags for the handlers that did not touch the body (open, move, favorite, archive). |
| `TagFilter` | Resolves "which pages carry all of these tags?" — the one rule shared by the sidebar, the search and the graph. |

## 3. Key design decisions

| # | Decision | Rationale (rejected alternative) |
|---|---|---|
| **D1** | Links and tags are **written in the markdown**, parsed by the backend on save. | Keeps the body portable and the frontend simple. Rejected authoring them through forms/junction CRUD, which would strand the metadata outside the exported file. |
| **D2** | Derived rows are reconciled by **diff**, not wiped and re-inserted. | Same idempotent result, and it never trips the unique index by deleting and re-inserting the same row inside one transaction. |
| **D3** | The tree and the graph are separate systems over one `Page`. | The tree is a filing decision (acyclic), the graph is meaning (cyclic). Rejected modelling links as tree edges. |
| **D4** | The slug is frozen at creation. | A rename must not break `[[links]]` or URLs. Rejected re-slugging on rename. |
| **D5** | Delete is a **soft delete of the whole subtree**, done in the command rather than by a DB cascade. | No child is left pointing at a deleted parent, and the rows survive for history. Rejected `ON DELETE CASCADE` (a hard delete of history). |
| **D6** | `search_vector` is a **generated STORED column**, not maintained by application code. | Postgres keeps it in sync, so no save path can forget it. Configuration `simple` (lower-case only, no stemming) because the notebook mixes PT-BR and EN. |
| **D7** | The blob store sits behind `IFileStorage`, with the backend and key recorded **on each attachment**. | Adding S3 later needs no migration and no rewrite of old rows — reads are self-describing. |
| **D8** | The local graph is cut **in memory**, not with a recursive SQL walk. | A personal notebook is small; loading the user's pages and edges and running BFS keeps depth logic out of SQL and unit-testable. |
| **D9** | A tag row survives losing its last page **only if it has a color**. | Color is the one thing the text cannot recover; without it, an unused tag is just noise in the filters. |
| **D10** | Multi-tag filters **intersect** (AND), identically on all three surfaces. | "Keep narrowing" is the expected behaviour, and one rule everywhere beats OR here and AND there. |
| **D11** | Attachments are served by an **authenticated endpoint**, never by a direct path. | The bytes are the user's. The consequence is that the browser cannot fetch them on its own — see [Attachments](attachments.md#4-the-consequence-the-browser-cannot-fetch-an-attachment-by-itself). |

## 4. Cross-cutting rules

- **Multi-tenant by user.** `nte001_page` and `nte005_tag` carry `user_id NOT NULL`; the derived
  tables reach the owner through their page. Every endpoint is authenticated and scoped to the
  token's user; another user's resource returns **404** (not 403).
- **`TimeProvider` everywhere.** No aggregate reads `DateTime.Now` directly, which is what makes the
  archive/delete timestamps testable.
- **No audit event log.** Unlike Finances, the module has no `audit_event` table. `Page` and `Tag`
  are `IAuditable` (`created_by/at`, `updated_by/at`); the derived edge tables carry only
  `created_at`, because they are rewritten rather than edited.
- **Frontend mirrors of backend parsers.** `lib/wikilinks.ts` and `lib/tags.ts` reimplement the
  backend parsers so the preview resolves a reference exactly the way the next save will. They are
  deliberate duplication, each covered by its own unit tests.
