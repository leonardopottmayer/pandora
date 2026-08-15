# Tags

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Search](search.md), [Graph View](graph-view.md)

---

## 1. The model: hybrid — the text rules, the row keeps the color

A tag is **written in the markdown** (`#ideias`), the way Obsidian does it, and the backend
materializes the edges on save — the same mechanic already proven by `PageLink`. That keeps the
module's central promise: markdown is the source of truth, and an exported `.md` carries its tags.

Above that sits a `Tag` row holding what the text cannot say: the **color**, and the name as it was
first written. It is not managed by CRUD — it is born when a tag appears in some content, and the
text stays in charge.

> Accepted consequence: **there is no global tag rename.** Renaming would mean rewriting the markdown
> of every page carrying it — that is find & replace, not a database operation.

## 2. Parsing rules

`#` is far more common in prose and shell than `[[`, so `TagParser` is stricter than the wikilink
parser:

| Rule | Effect |
|---|---|
| Must start a line or follow whitespace | `http://x#frag` and `src/lib#2` never fire. |
| Requires no space after `#` | A heading (`# Title`) is not a tag. |
| Allowed characters: letters, digits, `-`, `_`, `/` | `#projeto/pandora` is one nested-style tag. |
| Must contain at least one letter | `#123` is a number in the text, not a label. |
| **Code is removed before the search** | A `#comment` inside a fenced or inline code block is not a tag. The spans are replaced by spaces of the same length, so nothing outside them shifts. |

`TagName.ToSlug` then normalizes to the identity: lower-cased, accents stripped, keeping `/`, `-` and
`_` (unlike `Slugger`, which flattens everything to hyphens), capped at 50 characters. `#Café` and
`#cafe` are the same tag; the displayed **name** is how it was written first.

The frontend mirror is `lib/tags.ts`. Two quirks worth knowing there: the regex had to be built from
**quoted strings** (an escaped backtick is an invalid escape under the `u` flag), and the fence is
written twice — closed, and running to the end of the text — because `$` would mean end of *line*,
since the `m` flag is required by the start-of-line rule.

## 3. Synchronization and the orphan sweep

`PageTagSynchronizer` mirrors `PageLinkSynchronizer` — parse the body, resolve the tags, **diff** the
edges — with two additions the link graph does not need:

- It **creates** tags the text just invented; no CRUD screen could have created them.
- It **sweeps** the tags whose last page just dropped them, *unless they carry a color*. Color is the
  one thing the text cannot recover, so it is the one thing worth keeping an empty row alive for.

The sweep discounts the edges removed **in the same transaction** (the repository still sees them) by
passing the id of the page being saved.

Deleting a page runs `ClearAsync`, which drops all its edges and sweeps whatever that orphaned.

Two implementation notes with teeth:

- EF had to be told about the `page_tag → tag` foreign key (`HasOne<Tag>().WithMany().HasForeignKey(…)`)
  even with no navigation property. The sweep deletes an orphan tag and its edges in one transaction,
  and without the declared relationship EF emitted the tag's `DELETE` first and hit `fk_nte006_tag`.
  An integration test caught it.
- Re-saving unchanged text touches nothing, and no row is ever deleted and re-inserted inside one
  transaction — `uq_nte006_page_tag` would reject it.

## 4. Color

`PUT /notes/tags/{id}/color` is the only write. Only hex is accepted — `#rgb`, `#rrggbb`,
`#rrggbbaa` — validated in the handler because the value goes inline into the chip's `style`. An
invalid color answers **422**, the project's mapping for a validation error.

## 5. Filtering

The filter is a `tagIds` query parameter on four routes, and one rule governs all of them:
**several tags intersect (AND)**. Two selected tags show the pages carrying *both*. That is the
"keep narrowing" semantics a filter is expected to have, and one rule everywhere is more predictable
than OR in the graph and AND in the search.

`TagFilter.MatchingPageIdsAsync` is the single implementation. It returns `null` when no tag was
asked for — callers read that as *no filter*, not as *nothing matches* — and its result is not
owner-scoped, because callers intersect it with pages they already read for the user.

| Surface | Behaviour under a filter |
|---|---|
| **Sidebar** (`GET /notes/pages`) | Becomes a **flat list**, not a tree, and drag-and-drop is disabled. See [Pages & Hierarchy §7](pages-and-hierarchy.md#7-in-the-frontend). |
| **Search** (`GET /notes/pages/search`) | With an empty `q`, listing the pages of a tag *is* the navigation. Reads `ResultLimit × 10` and cuts afterwards — cutting first would drop a page matching both criteria. |
| **Graph** (`/graph`, `/{id}/graph`) | Nodes are cut **before** the neighborhood walk, so depth is counted over what remains. |
| **Page** | `PageDto` carries the page's own tags, drawn as chips; clicking one filters the sidebar by it. |

Filters live in component state and **do not enter the URL** — there is no shareable "pages with #x"
link. See [Implementation Status](implementation-status.md).

## 6. Out of scope

- **Global rename** (§1).
- **Real nested-tag hierarchy** — `#projeto/pandora` is accepted as the tag's text; there is no
  rollup of `#projeto` including its children.
- **Tag creation or deletion by API.** `TagsController` has no POST and no DELETE on purpose: the
  text creates them, the sweep removes them.
