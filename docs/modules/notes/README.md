# Notes Module

> A personal Notion/Obsidian inside the Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Notes** module is a knowledge base for a single user: hierarchical **pages** in a sidebar
tree, a **media library** of images and files, page links with backlinks, tags, full-text search, a
force-directed **graph view**, and a Notion-style block editor.

**A page is a document of blocks** (since the blocks revamp, 2026-11-07): every paragraph, heading,
list item, image or table is a block with a stable id, stored as one `jsonb` document per page. Links,
tags, media use and the search text are derived from the blocks on every save. The plan and the format
are in the [Product Plan](en/product-plan.md); the topic files below still describe the markdown era
until each step of the plan rewrites them.

---

## How this documentation is organized

Start with the **Overview** for the product picture and vocabulary, then dive into the topic you
need. Each topic file carries both the *product context* (what it means for the user and why) and
the *technical rules* (aggregates, invariants, schema, endpoints).

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | Vision, principles, ubiquitous language, scope (in/out) |
| 2 | [Architecture](en/architecture.md) | Project layout, DDD building blocks, ports, key design decisions |
| 3 | [Data Model](en/data-model.md) | Full schema catalog (`nte001`–`nte011`): columns, constraints, indexes |
| 4 | [Pages & Hierarchy](en/pages-and-hierarchy.md) | `Page` aggregate, tree, slug, move/reorder, favorite, archive, soft delete |
| 4a | [Spaces](en/spaces.md) | Grouping pages like Confluence spaces, the default space, moving across spaces |
| 5 | [Editor & Rich Blocks](en/editor.md) | CodeMirror 6, autosave, preview, slash commands, callouts, tables, autocomplete |
| 6 | [Media Library](en/media-library.md) | Items and folders, thumbnails, storage, authenticated serving, what is ready for video |
| 7 | [Wikilinks & Backlinks](en/wikilinks-and-backlinks.md) | `[[target]]` parsing, `PageLink` edges, linked mentions, create-on-click |
| 8 | [Tags](en/tags.md) | `#tag` parsing, normalization, derived edges, orphan sweep, colors, filters |
| 9 | [Search](en/search.md) | `tsvector` generated column, `tsquery` translation, excerpt, command palette |
| 10 | [Graph View](en/graph-view.md) | Global and local graph, neighborhood walk, degree, rendering |
| 10a | [Version history](en/version-history.md) | Complete snapshots, comparison and reversible restoration |
| 11 | [API Reference](en/api-reference.md) | Every endpoint under `/api/v{n}/notes` |
| 12 | [Implementation Status](en/implementation-status.md) | What is built vs. planned |
| 13 | [Product Plan — blocks revamp](en/product-plan.md) | The block format, derived data, decisions and the steps B1–B11 |

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Notes.*` (.NET 10, DDD, CQRS-style commands/queries).
- **Schema:** PostgreSQL schema `notes`, tables prefixed `nteXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/notes` (React + TanStack Query + TipTap).
- **API base:** `/api/v{version}/notes`, authenticated, scoped to the token's user.
- **Migrations:** `migrations/migrations/notes/`.
