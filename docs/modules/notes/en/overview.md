# Overview — Product & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Blocks & Editor](blocks.md)

---

## 1. What the module does

The **Notes** module is a personal knowledge base living inside the Pandora modular monolith. It
lets a single user:

- Organize pages in **spaces** and **folders**, with **sub-pages** under pages, in a sidebar tree with
  drag and drop; open a space's **overview**, a folder's page, **recent pages** and **today's daily
  note** from shortcuts at the top of the sidebar.
- Write in a **block editor** like Notion's: headings, lists, to-dos, toggles, columns, callouts,
  tables, code, quotes, images, galleries, video, audio, PDFs, bookmarks, embeds, Mermaid, math,
  synced blocks, template buttons, decisions, page properties, and live blocks from Agenda and
  Finances. `/` opens the block menu, `@` mentions pages, dates and items of other modules.
- Link pages by **mentioning** them or with a **page-link block**, and read who mentions the open page
  ("Mentioned in").
- Label pages with a **tags block** at the top, then filter the sidebar, search and graph by them.
- Keep a **media library** of images and files, organized in folders, with thumbnails.
- Find anything with **full-text search** (`Ctrl+K`), or **ask a question** and get an answer with its
  sources.
- See the network as a **graph**, global or around the open page.
- Go back in time with **version history**, comment on text or blocks, and see when each block was
  written.
- Dress a page with a **cover**, width, font, lock and verification; share it with a **public link**;
  export it as Markdown, HTML or PDF, and import Markdown.
- Use **templates** and **daily notes**, and run **AI** actions (summarize, improve, translate,
  continue, AI block) through the Assistant.

## 2. Core principles

1. **A page is a document of blocks.** One `jsonb` document per page, every block with a stable id.
   Stable ids are what comments, links to a block, synced blocks and block dates hang on.
2. **Derived data is rebuilt, never edited.** Links, tags, media use, search text and block stamps
   are recomputed from the document on every save, reconciled by diff: saving the same document twice
   changes nothing.
3. **The backend checks the shape, not each block type.** Size, depth, block count, unique ids and
   the type pattern are validated; what a type's props mean is the frontend's. A new block type needs
   no backend change unless it adds derived data.
4. **Links point at ids.** A mention stores the page id, so a rename never breaks a link and nothing
   resolves titles.
5. **Hierarchy ≠ graph.** The tree (folders and parent pages) is the filing system and forbids cycles;
   the links are the network of meaning and expect them.
6. **Deleting goes to the trash.** A deleted page and its sub-pages keep their rows until restored or
   deleted for good.

## 3. Ubiquitous language (glossary)

| Term | Meaning |
|---|---|
| **Space** | A group of pages and folders, like a Confluence space. One is the default. |
| **Folder** | A container of folders and pages inside one space. Never inside a page. |
| **Page** | A document of blocks owned by the user, the module's main aggregate. Has a title, an icon, a slug, a place in the tree, and settings (cover, lock, verification, public link). |
| **Sub-page** | A page whose `parent_id` is another page. It lives in its parent's space. |
| **Block** | One unit of the document: `id`, `type`, optional `props`, inline `content` and child blocks. |
| **Inline** | A piece of text inside a block: text with marks, a page mention, or a chip (date, status, item of another module, inline math). |
| **Mention** | An inline reference to a page by id. Creates a `wikilink` edge. |
| **Page-link block** | A block showing another page as a card or a preview. Creates an `embed` edge. |
| **Synced block** | A block whose copies (`synced-ref`) show it read-only in other pages. |
| **Backlink / "Mentioned in"** | The reverse read of an edge: the pages that reference the open one. |
| **Tag** | A label from the page's tags block. Its slug is its identity per user; its color is the only thing the tag row adds. |
| **Media item** | An image or file in the library, plus the `(storage_backend, storage_key)` pair locating its bytes. |
| **Version** | A complete snapshot of a page after a save that changed it. |
| **Block stamp** | When a block was written and last edited, and by what (edit, Assistant, restore). |
| **Template** | A page marked `is_template`; new pages copy it with fresh block ids. |
| **Daily note** | The page of one calendar date, created on first open from the daily template. |

## 4. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

Spaces, folders, pages and sub-pages with drag and drop; the block editor with every block listed in
[Blocks & Editor](blocks.md); mentions, page links, synced blocks and backlinks; tags with colors and
filters; full-text search and ask your notes; the media library; global and local graphs; version
history; comments and block stamps; covers, page width and font, lock, verification, recent pages;
trash; public links; Markdown, HTML and PDF export and Markdown import; templates and daily notes; AI
actions; Telegram capture; save an email to Notes; live Agenda and Finances blocks; the Assistant
tools.

### Out of scope / future

| Feature | Status |
|---|---|
| **Databases and Projects blocks, Radar item to page** | Wait for those modules; see [Product Plan](product-plan.md). |
| **Simultaneous editing** | Not planned. Stable block ids leave room for it. |
| **Tag filters in the URL** | Not implemented: filters are component state. |
| **Global tag rename, nested-tag rollup** | Out. |
| **Bucket storage and video uploads** | Designed for, not scheduled. See [Media Library](media-library.md). |
| **Audit event log** | Not part of this module. Version history and block stamps cover what changed. |
