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

## 6. Tag families and thematic regions

On the **global graph**, open **Configure families** to add groups with a name, color and one or more
existing tags. Any selected tag qualifies. The first matching family owns the page; arrows change
priority. Each page appears once, and unmatched pages stay outside the regions. Hover shows all
matching families, even when priority places the page in just one region.

**Show groups** is **off by default**. Turning it on gathers families into regions with a faint fill,
rounded outline and label, retaining real links between families. The legend shows visible counts;
clicking a family frames its members. Empty families draw no region. Space/tag filters narrow the
graph before grouping. Small screens use a wrapping toolbar with a tag filter; labels stay in the viewport.

The flag and ordered family configuration persist **per account in this browser**, under
`pandora.notes_graph.<userId>`, like the device-local editor mode. They do not sync across devices.
Blocked storage leaves the choice active for the current visit. Family editing uses a draft: Save
applies it and Cancel discards it. Deleted tags appear as unavailable and match no pages.

The backend adds `TagIds` with one batch read of the returned pages' tags. `lib/graphGroups.ts` assigns
membership and provides an anchor force; `lib/graphRegions.ts` draws behind real nodes/links. Turning
grouping off restores the original link force. Cached nodes and tag arrays are copied before simulation.
No schema change or new dependency is required. The local page graph keeps its original presentation.

This implements the idea recorded on 2026-11-02 using tags. Automatic communities and overlapping
regions remain future ideas.

## 7. Locate a note

The global graph's **Locate a note in the graph** selector searches titles among the currently
loaded nodes, ignoring case and accents. Space and tag filters therefore also limit the results.
Selecting a result centers the camera on that node at zoom 2 and highlights its immediate
connections without leaving the map or hiding other nodes. The selected label stays visible.
Hover temporarily highlights another neighborhood; leaving the node restores the search highlight.
Clearing the selector removes the persistent highlight and leaves the camera where it is.
Changing space or tag filters clears the selection. Clicking a canvas node still opens its page.
This is client-side navigation over the graph payload, separate from full-text page search.

## 8. Improvement ideas (2026-11-02)

These are ideas, not implementation commitments. Title search above is the first delivered item.

- **Details panel:** select a node to inspect its title, tags, families and connections, with an
  explicit action to open the page. This would change the current click-to-open behavior.
- **Collapse families:** replace a region with one node such as "Work · 25 notes", then expand it.
- **Navigation controls:** visible zoom in/out and fit-all buttons, especially useful on mobile.
- **Visibility filters:** hide archived pages and isolated nodes independently; the existing archive
  toggle only affects the sidebar.
- **Connections between families:** summarize bridges and link counts between thematic regions.
- **Later:** pin node positions, remember the camera viewport and explore automatic communities.

Suggested next priority: the details panel, followed by collapsing families when the graph grows.
