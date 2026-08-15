# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is designed but not yet implemented. Use it
to tell the difference between "documented because it exists" and "documented as a plan".

The module was built in nine sequential phases (scaffold → pages → attachments → editor → wikilinks →
search → graph → rich blocks → tags), all of which are **closed**. What follows is the state that
left behind, not the phase log.

---

## Implemented

| Area | Notes |
|---|---|
| **Module scaffold** | All layered projects, `notes` schema, registered in the Host. No jobs, no integration events, no audit event log. |
| **Pages** | `Page` aggregate, tree with cycle rejection on reparent, frozen slugs with collision suffixes, move/reorder, favorite, archive, soft delete of the whole subtree (`nte001`). |
| **Emoji icon** | Picker next to the title, reduced to the first grapheme via `Intl.Segmenter`, saved through the page's own autosave. |
| **Attachments** | `IFileStorage` + `DatabaseFileStorage`, blob table, authenticated upload/download, inline paste/drop in the editor, object-URL resolution in the preview (`nte002`, `nte003`). |
| **Editor** | CodeMirror 6, raw markdown + preview + split mode, 800 ms autosave, DOMPurify sanitization. |
| **Rich blocks** | Slash commands (11 blocks + 6 callouts), Obsidian callouts as a `marked` extension, assisted markdown tables with Tab/Shift+Tab and idempotent reformatting. |
| **Autocomplete** | Three menus — `[[` (pages by title and slug), `#` (existing tags), `/` (commands) — sharing `filter: false`, ref-based inputs and a body-parented tooltip. |
| **Wikilinks & backlinks** | `WikilinkParser`, title-then-slug resolution, `wikilink`/`embed` kinds, diff-based edge rebuild on create and update, backlinks endpoint and panel, create-on-click (`nte004`). |
| **Search** | Generated `tsvector` column + GIN index, `simple` configuration, `tsquery` translation with discarded punctuation, 160-char excerpt, command palette on Ctrl+K **and** a sidebar button. |
| **Graph** | Global and local graph from one query, in-memory undirected BFS with depth clamped 1..5, backend-computed degree, broken edges filtered out, `react-force-graph-2d` canvas with hover highlighting and zoom-gated labels. |
| **Tags** | `TagParser` with code stripping, `TagName` normalization, tag creation from the text, diff-based edges, orphan sweep preserving colored tags, hex color validation, intersecting filters on the sidebar / search / graph, chips in the preview (`nte005`, `nte006`). |
| **Frontend** | React module (`client-web/src/modules/notes`) covering all of the above, i18n EN + PT-BR, unit tests over every `lib/` module. |
| **Tests** | Domain unit tests (`Modules.Notes.Tests`: page, hierarchy, graph, search, slugger, tag name, tag parser, wikilink parser) + integration tests (`IntegrationTests/Modules/Notes`: pages, attachments, backlinks, search, graph, tags). |

## Not yet implemented (designed / planned)

| Area | Status |
|---|---|
| **Search ranking & highlighting** | Planned as v2 in the product plan. Results are ordered by title, not by `ts_rank`, and `PageSearchResultDto.Excerpt` is a plain slice — no `ts_headline`. |
| **Live block preview inside CodeMirror** | Not implemented. The editor stays raw markdown; the result is seen in the preview pane. Widget decorations inside the editor would be a phase of their own. |
| **Inline rendering of `![[embeds]]`** | Not implemented. The `embed` edge kind exists end to end (stored, drawn dashed in the graph, listed in backlinks), but the preview renders an embed as an ordinary link. |
| **Tag filters in the URL** | Not implemented. Filters are component state, so "the pages with #x" is not a shareable link. The place would be a search param on `NotesPage`. |
| **Archived toggle on the graph** | Not implemented. `NotesGraphPage`'s `includeArchived` only narrows the sidebar; the graph always includes archived pages (flagged). |
| **Collapsible callouts** (`> [!note]-`) | Deliberately out of the rich-blocks scope. If it lands, the place is the `-`/`+` variant in `lib/callouts.ts`. |
| **Column alignment from the UI** | Deliberately out. The place would be the separator line in `lib/markdownTables.ts`. |
| **Visual table editor** (WYSIWYG widget) | Rejected: it would swap the markdown for a widget rendered as editable text, which is the opposite of the module's premise. |
| **S3/MinIO storage backend** | Only the abstraction exists. `storage_backend` + `storage_key` on each attachment mean plugging it in needs no migration; the natural trigger is large uploads. |
| **Version history, page templates, sharing/collaboration** | Future in the product plan; nothing started. |

## Deliberately out (not debt)

- **Global tag rename** — it would mean rewriting the markdown of every page mentioning the tag; that
  is find & replace, not a database operation.
- **Nested-tag rollup** — `#projeto/pandora` is accepted as the tag's text, but `#projeto` does not
  include its children.
- **Tag create/delete endpoints** — the text creates a tag, the sweep removes it.
- **`ON DELETE CASCADE` on the page tree** — the delete command walks the subtree itself so the soft
  delete keeps its history.

## Known open points

1. **Blob retention.** Attachment bytes live in `bytea`; revisit moving to object storage if the
   notebook starts carrying heavy files — that is also the trigger for the S3 backend.
2. **Orphaned attachments.** Nothing deletes the blob when a page that embedded it is deleted, and
   `page_id` has no FK by design. A sweep would need a decision on what "unreferenced" means when the
   only reference is a markdown string.
3. **In-memory graph walk.** Loading the user's whole page/edge set is right for a personal notebook;
   it is the first thing to revisit if the graph ever gets big.
4. **Duplicated parsers.** `lib/wikilinks.ts` and `lib/tags.ts` mirror the backend on purpose, so the
   preview resolves what the save will resolve. Any change to a parsing rule has to land on both
   sides — the unit tests on each side are the guard.
