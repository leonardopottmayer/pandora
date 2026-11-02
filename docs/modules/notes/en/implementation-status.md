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
| **Spaces** | `Space` aggregate, one default per user (created on the fly when none), pages placed in a space, moving across spaces with the subtree, wikilinks preferring the own space, space filters on tree / search / graph, spaces screen and sidebar switcher, `create_note` taking a space (`nte007`). |
| **Emoji icon** | Picker next to the title, reduced to the first grapheme via `Intl.Segmenter`, saved through the page's own autosave. |
| **Media library** | Items (image / file; video in the schema, refused on upload) in virtual folders, WebP thumbnails with ImageSharp and EXIF rotation, folder moves without cycles, a deleted folder handing its contents up, soft delete keeping the bytes served, immutable caching, the library screen, paste/drop in the editor into "Unsorted" (`nte002`, `nte003`, `nte008`, `nte009`). |
| **Media inside notes** | Library picker from `/image`, `/file` and the toolbar, width, alignment and caption from the alt text (`![text|400|right]`), library `<img>` in HTML, full-screen zoom, file cards with size, which notes use each item derived on save (`nte010`), "Used in" in the library, an "Unused" place, the space taken (total and unused), a warning before deleting an item in use. |
| **Editor** | A Notion-style visual editor (TipTap 3, saving markdown: `/` `[[` `#` menus, selection bar, table bar, drag handle, native callouts/toggles with editable bodies and titles, the player under a video link, library file cards, inline HTML kept; unsupported HTML kept as raw blocks) as the default; CodeMirror 6 with raw markdown, preview and split mode beside it; 800 ms autosave, DOMPurify sanitization. |
| **Rich blocks** | Slash commands (14 blocks + 6 callouts), Obsidian callouts as a `marked` extension (collapsible with `-`/`+`), toggles (`<details>`), YouTube/Vimeo players from a bare link and an iframe allowlist (YouTube, Vimeo, Google Maps) with a fixed sandbox, assisted markdown tables with Tab/Shift+Tab and idempotent reformatting. |
| **Autocomplete** | Three menus — `[[` (pages by title and slug), `#` (existing tags), `/` (commands) — sharing `filter: false`, ref-based inputs and a body-parented tooltip. |
| **Wikilinks & backlinks** | `WikilinkParser`, title-then-slug resolution, `wikilink`/`embed` kinds, diff-based edge rebuild on create and update, backlinks endpoint and panel, create-on-click (`nte004`). |
| **Search** | Generated `tsvector` + GIN, `simple` configuration, safe prefix `tsquery`, title priority and `ts_rank_cd` relevance with stable ties, filters before the 20-result cap, 160-char plain-text excerpt with safe title/excerpt highlighting, Ctrl+K palette and sidebar button. |
| **Graph** | Global and local graph from one query, in-memory undirected BFS with depth clamped 1..5, backend-computed degree, broken edges filtered out, `react-force-graph-2d` canvas with hover highlighting and zoom-gated labels. |
| **Tags** | `TagParser` with code stripping, `TagName` normalization, tag creation from the text, diff-based edges, orphan sweep preserving colored tags, hex color validation, intersecting filters on the sidebar / search / graph, chips in the preview (`nte005`, `nte006`). |
| **Graph families** | Named/color-coded regions by chosen tags, ordered priority without duplicate pages, counts and family framing. Global graph switch off by default; mode and configuration remembered per account/browser. See [Graph View](graph-view.md#6-tag-families-and-thematic-regions). |
| **Collapsed graph families** | Per-visit collapse/expand controls replace members with a summary node and aggregate external links by direction/kind. Search reveals hidden pages automatically; the original graph is preserved. See [Collapse families](graph-view.md#10-collapse-and-expand-families). |
| **Graph camera controls** | Accessible zoom in/out and fit-all buttons in global/local graphs, including mobile; manual framing takes priority when the simulation settles. See [Camera controls](graph-view.md#11-camera-controls). |
| **Graph visibility** | Show archived now controls both the sidebar and global graph (off by default); Hide isolated removes nodes without remaining links. Search, details, family counts and framing use the same filtered projection, with recalculated degrees. Per-visit choices; no API change. See [Visibility filters](graph-view.md#5-visibility-filters). |
| **Graph inspection** | Title lookup and node selection focus the neighborhood and show a responsive details panel with tags, families and distinct connected notes. An explicit Open note action navigates to the editor. See [Locate a note](graph-view.md#7-locate-a-note) and [Inspect a note](graph-view.md#9-inspect-a-note-without-leaving-the-graph). |
| **Frontend** | React module (`client-web/src/modules/notes`) covering all of the above, i18n EN + PT-BR, unit tests over every `lib/` module. |
| **Tests** | Domain unit tests (`Modules.Notes.Tests`: page, hierarchy, graph, search, slugger, tag name, tag parser, wikilink parser) + integration tests (`IntegrationTests/Modules/Notes`: pages, attachments, backlinks, search, graph, tags). |

## Not yet implemented (designed / planned)

| Area | Status |
|---|---|
| **Tag filters in the URL** | Not implemented. Filters are component state, so "the pages with #x" is not a shareable link. The place would be a search param on `NotesPage`. |
| **Graph navigation ideas** | A bridge overview between families and later layout persistence/automatic communities. See [Graph ideas](graph-view.md#8-improvement-ideas-2026-11-02). |
| **Column alignment from the UI** | Deliberately out. The place would be the separator line in `lib/markdownTables.ts`. |
| **Bucket storage backend and video** | Designed for, not scheduled: not in the current queue. Only the abstraction exists. `storage_backend` + `storage_key` on each media row mean plugging a bucket in needs no migration; video also needs ranged downloads and a poster rendition. See [Media Library](media-library.md#3-kinds-and-what-is-ready-for-video). |
| **Version history** | Implemented — see [Version history](version-history.md). |

## Deliberately out (not debt)

- **Rendering `![[embeds]]` inline** — decided against on 2026-11-02: a page is never drawn inside
  another. `![[x]]` stays a link; the `embed` edge kind still exists (backlinks, dashed in the graph).
- **Cover image, table of contents, page templates and public links/sharing** — out for now (decided
  on 2026-11-02). Dropped from the revamp list; they come back only if the user asks.
- **Live preview inside CodeMirror** — superseded by the visual editor; CodeMirror stays as the raw
  Markdown mode.

- **Global tag rename** — it would mean rewriting the markdown of every page mentioning the tag; that
  is find & replace, not a database operation.
- **Nested-tag rollup** — `#projeto/pandora` is accepted as the tag's text, but `#projeto` does not
  include its children.
- **Tag create/delete endpoints** — the text creates a tag, the sweep removes it.
- **`ON DELETE CASCADE` on the page tree** — the delete command walks the subtree itself so the soft
  delete keeps its history.

## Version history

Implemented: complete immutable snapshots for every changed save, retained indefinitely.
Display-only grouping, arbitrary-pair Markdown and metadata comparison, preview and reversible
restore with ownership, hierarchy and concurrency validation. Existing pages receive an initial
snapshot; soft deletion retains history. See [Version history](version-history.md).

## Known open points

1. **Blob retention.** Media bytes live in `bytea`; revisit moving to object storage if the
   notebook starts carrying heavy files — that is also the trigger for the S3 backend.
2. **Unused bytes.** Deleting an item keeps its bytes, since notes may still show it; nothing sweeps the
   bytes of a deleted item that no note uses anymore. It stays that way by decision (2026-11-02): the
   library's "Unused" place already lists those items, and deleting them is the user's call.
3. **In-memory graph walk.** Loading the user's whole page/edge set is right for a personal notebook;
   it is the first thing to revisit if the graph ever gets big.
4. **Duplicated parsers.** `lib/wikilinks.ts` and `lib/tags.ts` mirror the backend on purpose, so the
   preview resolves what the save will resolve. Any change to a parsing rule has to land on both
   sides — the unit tests on each side are the guard.
