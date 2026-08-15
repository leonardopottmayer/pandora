# Pages & Hierarchy

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Editor](editor.md), [API Reference](api-reference.md)

---

## 1. What a page is

A **page** is a markdown document owned by the user and the aggregate root of the module
(`nte001_page`). Everything else in Notes either derives from its body (edges, tags, the search
vector) or hangs off it (attachments).

A page carries a `title`, an optional emoji `icon`, the `content_markdown` body, its place in the
tree (`parent_id` + `order_index`), and three states: favorite, archived, deleted.

## 2. The tree

The sidebar hierarchy is `parent_id` and nothing else — `NULL` means a root page. Siblings are
ordered by `order_index`. The frontend nests the flat list from `GET /notes/pages` with
`lib/buildTree.ts`; the backend never returns a nested payload.

**Cycles are rejected.** `Page.Move` only guards the trivial "parent is myself" case; the real
invariant needs the whole tree, so it lives in `PageHierarchy.WouldCreateCycle`, which walks upward
from the prospective parent over a map of every page's parent and reports a cycle if the walk ever
reaches the page being moved. A broken or foreign parent link ends the walk without a cycle.

This is the opposite stance from the wiki graph, which expects cycles — see
[Wikilinks & Backlinks](wikilinks-and-backlinks.md).

## 3. Slugs

The slug is derived from the title by `Slugger` — lower-cased, accents stripped, non-alphanumerics
collapsed into single hyphens, capped at 80 characters, falling back to `untitled` when nothing
usable is left. `CreatePage` then appends `-2`, `-3`, … until it is free for that user.

Two rules follow from the schema:

- **The slug is frozen at creation.** Renaming a page does not re-slug it, so a `[[link]]` written
  against the old slug keeps resolving.
- **A soft-deleted page frees its slug**, because `uq_nte001_user_slug` is a partial index over
  `deleted_at IS NULL`.

## 4. States

| State | Storage | Meaning |
|---|---|---|
| **Favorite** | `is_favorite` boolean | Purely a marker for the sidebar. |
| **Archived** | `archived_at` timestamptz | Hidden from the default tree, **still editable**, still in the graph and in search results (flagged). `POST /archive` is a no-op if already archived. |
| **Deleted** | `deleted_at` timestamptz | Soft delete. The row and its history stay; every read filters it out. |

Archiving and deleting are timestamps rather than booleans so the module records *when* — and so the
`TimeProvider` injected into the aggregate is what tests control.

## 5. Deleting a subtree

`DeletePageCommandHandler` does more than flip one flag. It loads the user's whole tree, walks the
subtree breadth-first from the target, and for every page in it:

1. soft-deletes the page (so no child is left pointing at a deleted parent);
2. removes the `PageLink` edges **leaving** it — edges pointing *at* it stay, and are filtered out on
   read, which means restoring the row would restore its inbound mentions;
3. clears its `PageTag` rows through `PageTagSynchronizer.ClearAsync`, which also sweeps any tag that
   just lost its last page and has no color.

There is deliberately **no `ON DELETE CASCADE`** on `fk_nte001_parent`: a database cascade would be a
hard delete of the history the soft delete exists to keep.

## 6. Commands and queries

| Use case | Type | Notes |
|---|---|---|
| `CreatePage` | Command | Resolves a unique slug; parses the body for links and tags (a page can be born with content). |
| `UpdatePage` | Command | Title + icon + body — the autosave path. Rebuilds edges and tags. |
| `MovePage` | Command | Reparent + reorder, rejecting cycles. |
| `SetPageFavorite` / `SetPageArchived` | Commands | Two routes each (`/favorite` ↔ `/unfavorite`, `/archive` ↔ `/unarchive`) over one flag-carrying command. |
| `DeletePage` | Command | The subtree walk above. |
| `GetPage` | Query | Full `PageDto` including the body and the page's tags. |
| `GetPageTree` | Query | Flat `PageSummaryDto` list (no body), filtered by `includeArchived` and `tagIds`. |

`PageDto` carries the page's tags. On the save path they come straight from `PageTagSynchronizer`,
which just wrote them; on the read paths that never touched the body (open, move, favorite, archive)
`PageTagReader` loads them instead.

## 7. In the frontend

- **Sidebar** (`components/NotesSidebar.tsx`): the tree, with expand/collapse, drag-and-drop for
  reordering and reparenting (`lib/moveMath.ts` computes the resulting `parentId` + `orderIndex`),
  per-page actions, an archived toggle, a tag filter and a search button.
- **Icon** (`components/PageIconPicker.tsx`): a button left of the title opening a popover with a
  free field, a row of suggestions and "remove". No emoji library — every OS ships a picker
  (`Win+.`), and what gets typed is reduced to its **first grapheme** via `Intl.Segmenter`, because
  an emoji is often several code points (flags, skin tones, families) and cutting by index would
  yield half of one. The icon rides in the `PageDraft`, so it saves through the same autosave as the
  title and the body.
- **Filtered sidebar turns into a flat list**, not a tree: filtering a hierarchy by tag breaks it
  (a child matches, its parent does not), and inventing an ancestor rule would answer a question
  nobody asked. While a filter is active, **drag-and-drop is disabled** — a drop would be reordering
  against siblings that are not on screen.
