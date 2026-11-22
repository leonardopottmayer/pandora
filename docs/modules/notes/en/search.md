# Search

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Tags](tags.md), [API Reference](api-reference.md)

---

## 1. The vector

Full-text search runs on a **generated `STORED` column** on `nte001_page`:

```sql
search_vector tsvector GENERATED ALWAYS AS (
    to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(content_text, ''))
) STORED
```

with a **GIN** index over it. `content_text` is derived from the blocks on every save (`PageContent`): every inline's words, table cells, captions, code, page properties and the accepted output of AI blocks. Chips count as their words: a date as its value, a status or an item as its label, math as its LaTeX.

Postgres maintains the vector, so no save path can forget to update it
— which is exactly why it is a generated column rather than something the application writes.

The configuration is **`simple`**: lower-casing only, no stemming, no language guess. The notebook
mixes Portuguese and English, and committing to one language would make the other match worse.

In EF the column is a **shadow property** (`PageColumns.SearchVector`) — the `Page` aggregate does not
know the vector exists. The query reaches it through
`EF.Property<NpgsqlTsVector>(…).Matches(…)`.

## 2. Translating what the user typed

`PageSearch` (Domain) turns the term into a `tsquery`: each word becomes `word:*`, joined by `&`. So
every word must be present, and each is a prefix — a palette matches while it is still being
typed.

Punctuation is **discarded, not escaped**: nothing typed can become `tsquery` syntax. When nothing
searchable is left, the result is an empty string and the caller answers with no results instead of
querying.

The same type cuts the **excerpt**: 160 characters around the first matching word prefix, keeping ~30
characters of context before the match, with ellipses marking a cut. A title-only hit falls back to
the head of the body.

## 3. The endpoint

`GET /notes/pages/search?q=…&tagIds=…&spaceId=…`

- Cap of **20 results**: title matches first, then descending `ts_rank_cd` over the stored vector
  (cover density considers repeated matches and proximity), then title and id for stable ties.
- **Archived** pages appear, carrying the flag; soft-deleted ones never do.
- Owner, space and intersecting tag filters apply **before** ranking and limiting. There is no
  intermediate 200-hit window that could hide a matching tagged page.
- With tags and an **empty `q`**, it lists the pages of those tags: that is how a tag is browsed.

`PageSearchResultDto(Id, SpaceId, Title, Slug, Icon, IsArchived, Excerpt)` is the minimum the palette needs to
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

## 5. Highlighting

`SearchHighlight` highlights whole words beginning with a query prefix in titles and excerpts,
case-insensitively, using Unicode letters/numbers and treating punctuation as separators. Accents
remain significant, like the existing `simple` search; this does not add stemming or fuzzy search.

The API keeps `Excerpt` as **plain text**, also usable by the assistant. The palette creates React
text and `<mark>` nodes with theme tokens rather than inserting `ts_headline` HTML. Literal HTML stays text. While placeholder results from the previous query are
visible, highlighting is suppressed until the new results arrive. No schema change is required.

## 6. Ask your notes

A tab beside the search answers a question from the notes instead of listing pages. It uses the same
vector with an OR query; see [AI §3](ai.md#3-ask-your-notes).
