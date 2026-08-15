# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Pages & Hierarchy](pages-and-hierarchy.md)

PostgreSQL schema **`notes`**. Conventions across all tables: PK `uuid DEFAULT uuid_generate_v7()`,
`TIMESTAMPTZ` for timestamps, named constraints (`pk_nteXXX`, `uq_nteXXX_*`, `fk_nteXXX_*`), enums
stored as `VARCHAR`. User-owned roots carry `user_id NOT NULL` and an index on it; the derived tables
reach the owner through their page.

Audit columns (`created_by/created_at/updated_by/updated_at`) exist only on the tables that are
*edited* — `nte001_page` and `nte005_tag`. The derived tables (`nte004`, `nte006`) and the write-once
ones (`nte002`, `nte003`) carry just `created_at`, because nothing ever updates a row there.

Migrations live in `migrations/migrations/notes/`.

## Table catalog

| # | Table | Contents |
|---|---|---|
| nte001 | `page` | Pages (tree + body + search vector) |
| nte002 | `attachment` | Uploaded file metadata |
| nte003 | `file_blob` | The bytes (`DatabaseFileStorage` backend) |
| nte004 | `page_link` | Wiki graph edges, derived from the body |
| nte005 | `tag` | User labels, born from the body |
| nte006 | `page_tag` | Page ↔ tag edges, derived from the body |

> The numbering is creation order, not the order the tables reference each other: `nte003` was
> created before `nte002` because the blob store had to exist before the metadata pointing at it.

---

## nte001_page

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | owner |
| `parent_id` | uuid NULL → nte001 | the sidebar tree; `NULL` is a root |
| `title` | varchar(255) NOT NULL | |
| `slug` | varchar(100) NOT NULL | derived from the title at creation, then frozen |
| `content_markdown` | text NOT NULL DEFAULT `''` | the source of truth |
| `icon` | varchar(50) NULL | a single emoji grapheme |
| `order_index` | int NOT NULL DEFAULT 0 | position among siblings |
| `is_favorite` | boolean NOT NULL DEFAULT false | |
| `archived_at` | timestamptz NULL | archived = hidden from the default tree, still editable |
| `deleted_at` | timestamptz NULL | soft delete |
| `search_vector` | tsvector GENERATED ALWAYS … STORED | `to_tsvector('simple', title || ' ' || content_markdown)` |
| audit | `created_by/at`, `updated_by/at` | |

Constraints and indexes:

- `pk_nte001`, `fk_nte001_parent (parent_id → nte001.id)` — no cascade on purpose: the delete
  command soft-deletes the whole subtree itself (see [D5](architecture.md#3-key-design-decisions)).
- `uq_nte001_user_slug (user_id, slug) WHERE deleted_at IS NULL` — a **partial** unique index, so a
  soft-deleted page frees its slug for reuse.
- `ix_nte001_user_id`, `ix_nte001_parent_id`.
- `ix_nte001_search_vector` — **GIN** over the generated column.

The vector uses the `simple` configuration (lower-casing only, no stemming, no language guess),
because the notebook mixes Portuguese and English and picking one language would degrade the other.
See [Search](search.md).

## nte002_attachment

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | also the id in the download URL |
| `page_id` | uuid NULL | **loose reference, no FK** — a page may be soft-deleted while its attachment lingers |
| `file_name` | varchar(255) NOT NULL | original name, used as the download filename |
| `content_type` | varchar(255) NOT NULL | MIME as sent; empty falls back to `application/octet-stream` |
| `size_bytes` | bigint NOT NULL | |
| `storage_backend` | varchar(50) NOT NULL | which `IFileStorage` holds the bytes — always `Database` today |
| `storage_key` | varchar(1024) NOT NULL | opaque key within that backend — today the `nte003` row id |
| `created_at` | timestamptz NOT NULL | write-once, so no `updated_*` |

Index `ix_nte002_page_id`. The `storage_backend` + `storage_key` pair is what lets a future S3
backend land without a migration: reads stay self-describing and old rows keep working.

## nte003_file_blob

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | the value stored as an attachment's `storage_key` |
| `file_name` | varchar(255) NOT NULL | |
| `content_type` | varchar(255) NOT NULL | |
| `size_bytes` | bigint NOT NULL | |
| `content` | bytea NOT NULL | the bytes |
| `created_at` | timestamptz NOT NULL | |

The generic blob store behind `DatabaseFileStorage`. It knows nothing about pages or users — it is
addressed only through an attachment row.

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

---

## Relationship map

```
nte001_page ──parent_id──┐ (self, tree, no cascade)
     │                   └──> nte001_page
     │
     ├──< nte004_page_link (source_page_id, target_page_id)   the wiki graph, cycles allowed
     ├──< nte006_page_tag (page_id) >── nte005_tag (user_id)  labels written in the body
     └──… nte002_attachment (page_id, loose, no FK) ──storage_key──> nte003_file_blob
```
