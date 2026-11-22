# Implementation Status

[← Back to index](../README.md)

What is built, what is deliberately out, and the known open points. What is left is in
[product-plan.md](product-plan.md).

---

## Implemented

| Area | Notes |
|---|---|
| **Module scaffold** | Seven layered projects, `notes` schema `nte001`–`nte017`, registered in the Host. No jobs, no integration events. |
| **Block document** | `PageContent`: shape validation, derived search text, links, tags, media, fingerprints and properties in one walk; limits 2 MB, 10,000 blocks, 24 levels. See [Blocks & Editor](blocks.md). |
| **Editor** | TipTap 3 with every block in [Blocks & Editor](blocks.md): `/`, `@` and `:` menus, block menu, multi-block selection, links to a block, table of contents, 800 ms autosave. |
| **Spaces, folders, pages** | Default space, folders, sub-pages, atomic tree moves, overview, folder page with list, cards and read-only properties table. See [Pages & Hierarchy](pages-and-hierarchy.md). |
| **Page settings** | Cover, width, font, small text, lock, verification, recent pages, public link. |
| **Trash** | Delete with sub-pages, restore to the best place left, delete for good, empty. |
| **Templates and daily notes** | `is_template`, new from template with fresh ids and `{{date}}`/`{{title}}`; daily notes in the daily folder from the daily template (`nte016`, `nte017`). |
| **Export and import** | Markdown, HTML and PDF (print) export; Markdown import. |
| **Links** | Mentions, page-link blocks, synced blocks, "Mentioned in" (`nte004`). See [Links & Backlinks](links-and-backlinks.md). |
| **Tags** | Tags block, normalization, creation on save, orphan sweep keeping colored tags, intersecting filters (`nte005`, `nte006`). |
| **Search** | Generated `tsvector` over title and derived text, prefix `tsquery`, title first then `ts_rank_cd`, filters before the 20-result cap, highlighted excerpts, `Ctrl+K`. |
| **Media library** | Items in folders, WebP thumbnails, soft delete keeping bytes, which pages use each item, used and unused space (`nte002`, `nte003`, `nte008`–`nte010`). |
| **Graph** | Global and local graph, families, collapse, camera controls, visibility filters, inspection. See [Graph View](graph-view.md). |
| **Version history** | Complete snapshots with a source, comparison, preview, restore (`nte011`). |
| **Comments and block dates** | Threads on words or blocks, resolve, panel; block stamps with source (`nte013`, `nte014`). See [Comments](comments.md). |
| **AI** | Summarize, improve, translate, continue; page summary; ask your notes; AI block; Telegram `/nota` capture (`nte015`). See [AI](ai.md). |
| **Other modules** | Live Agenda and Finances blocks; date chips with Agenda reminders; items of other modules; save an email to Notes (Communications, through `INoteImporter`); five Assistant tools. |
| **Frontend** | `client-web/src/modules/notes`, i18n EN + PT-BR, unit tests over `lib/`. |
| **Tests** | Domain unit tests (`Modules.Notes.Tests`: `PageContent`, hierarchy, graph, search, slugger, tag name, tools) and integration tests (`IntegrationTests/Modules/Notes`). |

## Deliberately out (not debt)

- **Drawing a whole page inside another.** A page link shows a card or the first lines; a synced block
  shows one block.
- **Editing a synced copy in place.** The original is edited where it lives.
- **Global tag rename** and **nested-tag rollup.**
- **Tag create/delete endpoints.** Pages create tags, the sweep removes them.
- **`ON DELETE CASCADE` on the page tree.** The commands walk the subtree themselves.
- **Inline editing in the folder's properties table.** It is read-only.

## Known open points

1. **Real-world checks.** The AI actions, Telegram capture and the live Agenda and Finances blocks were
   built and covered by tests; a check against the real provider, Telegram and real accounts is not
   recorded.
2. **Blob retention.** Media bytes live in `bytea`; moving to object storage is the trigger for a bucket
   backend.
3. **Unused bytes.** Deleting an item keeps its bytes, since pages and old versions may still show it.
   Nothing sweeps them; the library's "Unused" place lists the items, and deleting them is the user's
   call.
4. **In-memory graph walk.** Right for a personal notebook; the first thing to revisit if the graph
   grows.
5. **Tag filters are not in the URL**, so "pages with tag x" is not a shareable link.
6. **Ask your notes is lexical.** No synonyms or semantic matching; the Assistant's `ask_notes` widens
   the words through the model.
