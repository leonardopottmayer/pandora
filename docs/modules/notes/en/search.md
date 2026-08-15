# Search

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Tags](tags.md), [API Reference](api-reference.md)

---

## 1. The vector

Full-text search runs on a **generated `STORED` column** on `nte001_page`:

```sql
search_vector tsvector GENERATED ALWAYS AS (
    to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(content_markdown, ''))
) STORED
```

with a **GIN** index over it. Postgres maintains the vector, so no save path can forget to update it
— which is exactly why it is a generated column rather than something the application writes.

The configuration is **`simple`**: lower-casing only, no stemming, no language guess. The notebook
mixes Portuguese and English, and committing to one language would make the other match worse.

In EF the column is a **shadow property** (`PageColumns.SearchVector`) — the `Page` aggregate does not
know the vector exists. The query reaches it through
`EF.Property<NpgsqlTsVector>(…).Matches(…)`.

## 2. Translating what the user typed

`PageSearch` (Domain) turns the term into a `tsquery`: each word becomes `word:*`, joined by `&`. So
every word must be present, and the last one is a prefix — a palette matches while it is still being
typed.

Punctuation is **discarded, not escaped**: nothing typed can become `tsquery` syntax. When nothing
searchable is left, the result is an empty string and the caller answers with no results instead of
querying.

The same type cuts the **excerpt**: 160 characters around the first word of the term, keeping ~30
characters of context before the match, with ellipses marking a cut. A title-only hit falls back to
the head of the body.

## 3. The endpoint

`GET /notes/pages/search?q=…&tagIds=…`

- Cap of **20 results**, ordered by **title**.
- **Archived** pages appear, carrying the flag; soft-deleted ones never do.
- With a tag filter, the query reads `20 × 10` hits and intersects afterwards — cutting first would
  drop a page that satisfies both criteria.
- With tags and an **empty `q`**, it lists the pages of those tags: that is how a tag is browsed.

`PageSearchResultDto(Id, Title, Slug, Icon, IsArchived, Excerpt)` is the minimum the palette needs to
draw a row and open it.

## 4. The command palette

`components/SearchPalette.tsx`:

- **Ctrl+K** (Cmd+K on Mac), registered in *capture* phase so it wins against the editor.
- The term is debounced by 200 ms; arrows / Enter / Esc drive the list.
- The palette belongs to the Notes module, not to `AppLayout` — it only searches pages.
- Its `open` state lives in **the page**, not in the palette (`open` / `onOpenChange`), because the
  sidebar's magnifier button opens the same modal. The keyboard listener stayed inside the palette,
  where it belongs. Nothing about the search itself is duplicated.
- The magnifier sits at the top of the sidebar next to the graph and new-page buttons, and its
  `title` shows `Ctrl+K` — so the button also teaches the shortcut. `NotesGraphPage` mounts the same
  sidebar, so it mounts the palette too; otherwise the button would be dead on one of the two routes.
  As a bonus, Ctrl+K works on the graph route.

## 5. Not implemented

**Ranking and highlighting** are planned for v2 and are not here: no `ts_rank` ordering, no
`ts_headline` — the excerpt is a plain slice and the ordering is alphabetical by title. See
[Implementation Status](implementation-status.md).
