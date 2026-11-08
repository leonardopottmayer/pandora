# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Pages & Hierarchy](pages-and-hierarchy.md)

PostgreSQL schema **`notes`**. Conventions across all tables: PK `uuid DEFAULT uuid_generate_v7()`,
`TIMESTAMPTZ` for timestamps, named constraints (`pk_nteXXX`, `uq_nteXXX_*`, `fk_nteXXX_*`), enums
stored as `VARCHAR`. User-owned roots carry `user_id NOT NULL` and an index on it; the derived tables
reach the owner through their page.

Audit columns (`created_by/created_at/updated_by/updated_at`) exist only on the tables that are
*edited* — pages, tags, spaces, media items and folders. The derived tables (`nte004`, `nte006`) and the
write-once ones (`nte003`, `nte009`) carry just `created_at`, because nothing ever updates a row there.

Migrations live in `migrations/migrations/notes/`.

## Telegram capture receipts — `nte015_note_capture`

Write-once capture records: `id` is the Channels inbound event id (PK, supplied rather than generated),
`user_id`, `page_id`, and UTC `captured_at`. Index `ix_nte015_user_page` supports finding the user's
newest live page previously used for capture, independently of title or location. There is deliberately
no page FK: receipts survive trash/purge and version restore, preventing event replay from resurrecting
deleted text. They contain no message body. A receipt commits with the page, derived data and version.
New migration: `20261107150000-create-table-nte015-note-capture`; no existing CREATE was changed for this slice.

## Table catalog

| # | Table | Contents |
|---|---|---|
| nte001 | `page` | Pages (tree + body + search vector) |
| nte002 | `media` | Media library items: images and files |
| nte003 | `file_blob` | The bytes (`DatabaseFileStorage` backend) |
| nte004 | `page_link` | Wiki graph edges, derived from the body |
| nte005 | `tag` | User labels, born from the body |
| nte006 | `page_tag` | Page ↔ tag edges, derived from the body |
| nte007 | `space` | Spaces grouping the pages |
| nte008 | `media_folder` | Virtual folders of the media library |
| nte009 | `media_rendition` | Versions derived from a media item (thumbnails) |
| nte010 | `page_media` | Page → media item, derived from the body |
| nte011 | `page_version` | Complete immutable page snapshots; see [Version history](version-history.md) |
| nte015 | `note_capture` | Event-keyed Telegram capture receipts, retained after page purge |
| nte016 | `notes_preferences` | User's daily template and folder preference |
| nte017 | `daily_note` | Stable page identity by user and calendar date |

> The numbers name the tables; the migrations run in dependency order instead (space before page,
> blob store and folders before media).

---

## nte001_page

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | owner |
| `space_id` | uuid NOT NULL → nte007 | the space the page lives in; a child always shares its parent's |
| `parent_id` | uuid NULL → nte001 | the sidebar tree; `NULL` is a root |
| `title` | varchar(255) NOT NULL | |
| `slug` | varchar(100) NOT NULL | derived from the title at creation, then frozen |
| `content_markdown` | text NOT NULL DEFAULT `''` | the source of truth |
| `icon` | varchar(50) NULL | a single emoji grapheme |
| `order_index` | int NOT NULL DEFAULT 0 | position among siblings |
| `is_favorite` | boolean NOT NULL DEFAULT false | |
| `is_template` | boolean NOT NULL DEFAULT false | available as a template; page setting, excluded from version snapshots |
| `archived_at` | timestamptz NULL | archived = hidden from the default tree, still editable |
| `deleted_at` | timestamptz NULL | soft delete |
| `history_number` | bigint NOT NULL | Latest snapshot number; optimistic concurrency token |
| `search_vector` | tsvector GENERATED ALWAYS … STORED | `to_tsvector('simple', title || ' ' || content_markdown)` |
| audit | `created_by/at`, `updated_by/at` | |

Constraints and indexes:

