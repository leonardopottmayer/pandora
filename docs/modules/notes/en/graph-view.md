# Graph View

[← Back to index](../README.md) · Related: [Links & Backlinks](links-and-backlinks.md), [Tags](tags.md)

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
- **Archived pages stay in the API graph payload**, carrying the flag; the global screen applies
  its visibility choice locally. The local graph continues to include archived neighbors.
- **`Degree` is computed in the backend**, counted **within the returned graph** — it is what sizes
  the node when drawn. The global screen recomputes it after applying visibility filters.
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
- Clicking a global node opens its details panel; the local graph still navigates directly to its page.

## 5. Visibility filters

The global graph uses **Show archived** from the sidebar on desktop; small screens put the same
control in the graph toolbar. It starts **off** and now applies to both the sidebar and graph.
**Hide isolated notes** is in the toolbar on all screen sizes and starts **off**. Both choices
last for the current visit, independently of the saved Show groups preference.

Visibility applies after the API's space/tag filters and before family grouping or collapsing.
`lib/graphVisibility.ts` removes archived nodes and their links when requested, recalculates node
degrees, then optionally removes nodes with no remaining links. A page linked only to a hidden
archive can therefore become isolated. Self-links count as connections, with degree two, matching
the backend rule. Hide isolated affects the graph, not the sidebar page list.

The graph's title locator, details, family counts, summary nodes and camera framing all use this
same filtered graph. Changing either switch clears selection and details. An empty result explains
the visibility filters; switches stay available to restore the view. Shared full-text page search
and the local page graph retain their existing behavior. This is an immutable client-side projection;
no API parameter, backend change, saved note or link is altered.

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
No schema change or new dependency is required. The local page graph does not use families.

This implements the idea recorded on 2026-11-02 using tags. Automatic communities and overlapping
regions remain future ideas.

## 7. Locate a note

The global graph's **Locate a note in the graph** selector searches titles among the currently
loaded nodes, ignoring case and accents. Space and tag filters therefore also limit the results.
Selecting a result centers the camera on that node at zoom 2 and highlights its immediate
connections without leaving the map or hiding other nodes. The selected label stays visible.
The selection also opens the details panel. Hover temporarily highlights another neighborhood;
leaving the node restores the search highlight.
Clearing the selector removes the persistent highlight and leaves the camera where it is.
Changing space or tag filters clears the selection and closes its details.
This is client-side navigation over the graph payload, separate from full-text page search.

## 8. Improvement ideas (2026-11-02)

These are ideas, not implementation commitments. Title search, details, collapsing families, navigation controls and visibility filters are delivered.

- **Details panel (implemented):** see the inspection behavior below.
- **Collapse families (implemented):** see the display projection below.
- **Navigation controls (implemented):** see the camera controls below.
- **Visibility filters (implemented):** see the shared archive choice and graph-only isolation filter above.
- **Connections between families:** summarize bridges and link counts between thematic regions.
- **Later:** pin node positions, remember the camera viewport and explore automatic communities.

Suggested next priority: an overview of bridges between families.

## 9. Inspect a note without leaving the graph

Click a global graph node, select a search result or choose a connected note in the panel to inspect
that note and focus its neighborhood. The panel shows its title/icon, archive status, tags, all
configured matching families and connected notes within the current graph filters. Family order
still determines the drawn region, even when several families match. Configured family metadata
is visible with Show groups off as well.

Connections include incoming and outgoing links, listing each other note once regardless of link
kind or direction. The **Open note** button is the explicit action that navigates to the editor.
The sidebar still opens pages directly. Closing details keeps the current selection and highlight;
clearing search or changing space/tag filters closes details and clears selection.

Desktop uses a side panel beside the canvas, allowing another node to be selected immediately.
Small screens use a scrollable drawer. All metadata comes from the existing graph and tag payloads;
no page content request, backend change or new persistence is required. The local page graph keeps
its original click-to-open behavior.

## 10. Collapse and expand families

With **Show groups** enabled, each nonempty family in the legend has a **Collapse/Expand** button.
Collapse replaces its visible members with one colored node labelled `Family · count`. Clicking
that summary expands the family; the legend's name still frames it. Families start expanded.
Unmatched pages stay visible, and priority still assigns each page to one family.

The display projection hides links internal to a collapsed family and remaps its external links
to the summary. Parallel links combine by displayed source, target and kind; direction and dashed
embed styling remain intact. Hovering an aggregated link shows its real link count. Counts and
membership reflect current space/tag filters; empty families produce no summary or legend entry.

Searching for a hidden page or selecting it from the details panel expands its owning family before
focusing it. Collapsing the family of the selected page clears selection and closes details. Summary
nodes expand rather than opening page details. Search and details always use the original graph.

Collapse state lasts for the current visit and is not persisted. Turning groups off restores every
real page and link; turning groups back on reapplies that visit's collapsed choices. Saving family
configuration expands all families. `lib/collapsedGraph.ts` creates fresh simulation data; no API,
schema or real note/link is changed. The local page graph remains unchanged.

## 11. Camera controls

Both global and local graphs have **Zoom in**, **Zoom out** and **Fit graph to view** buttons in
the upper-right corner. Buttons have accessible labels, keyboard activation and 40 px touch targets;
they remain separate from the family legend. They are disabled when no nodes are available.

Zoom changes the current scale by a factor of 1.25 with a short animation and the canvas's existing
zoom limits. Fit frames all currently drawn nodes, respecting space/tag filters and collapsed
families. Neither action clears the selection, closes details, changes family state or writes a
preference. Wheel/pinch navigation remains available.

A manual camera action, including framing a family, takes priority over automatic framing when
the simulation settles. Selecting another note or changing the graph/layout resumes automatic
positioning. The controls use the existing canvas API; no backend change or new dependency.
