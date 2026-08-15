# Overview — Product & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. What the module does

The **Notes** module is a personal knowledge base living inside the Pandora modular monolith. It
lets a single user:

- Write **pages** in markdown, organized as a **tree** (parent/child) shown in a sidebar.
- Reorder and reparent pages by drag-and-drop; **favorite**, **archive**, and **delete** (soft).
- Give a page an **emoji icon**, edited next to its title.
- **Upload attachments** — paste an image, drop a zip or a PDF — served back by an authenticated
  endpoint and embedded inline in the markdown.
- Connect pages with `[[wikilinks]]` and `![[embeds]]`, and read the **backlinks** ("linked
  mentions") of the page being viewed.
- Label pages with `#tags` written **in the text**, then filter the sidebar, the search and the
  graph by them (paint them with a color while at it).
- Find anything by title or body with **full-text search**, reachable by `Ctrl+K` or a button.
- See the whole network as a **graph**, or just the neighborhood of the open page.
- Edit with slash commands, callouts and assisted markdown **tables**, plus autocomplete for `[[`,
  `#` and `/`.

## 2. Core principles

1. **Markdown is the source of truth.** Wikilinks and tags are typed into the body, not picked from
   a form. An exported `.md` keeps its links and labels; a `.md` written elsewhere and pasted in
   works the same. *(Design decision D1.)*
2. **Derived data is rebuilt, never edited.** `PageLink` edges and `PageTag` rows are recomputed
   from the body on every save, reconciled by diff — saving the same text twice changes nothing.
   *(D2.)*
3. **Hierarchy ≠ graph.** The tree (`parent_id`) is the filing system and forbids cycles; the graph
   (`PageLink`) is the network of meaning and expects them. Two independent systems over one `Page`.
   *(D3.)*
4. **Nothing is really deleted.** Deleting is a soft delete of the page and its whole subtree; a
   soft-deleted page keeps its row and its inbound edges, which reads filter out.
5. **The database only stores what text cannot.** A tag's color, the search vector, the materialized
   edges — everything else the markdown already says.

## 3. Ubiquitous language (glossary)

| Term | Meaning |
|---|---|
| **Page** | A markdown document owned by the user, and the module's aggregate root. Has a title, an optional emoji icon, a body, a parent, and a position among its siblings. |
| **Tree / hierarchy** | The parent/child structure shown in the sidebar, held by `parent_id` alone. Cycles are rejected on reparent. |
| **Slug** | A link-friendly derivation of the title, unique per user among live pages. Fixed at creation, so a link survives a rename. |
| **Wikilink** | A `[[Target]]` reference in the body. `[[Target\|alias]]` shows the alias; `![[Target]]` is an **embed**. |
| **PageLink (edge)** | The materialized fact that one page references another, with a `kind` of `wikilink` or `embed`. Derived from the source's body. |
| **Backlink / linked mention** | The reverse read of an edge: the pages that reference the one being viewed. |
| **Broken link** | A `[[Target]]` that matches no page. It exists only in the text — no edge is created. Clicking it offers **create-on-click**. |
| **Tag** | A `#label` written in the body. Its **slug** is its identity per user (`#Café` and `#cafe` are one tag); its **name** is how it was first written; its **color** is the only thing the row adds over the text. |
| **PageTag (edge)** | The materialized fact that a page carries a tag. Derived from the page's body, exactly like an edge. |
| **Orphan sweep** | The pass at the end of a save that deletes tags no page mentions anymore — unless they carry a color. |
| **Attachment** | An uploaded file's metadata plus the `(storage_backend, storage_key)` pair locating its bytes. Write-once. |
| **File blob** | The bytes themselves, in the MVP a `bytea` row in `nte003_file_blob` — the one real `IFileStorage` backend. |
| **Local graph** | The neighborhood of the open page within N hops, edges followed in both directions. |
| **Degree** | How many edges touch a node *inside the returned graph* — what sizes the node when drawn. |
| **Callout** | An Obsidian-syntax highlight block (`> [!note] Title`). Degrades to a plain blockquote in any other markdown renderer. |

## 4. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

Page CRUD with the tree, slugs, move/reorder, favorite, archive and soft delete; attachments with an
`IFileStorage` abstraction and a database backend; the CodeMirror editor with autosave, live preview,
inline upload, slash commands, callouts and table editing; wikilinks, embeds and backlinks; tags with
colors and intersection filters on three surfaces; full-text search with a command palette; global
and local graph views.

### Out of scope / future

| Feature | Status |
|---|---|
| **Search ranking & highlighting** | Planned (v2). Results are ordered by title and the excerpt is a plain slice — no `ts_rank`, no `ts_headline`. |
| **Live block preview inside CodeMirror** | Not implemented. The editor stays raw markdown; rendering happens in the preview pane. Would be a phase of its own (widget decorations). |
| **Inline rendering of `![[embeds]]`** | Not implemented. The edge kind exists and is drawn dashed in the graph, but the preview renders an embed as an ordinary link. |
| **Filters in the URL** | Not implemented. Tag filters are component state, so there is no shareable "pages with #x" link. |
| **Hiding archived pages in the graph** | Not implemented. The archived toggle only narrows the sidebar; the graph always includes archived pages (flagged). |
| **Collapsible callouts** (`> [!note]-`) and **column alignment from the UI** | Deliberately out. |
| **Global tag rename** | Deliberately out — it would mean rewriting the markdown of every page that mentions it. |
| **Nested-tag rollup** (`#projeto` including `#projeto/pandora`) | Out. The slash is kept as part of the tag's text, nothing more. |
| **S3/MinIO storage backend** | Designed for, not implemented. `IFileStorage` + the self-describing `storage_backend`/`storage_key` columns mean adding it needs no migration. |
| **Version history, page templates, sharing/collaboration** | Future. |
| **Audit event log** | Not part of this module. Pages and tags carry `created_by/at` + `updated_by/at`; there is no `nte`-side event log like the Finances `fin016`. |
