# Notes — Product Plan (what is left)

[← Back to index](../README.md) · 🇧🇷 [Versão em português](../pt-BR/product-plan.md)

> **Status:** the blocks revamp (B1–B11) is built for every module that exists. What exists is in
> [Implementation Status](implementation-status.md) and the topic documents; this file keeps only what
> is not built yet.

---

## Blocks that wait for other modules

| Block | Waits for |
|---|---|
| A **Radar** item saved as a page | [Radar](../../radar/README.md) |
| A **Databases** view inside a page | [Databases](../../databases/README.md) |
| A **Projects** board or item list inside a page | [Projects](../../projects/README.md) |

They go in the `/` menu's **Data** group, like the Agenda and Finances blocks, and follow the same
rule: the block stores only its configuration, reads the other module's own endpoints, and a public
page shows "Private content".

## Later (not scheduled)

- **Properties report** across pages (Confluence's "page properties report"); the folder's table view
  covers one folder today.
- **Inline editing** in the folder's properties table.
- **Fixed dates** in the Agenda block; it shows rolling windows of 7 or 30 days.
- **Tag filters in the URL**, as a search parameter on `NotesPage`.
- **Bucket storage and video uploads:** a bucket backend (each media row already records its backend),
  ranged downloads, and a poster frame rendition.
- **Graph ideas:** a bridge overview between families, saved layouts, automatic communities. See
  [Graph View](graph-view.md).
