# Tags

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Search](search.md), [Graph View](graph-view.md)

---

## 1. The model: the page's tags block rules, the row keeps the color

A page's tags live in a **`tags` block**, the first block of the document:
`{ "type": "tags", "props": { "tags": ["dotnet", "Mensageria"] } }`. The header edits them as chips;
the editor saves them as that block.

Above that sits a `Tag` row (`nte005`) holding what the document cannot say: the **color**, and the
name as it was first written. There is no CRUD for it: a tag is born when a page first carries it.

> Accepted consequence: **there is no global tag rename.** Renaming would mean rewriting every page that
> carries it.

## 2. Normalization

`TagName.ToSlug` turns a name into its identity: lower-case, accents stripped, keeping `/`, `-` and `_`,
50 characters at most, and at least one letter (`123` is not a tag). `Café` and `cafe` are the same
tag; the displayed **name** is how it was written first.

## 3. Synchronization and the orphan sweep

On every save `PageTagSynchronizer` reads the tags block (at any depth), resolves the tags by slug and
**diffs** `nte006_page_tag`, with two additions the link graph does not need:

- it **creates** tags the page just introduced;
- it **sweeps** tags whose last page just dropped them, *unless they carry a color*. Color is the one
  thing worth keeping an empty row alive for.

The sweep discounts the edges removed in the same transaction by passing the id of the page being
saved. Deleting a page runs `ClearAsync`, which drops all its rows and sweeps what that orphaned.

EF knows the `page_tag → tag` foreign key even without a navigation property, so it deletes an orphan
tag's rows before the tag.

## 4. Color

`PUT /notes/tags/{id}/color` is the only write. Only hex is accepted (`#rgb`, `#rrggbb`,
`#rrggbbaa`), because the value goes inline into the chip's style; anything else answers **422**.

## 5. Filtering

The filter is a `tagIds` query parameter, and one rule governs every surface: **several tags
intersect (AND)**. `TagFilter.MatchingPageIdsAsync` is the single implementation; it returns `null`
when no tag was asked for, meaning *no filter*.

| Surface | Behaviour under a filter |
|---|---|
| **Sidebar** (`GET /notes/pages`) | A flat list instead of a tree; dragging is disabled. |
| **Search** (`GET /notes/pages/search`) | Filters before ranking and the 20-result cap. With an empty `q`, it lists the pages of the tags. |
| **Graph** (`/graph`, `/{id}/graph`) | Nodes are cut before the neighborhood walk. |
| **Page** | The header shows the page's tags; clicking one filters the sidebar. |
| **Pages by tag block** | `tag-pages` lists the pages carrying its tags. |

Filters live in component state and do not enter the URL.

## 6. Out of scope

- **Global rename** (§1).
- **Nested-tag rollup:** `projeto/pandora` is one tag; `projeto` does not include it.
- **Tag create/delete endpoints:** pages create tags, the sweep removes them.
