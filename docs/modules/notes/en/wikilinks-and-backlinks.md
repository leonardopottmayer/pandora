# Wikilinks & Backlinks

[← Back to index](../README.md) · Related: [Graph View](graph-view.md), [Data Model](data-model.md), [Editor](editor.md)

The wiki graph is **derived from the markdown**, not authored. It is also the foundation the
[graph view](graph-view.md) draws.

---

## 1. Syntax

| Written | Meaning |
|---|---|
| `[[Target]]` | A wikilink to the page whose **title** or **slug** is `Target`. |
| `[[Target\|alias]]` | The same link, displayed as `alias`. The target stops at the pipe. |
| `![[Target]]` | An **embed** — a separate edge kind. |

`WikilinkParser` returns each reference once per (target, kind) pair: a target linked five times in
one page is one fact, but a page that both links and embeds another produces two edges.

## 2. Resolving the target

`PageLinkSynchronizer` resolves against the owner's pages, **title first (case-insensitive), then
slug**. Both spellings are looked up in one round trip, so `[[Meeting Notes]]` and `[[meeting-notes]]`
collapse into the same edge.

A reference matching no page produces **no edge**. A broken link exists only in the text — the graph
never contains an edge into nothing.

## 3. Rebuilding the edges

Parsing runs on **update and on create** (a page can be born with content). The rebuild is a **diff**,
not a wipe-and-recreate:

1. compute the set the body asks for, as `(targetId, kind)` pairs;
2. remove the stored edges that are no longer wanted;
3. insert only the genuinely new ones.

Re-saving unchanged text touches nothing — and, importantly, the same row is never deleted and
re-inserted inside one transaction, which `uq_nte004_edge` would reject.

## 4. Deletion semantics

Soft-deleting a page removes the edges **leaving** it. Edges pointing **at** it stay in the table and
are filtered out on read, which means restoring the row would restore its inbound mentions. The same
filtering is what guarantees the graph payload never carries an edge into a node that is not there.

## 5. Backlinks ("linked mentions")

`GET /notes/pages/{id}/backlinks` returns the pages referencing this one, as `BacklinkDto(PageId,
Title, Slug, Icon, IsArchived, Kind)`. A page that both links and embeds shows up once per kind — the
same shape the graph uses. `ix_nte004_target_page_id` exists for this read.

The frontend shows them in `components/BacklinksPanel.tsx`, next to the local-graph panel.

## 6. In the frontend

- `lib/wikilinks.ts` mirrors the backend parser and slugger, so the preview resolves a reference
  exactly the way the next save will. It is deliberate duplication, unit-tested on its own.
- An unresolved `[[target]]` renders as a **create-on-click** link: clicking it creates the page and
  navigates to it.
- An `![[embed]]` renders as an ordinary link. Rendering the target's content inline is **not
  implemented** — see [Implementation Status](implementation-status.md).
- Autocomplete of `[[` is described in [Editor §4](editor.md#4-three-autocomplete-menus). It arrived
  after the rich-blocks work, which is what brought `@codemirror/autocomplete` in as a direct
  dependency and gave it a menu to sit beside.