- `pk_nte001`, `fk_nte001_parent (parent_id → nte001.id)` — no cascade on purpose: the delete
  command soft-deletes the whole subtree itself (see [D5](architecture.md#3-key-design-decisions)).
- `uq_nte001_user_slug (user_id, slug) WHERE deleted_at IS NULL` — a **partial** unique index, so a
  soft-deleted page frees its slug for reuse.
- `ix_nte001_user_id`, `ix_nte001_parent_id`, `ix_nte001_space_id`.
- `fk_nte001_space (space_id → nte007.id)`.
- `ix_nte001_search_vector` — **GIN** over the generated column.

The vector uses the `simple` configuration (lower-casing only, no stemming, no language guess),
because the notebook mixes Portuguese and English and picking one language would degrade the other.
See [Search](search.md).

## nte002_media

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | also the id in the URL a note embeds |
| `user_id` | uuid NOT NULL | the uploader; anyone else gets 404 |
| `folder_id` | uuid NULL → nte008 | `NULL` is "Unsorted" |
| `kind` | varchar(20) NOT NULL | `image` \| `file` \| `video` (`chk_nte002_kind`); video is not accepted yet |
| `name` | varchar(255) NOT NULL | what the library shows; renameable |
| `original_file_name` | varchar(255) NOT NULL | the download's file name |
| `content_type` | varchar(255) NOT NULL | MIME as sent; empty falls back to `application/octet-stream` |
| `size_bytes` | bigint NOT NULL | |
| `width`, `height` | int NULL | pixels, upright; NULL for a file or an image the server cannot read |
| `storage_backend` | varchar(50) NOT NULL | which `IFileStorage` holds the bytes — `Database` today |
| `storage_key` | varchar(1024) NOT NULL | opaque key within that backend — today an `nte003` row id |
| `deleted_at` | timestamptz NULL | soft delete; the bytes keep being served |
| audit | `created_by/at`, `updated_by/at` | |

Indexes `ix_nte002_user_id`, `ix_nte002_folder_id`; `fk_nte002_folder`. The `storage_backend` +
`storage_key` pair is what lets a bucket backend land without a migration. See [Media Library](media-library.md).

## nte003_file_blob

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | the value stored as a media item's or rendition's `storage_key` |
| `file_name` | varchar(255) NOT NULL | |
| `content_type` | varchar(255) NOT NULL | |
| `size_bytes` | bigint NOT NULL | |
| `content` | bytea NOT NULL | the bytes |
| `created_at` | timestamptz NOT NULL | |

The generic blob store behind `DatabaseFileStorage`. It knows nothing about pages, media or users —
it is addressed only through the rows that point at it.

## nte008_media_folder

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `parent_id` | uuid NULL → nte008 | the tree; `NULL` is the top |
| `name` | varchar(100) NOT NULL | |
| audit | `created_by/at`, `updated_by/at` | |

`fk_nte008_parent`, `ix_nte008_user_id`. Deleting a folder moves what it holds up a level first, so
the row is removed outright.

## nte009_media_rendition

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `media_id` | uuid NOT NULL → nte002 | |
| `purpose` | varchar(20) NOT NULL | `thumbnail` (`chk_nte009_purpose`); a video poster would be the next |
| `width`, `height` | int NOT NULL | |
| `content_type` | varchar(255) NOT NULL | `image/webp` for thumbnails |
| `size_bytes` | bigint NOT NULL | |
| `storage_backend`, `storage_key` | as in nte002 | its own bytes in the blob store |
| `created_at` | timestamptz NOT NULL | written once at upload |

`uq_nte009_media_purpose (media_id, purpose)` — one of each per item.

## nte004_page_link

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `source_page_id` | uuid NOT NULL → nte001 | the page whose body contains the reference |
| `target_page_id` | uuid NOT NULL → nte001 | the page it resolves to |
| `kind` | varchar(20) NOT NULL | `wikilink` \| `embed` |
| `created_at` | timestamptz NOT NULL | edges are created and removed, never edited |

- `uq_nte004_edge (source_page_id, target_page_id, kind)` — a target linked five times in one page is
  one fact. A page that both links **and** embeds another produces two rows.
- `ix_nte004_target_page_id` — "who points at me?" is the hot read (backlinks).
- Both FKs point at real pages, and the rows survive a **soft** delete of either side: an edge whose
  target is soft-deleted is a *broken* edge, filtered out on read rather than deleted. Edges leaving
  a deleted page are removed outright.

## nte005_tag

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `slug` | varchar(50) NOT NULL | the identity — `#Café` and `#cafe` share `cafe` |
| `name` | varchar(50) NOT NULL | how it was first written; display only |
| `color` | varchar(20) NULL | hex (`#rgb`/`#rrggbb`/`#rrggbbaa`), validated in the handler |
| audit | `created_by/at`, `updated_by/at` | `updated_*` exists because the color is editable |

`uq_nte005_user_slug (user_id, slug)`. There is no unique index on `name`: two spellings collapse
into one row, and the first spelling wins.

The row is never created by a CRUD screen — it appears because some page's markdown mentioned it, and
it is deleted when the last page stops mentioning it, **unless it carries a color**. See
[Tags](tags.md).

## nte006_page_tag

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `page_id` | uuid NOT NULL → nte001 | |
| `tag_id` | uuid NOT NULL → nte005 | |
| `created_at` | timestamptz NOT NULL | derived, never edited |

- `uq_nte006_page_tag (page_id, tag_id)` — a tag written five times in one page is one fact.
- `ix_nte006_tag_id` — "which pages carry this tag?" is the filter's read.
- `fk_nte006_tag` is **declared to EF as a relationship** even though there is no navigation
  property. Without it, the orphan sweep — which deletes a tag and its edges in the same transaction
  — emitted the tag's `DELETE` first and hit the constraint. An integration test caught it.

## nte007_space

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | owner |
| `name` | varchar(100) NOT NULL | |
| `icon` | varchar(50) NULL | one emoji grapheme |
| `color` | varchar(20) NULL | hex `#rrggbb`, validated in the handler |
| `description` | varchar(500) NULL | |
| `is_default` | boolean NOT NULL DEFAULT false | where a page with no space named goes |
| `archived_at` | timestamptz NULL | hidden from the list; its pages stay reachable |
| `deleted_at` | timestamptz NULL | soft delete, only of an empty space |
| audit | `created_by/at`, `updated_by/at` | |

- `uq_nte007_user_default (user_id) WHERE is_default AND deleted_at IS NULL` — one default per user.
- `ix_nte007_user_id`. See [Spaces](spaces.md).

## nte010_page_media

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `page_id` | uuid NOT NULL → nte001 | |
| `media_id` | uuid NOT NULL → nte002 | |
| `created_at` | timestamptz NOT NULL | derived, never edited |

- `uq_nte010_page_media (page_id, media_id)` — an item shown five times on a page is one fact.
- `ix_nte010_media_id` — "which pages use this item?" is the library's read.
- Rewritten from the markdown on every save; a deleted page drops its rows. See [Media Library](media-library.md#10-which-notes-use-each-item).

---

## Relationship map

```
nte007_space ──< nte001_page (space_id)                every page in exactly one space
nte001_page ──parent_id──┐ (self, tree, no cascade)
     │                   └──> nte001_page
     │
     ├──< nte004_page_link (source_page_id, target_page_id)   the wiki graph, cycles allowed
     ├──< nte006_page_tag (page_id) >── nte005_tag (user_id)  labels written in the body
     └──< nte010_page_media (page_id) >── nte002_media     media the body shows

nte008_media_folder ──< nte002_media (folder_id) ──< nte009_media_rendition (media_id)
                         nte002 / nte009 ──storage_key──> nte003_file_blob
```

## Daily notes (B10)

`nte016_notes_preferences` is keyed by `user_id` (uuid). Nullable `daily_template_id` and
`daily_folder_id` retain the selected model and daily folder identity. Standard audit columns:
`created_by`, `created_at`, `updated_by`, `updated_at`. No row means defaults. No FKs: optional
references are owner-checked on use, and unavailable templates/folders fall back safely after purge.

`nte017_daily_note` has UUID PK `id`, owner `user_id`, calendar `date` (PostgreSQL date / DateOnly)
and current `page_id`, all required. Constraint `uq_nte017_user_date` enforces one entry per
owner/day. No page FK: entries survive page purge and replace their page reference on the next
visit. Restoring an old page leaves the current registry unchanged. This identity is not part of
page versions. It remains stable after rename/move/archive.

New CREATE/down migrations: `20261107160000-create-table-nte016-notes-preferences` and
`20261107160001-create-table-nte017-daily-note`. Local database not migrated or recreated.

## Folder properties projection

The folder table is derived on read from existing properties blocks (`props.rows[{key,value}]`,
rich-text inline arrays). `PageContent.Properties()` extracts named rows, including nested blocks.
The API groups column names case-insensitively and preserves repeated values with newlines. No
persisted column/table, content rewrite or version is added.

## Live Agenda configuration

agenda-list uses the existing content JSON only: mode today/tasks and optional listId. No list name,
task title, status, date or fetched result is persisted by the block. These are resolved by the
authenticated frontend. Restoring versions/templates keeps the configuration, not a historical
Agenda snapshot; unavailable lists are reported when viewed.

## Live Finance configuration

finance-summary uses existing content JSON: mode account/card/expenses and accountId/cardId or
optional month (YYYY-MM). Only configuration survives versions/templates; restored content
resolves current figures. No financial names, amounts, currency totals or fetched rows are
persisted. No schema/migration change.

## Upcoming Agenda configuration

agenda-list also accepts { mode: "events", days: 7 | 30, calendarId?: string }. Defaults: 7 days,
all active calendars. Only filters are stored; UTC date bounds, names and occurrences are resolved
when rendered. Configuration survives versions/templates without preserving a financial or
calendar snapshot. No schema/migration change.
