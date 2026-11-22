# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Pages & Hierarchy](pages-and-hierarchy.md)

PostgreSQL schema **`notes`**. Conventions: PK `uuid DEFAULT uuid_generate_v7()`, `timestamptz`
everywhere, table names `nteXXX_<name>`. Migrations in `migrations/migrations/notes/`; before 1.0 a new
column goes into the table's original `CREATE TABLE`.

---

## Catalog

| Table | Name | Holds |
|---|---|---|
| nte001 | `page` | The page and its block document |
| nte002 | `media` | A library item |
| nte003 | `file_blob` | Bytes of items and thumbnails (database storage backend) |
| nte004 | `page_link` | Derived edges between pages |
| nte005 | `tag` | A user's tag |
| nte006 | `page_tag` | Derived: which pages carry which tags |
| nte007 | `space` | A space |
| nte008 | `media_folder` | A library folder |
| nte009 | `media_rendition` | A thumbnail of an item |
| nte010 | `page_media` | Derived: which pages use which items |
| nte011 | `page_version` | Immutable page snapshots |
| nte012 | `folder` | A folder of pages |
| nte013 | `page_comment` | Comments and replies |
| nte014 | `block_stamp` | When each block was written and edited |
| nte015 | `note_capture` | Receipts of Telegram captures |
| nte016 | `notes_preferences` | The user's Notes preferences |
| nte017 | `daily_note` | The page of each date |

---

## nte001_page

| Column | Type | Notes |
|---|---|---|
| `id`, `user_id` | uuid | |
| `space_id` | uuid NOT NULL | FK `nte007` |
| `parent_id` | uuid NULL | FK `nte001`; set for a sub-page |
| `folder_id` | uuid NULL | FK `nte012`; only on a top-level page |
| `title` | varchar(255) | |
| `slug` | varchar(100) | Fixed at creation; unique per user among live pages |
| `content` | jsonb | The [block document](blocks.md); default `{"version": 1, "blocks": []}` |
| `content_text` | text | Search text derived from the blocks |
| `search_vector` | tsvector, generated | `to_tsvector('simple', title || ' ' || content_text)` |
| `icon` | varchar(50) NULL | One emoji |
| `order_index` | int | Position among siblings |
| `is_favorite`, `is_template` | boolean | |
| `archived_at`, `deleted_at` | timestamptz NULL | Deleted = in the trash |
| `appearance` | jsonb | `{ cover?, width, font, smallText }`; see [Pages §8](pages-and-hierarchy.md#8-page-settings) |
| `is_locked` | boolean | |
| `verified_at`, `verified_until` | timestamptz NULL | |
| `viewed_at` | timestamptz NULL | Last opened, for recent pages |
| `public_token` | varchar(64) NULL | Unique while set |
| `history_number` | bigint | Number of the latest version |
| `created_*`, `updated_*` | | Audit columns |

**Indexes:** `uq_nte001_user_slug (user_id, slug) WHERE deleted_at IS NULL`,
`uq_nte001_public_token WHERE public_token IS NOT NULL`, GIN on `search_vector`, plus `user_id`,
`parent_id`, `folder_id`, `space_id`.

Appearance, lock, verification, `viewed_at`, `public_token` and `is_template` are **not** part of a
version.

## nte007_space

`user_id`, `name` (100), `icon`, `color` (`#rrggbb`), `description` (500), `is_default`,
`archived_at`, `deleted_at`, audit columns. **Index:** `uq_nte007_user_default (user_id) WHERE
is_default AND deleted_at IS NULL`.

## nte012_folder

`user_id`, `space_id` (FK `nte007`), `parent_id` (FK `nte012`), `name` (255), `icon`, `description`,
`order_index`, `deleted_at`, audit columns. Indexes on `user_id` and `space_id`.

## nte004_page_link

`source_page_id`, `target_page_id` (both FK `nte001`), `kind` (`wikilink` for a mention, `embed` for a
page-link block or a synced copy), `created_at`. **Indexes:** `uq_nte004_edge (source, target, kind)`,
`ix_nte004_target_page_id` for backlinks.

## nte005_tag / nte006_page_tag

- `nte005_tag`: `user_id`, `slug` (50), `name` (50, first spelling), `color` NULL, audit columns.
  **Index:** `uq_nte005_user_slug`.
- `nte006_page_tag`: `page_id`, `tag_id`, `created_at`. **Index:** `uq_nte006_page_tag`, plus
  `tag_id`.

A tag row is created when a tags block introduces it and deleted when no page carries it, unless it
has a color. See [Tags](tags.md).

## Media: nte002, nte003, nte008, nte009, nte010

| Table | Columns |
|---|---|
| `nte002_media` | `user_id`, `folder_id` (FK `nte008`), `kind` (`image`/`file`/`video`), `name`, `original_file_name`, `content_type`, `size_bytes`, `width`, `height`, `storage_backend`, `storage_key`, `deleted_at`, audit columns |
| `nte003_file_blob` | `file_name`, `content_type`, `size_bytes`, `content` (bytea), `created_at` |
| `nte008_media_folder` | `user_id`, `parent_id` (FK self), `name`, audit columns |
| `nte009_media_rendition` | `media_id` (FK `nte002`), `purpose` (`thumbnail`), `width`, `height`, `content_type`, `size_bytes`, `storage_backend`, `storage_key`; unique `(media_id, purpose)` |
| `nte010_page_media` | `page_id`, `media_id`; unique `(page_id, media_id)`, index on `media_id` |

See [Media Library](media-library.md).

## nte011_page_version

`page_id`, `user_id`, `number` (unique per page), `created_at`, `source` (`initial`, `edit`,
`assistant`, `restore`), `restored_from` NULL, `snapshot` (jsonb with PascalCase keys: `Title`,
`Content`, `Icon`, `SpaceId`, `ParentId`, `FolderId`, `OrderIndex`, `IsFavorite`, `ArchivedAt`,
`DeletedAt`). See [Version History](version-history.md).

## nte013_page_comment

`page_id`, `user_id`, `block_id` (64), `thread_id` NULL (a reply points at its thread's first
comment), `quote` (500) NULL, `body`, `resolved_at`, `created_at`, `edited_at`. Index on `page_id`.

## nte014_block_stamp

`page_id`, `block_id` (64), `fingerprint` (32, an MD5 of the block's own JSON without its children),
`created_at`, `updated_at`, `updated_source` (`edit`, `assistant`, `restore`). Unique
`(page_id, block_id)`. See [Comments](comments.md#4-block-dates).

## nte015_note_capture

`id` (the inbound event id), `user_id`, `page_id`, `captured_at`. Index `(user_id, page_id)`. Holds no
text, and survives deleting the page for good.

## nte016_notes_preferences

`user_id` (PK), `daily_template_id` NULL, `daily_folder_id` NULL, audit columns.

## nte017_daily_note

`user_id`, `date`, `page_id`; unique `(user_id, date)`.

## Deletion

Deleting a page for good removes its links (both directions), tags, media uses, versions, comments
and block stamps. Nothing uses `ON DELETE CASCADE`: the commands do it, so the order is explicit.
