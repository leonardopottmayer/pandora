# Graph View

[← Back to index](../README.md) · Related: [Wikilinks & Backlinks](wikilinks-and-backlinks.md), [Tags](tags.md)

Nodes are pages, edges are `PageLink`. Because the data already exists, the graph is essentially a
**visualization** — the backend work is one query.

---

## 1. The two modes

| Mode | Route | Behaviour |
|---|---|---|
| **Global** | `GET /notes/pages/graph` | The whole network. |
| **Local** | `GET /notes/pages/{id}/graph?depth=N` | The neighborhood of one page, Obsidian-style. |

Both are served by the same `GetPageGraphQuery`: a `null` `RootPageId` *is* the global graph. `depth`
is clamped to **1..5** — beyond that the local graph is the global one with extra steps.

## 2. How the neighborhood is computed

The user's pages and their edges are loaded whole, and the neighborhood is cut **in memory** by
`PageGraph.Neighborhood` — a breadth-first walk with a visited set. A personal notebook is small, and
this keeps the depth walk out of SQL, with no recursive CTE.

The walk **ignores edge direction**: a page pointing at the open one is as much a neighbor as one it
points to. That is what Obsidian's local graph shows.

## 3. Payload rules

- An edge whose target no longer resolves (deleted page) is **discarded on read**, the same as in the
  backlinks panel. Every endpoint of an edge in `Edges` is guaranteed to be in `Nodes`, so the
  frontend never draws an edge into nothing.
- **Archived pages stay in the graph**, carrying the flag: archiving removes a page from the sidebar,
  it does not switch off its links.
- **`Degree` is computed in the backend**, counted **within the returned graph** — it is what sizes
  the node when drawn, so it has to respect the same cut as the rest of the payload.
- A page that both links and embeds another yields two edges, one per kind.
- `tagIds` cuts the nodes **before** the neighborhood walk, so depth is counted over what is left.

## 4. In the frontend

`react-force-graph-2d` (canvas + d3-force), split three ways:

| Piece | Role |
|---|---|
| `components/GraphView.tsx` | The canvas itself. |
| `components/LocalGraphPanel.tsx` | The Collapse next to the backlinks panel — **closed by default**, because the canvas runs a simulation and that should not run underneath every page the user opens to read. |
| `pages/NotesGraphPage.tsx` | The `/notes/graph` route, declared **before** `notes/:id` so it is not read as a page id. Entry point: the graph button at the top of the sidebar. |

- `lib/graphData.ts` (`toForceData`) **copies the nodes** before handing them to the simulation: it
  writes `x`/`y`/velocities into the objects it receives, and those objects would be the react-query
  cache.
- Hover highlights the immediate neighborhood and fades the rest; labels only appear past a certain
  zoom — without that a graph of any size becomes a smear. Embeds are drawn dashed.
- Clicking a node navigates to its page.

## 5. Not implemented

The global graph has **no archived toggle of its own**. The `includeArchived` state on
`NotesGraphPage` only narrows the sidebar; the graph query never receives it, so archived pages are
always drawn. If it starts to bother, the place is `NotesGraphPage` plus a parameter on
`GetPageGraphInput`. See [Implementation Status](implementation-status.md).
