# Notes Module

> A personal Notion/Confluence inside the Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Notes** module is a knowledge base for one user. Pages live in **spaces** and **folders**, can
have sub-pages, and are **documents of blocks**: every paragraph, heading, list item, image, table,
callout or embed is a block with a stable id, stored as one `jsonb` document per page. Links, tags,
media use and the search text are derived from the blocks on every save.

Around the pages: a media library, page links with backlinks, tags, full-text search, a graph view,
version history, comments, templates, daily notes, public links, and AI actions through the
[Assistant](../assistant/README.md).

---

## How this documentation is organized

Start with the **Overview** for the product picture and vocabulary, then the topic you need.

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | What the module does, principles, glossary, scope |
| 2 | [Architecture](en/architecture.md) | Projects, aggregates, `PageContent`, derived data, decisions |
| 3 | [Data Model](en/data-model.md) | Schema catalog `nte001`–`nte017`: columns, constraints, indexes |
| 4 | [Blocks & Editor](en/blocks.md) | The block format, every block and inline kind, the editor |
| 5 | [Pages & Hierarchy](en/pages-and-hierarchy.md) | Pages, folders, the tree, trash, templates, daily notes, page settings, export |
| 6 | [Spaces](en/spaces.md) | Grouping pages like Confluence spaces, the default space |
| 7 | [Links & Backlinks](en/links-and-backlinks.md) | Page mentions, page-link blocks, synced copies, "Mentioned in" |
| 8 | [Tags](en/tags.md) | The tags block, normalization, colors, filters |
| 9 | [Search](en/search.md) | `tsvector` over the derived text, the palette, highlighting |
| 10 | [Media Library](en/media-library.md) | Items and folders, thumbnails, storage, serving, which pages use what |
| 11 | [Graph View](en/graph-view.md) | Global and local graph, families, inspection |
| 12 | [Version History](en/version-history.md) | Snapshots, comparison, restoration |
| 13 | [Comments](en/comments.md) | Threads on text or blocks, block dates |
| 14 | [AI](en/ai.md) | Text actions, page summary, ask your notes, AI block, Telegram capture, Assistant tools |
| 15 | [API Reference](en/api-reference.md) | Every endpoint under `/api/v{n}/notes` |
| 16 | [Implementation Status](en/implementation-status.md) | What is built, open points |
| 17 | [Product Plan](en/product-plan.md) | What is left |

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Notes.*` (.NET 10, DDD, commands/queries over the Tars mediator).
- **Schema:** PostgreSQL schema `notes`, tables `nte001`–`nte017`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/notes` (React, TanStack Query, TipTap 3).
- **API base:** `/api/v{version}/notes`, authenticated, scoped to the token's user; public pages under
  `/api/v{version}/public/notes/pages/{token}`.
- **Migrations:** `migrations/migrations/notes/`.
