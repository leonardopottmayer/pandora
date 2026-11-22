# Links & Backlinks

[← Back to index](../README.md) · Related: [Graph View](graph-view.md), [Blocks & Editor](blocks.md), [Data Model](data-model.md)

The links between pages are **derived from the blocks**, not authored separately. They are also what
the [graph view](graph-view.md) draws.

---

## 1. Three ways to link a page

| In the document | Edge kind | Drawn as |
|---|---|---|
| A **mention** inline: `{ mention: { page } }`, typed with `@` or `[[` | `wikilink` | The page's title, boxed, inside the text |
| A **page-link block**: `page-link`, `props.pageId`, `props.look` | `embed` | A card, or a preview with the first lines of the page |
| A **synced copy**: `synced-ref`, `props.pageId`, `props.blockId` | `embed` | The original block, read-only (§4) |

A mention can be turned into a card or a preview, and a card or preview back into a mention, from the
selection or the block menu.

Every link stores the target's **id**. A rename never breaks one, and nothing is resolved by title.
A whole page is never drawn inside another.

## 2. Rebuilding the edges

`PageContent` collects the mentioned and linked page ids in its walk; `PageLinkSynchronizer` then
diffs them against `nte004_page_link`:

1. the set the document asks for, as `(targetId, kind)` pairs, limited to the owner's live pages;
2. stored edges no longer wanted are removed;
3. only the new ones are inserted.

Saving an unchanged document touches nothing, and the same row is never deleted and re-inserted in
one transaction (`uq_nte004_edge` would reject it).

## 3. "Mentioned in"

`GET /notes/pages/{id}/backlinks` returns the pages referencing this one:
`BacklinkDto(PageId, Title, Slug, Icon, IsArchived, Kind)`. A page that both mentions and embeds the
open one appears once per kind. The page shows them under the document, after its sub-pages.

A page in the trash keeps the edges pointing at it; reads filter them out, and a restore brings them
back. Deleting for good removes edges in both directions.

## 4. Synced blocks

- **The original** is a `synced` block whose `children` are its content, edited where it lives.
- **"Copy to sync"** in its menu puts it on the clipboard. Pasted into another page, it becomes a
  `synced-ref` that shows the original's blocks **read-only**, with a way to open the original and
  edit it there. Two pages therefore never write the same block.
- A copy counts as a link of kind `embed`, so the original's page lists the copy's page in "Mentioned
  in".

## 5. On a public page

A mentioned page reads as "Private page", and a page-link block or synced copy shows nothing of the
other page. Only the page behind the link is public.
