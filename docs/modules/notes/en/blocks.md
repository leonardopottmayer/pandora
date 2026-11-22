# Blocks & Editor

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Links & Backlinks](links-and-backlinks.md), [Media Library](media-library.md)

---

## 1. The document

`nte001_page.content` holds one document per page:

```json
{
  "version": 1,
  "blocks": [
    { "id": "b8f3k2", "type": "tags", "props": { "tags": ["dotnet", "messaging"] } },
    { "id": "c1x9qa", "type": "heading", "props": { "level": 1 }, "content": [{ "text": "Why an outbox" }] },
    { "id": "d72mpe", "type": "paragraph", "content": [
        { "text": "Related: " },
        { "mention": { "page": "0198f6a2-…" } },
        { "text": " and more", "marks": [{ "type": "bold" }] }
    ] },
    { "id": "e4a0zz", "type": "bulleted", "content": [{ "text": "Consumers are idempotent." }], "children": [
        { "id": "f0q1rr", "type": "bulleted", "content": [{ "text": "Keep the processed ids." }] }
    ] }
  ]
}
```

- **Block:** `id` (unique in the page), `type` (`[a-z][a-z0-9-]*`), optional `props`, `content`
  (inlines) and `children` (blocks).
- **The backend checks the shape only** (`PageContent`): valid JSON, a `blocks` list, an id and a
  valid type on every block, unique ids, `content` as a list of objects, at most 2 MB, 10,000 blocks
  and 24 levels. What each type's props mean is the frontend's business.
- **An unknown type survives.** The editor keeps a block it does not know as-is and saves it back
  untouched.

## 2. Inlines

| Inline | Stored as | Notes |
|---|---|---|
| Text | `{ text, marks? }` | Marks: `bold`, `italic`, `underline`, `strike`, `code`, `link` (`href`), `color` and `highlight` (`attrs.color`), `comment` (`attrs.thread`) |
| Page mention | `{ mention: { page } }` | Typed with `@` or `[[`; drawn boxed with the page's current title |
| Date | `{ date: { value, reminderId? } }` | `YYYY-MM-DD` or `YYYY-MM-DDTHH:mm` (local); can carry an Agenda reminder |
| Status | `{ status: { label, color? } }` | A pill |
| Item of another module | `{ item: { module, kind, id, label } }` | Agenda tasks and reminders, Finances accounts and cards; the label is kept as it was when linked |
| Inline math | `{ math: { latex } }` | KaTeX; `$x^2$` typed in the text becomes one |

The eight colours, for text, backgrounds, statuses and highlights: `gray`, `orange`, `yellow`,
`green`, `blue`, `purple`, `magenta`, `red`.

The search text reads every inline as its words: a date as its value, a status or item as its label,
math as its LaTeX.

## 3. Block catalog

### Basic

| Block | Stored as |
|---|---|
| Paragraph | `paragraph` |
| Heading 1–3 | `heading`, `props.level`; `props.toggle: true` makes it collapsible, with `props.open` and the section in `children` |
| Bulleted / numbered list item | `bulleted`, `numbered` (nesting in `children`) |
| To-do | `todo`, `props.checked` |
| Toggle list | `toggle`, `props.open`, body in `children` |
| Quote | `quote` |
| Divider | `divider` |
| Callout | `callout`, `props.tone` (`note`, `info`, `success`, `warning`, `error`), optional title and fold (`+`/`-`), body in `children` |
| Columns | `columns` with 2 to 4 `column` children; a narrow screen stacks them |

Paragraphs, headings, list items, quotes, code, callouts and toggles take `props.color`,
`props.background` and `props.align` (`center`, `right`).

### Media

| Block | Stored as | Draws as |
|---|---|---|
| Image | `image`, `props.mediaId` (or `src`), `width`, `align`; caption in `content` | The image; a click opens it full screen |
| Gallery | `gallery`, `props.items[]` of `{ mediaId, caption? }` | A grid of library images |
| Video | `video`, `props.url` | YouTube, Vimeo or Loom player, or the browser's own for a video file |
| Audio | `audio`, `props.mediaId` (+ `name`) or `props.url` | The browser's player |
| File | `file`, `props.mediaId`, `name` | A card with icon, name and size; a click downloads it |
| PDF | `pdf`, `props.mediaId` (+ `name`) or `props.url` | The browser's PDF reader, in the page |

### Advanced

| Block | Stored as | What it does |
|---|---|---|
| Code | `code`, `props.language` | A code block |
| Table | `table`, `props.header`, `props.rows` (rows of cells, each a list of inlines) | Editable table with a header row |
| Mermaid | `mermaid`, `props.code` | The diagram (strict security, SVG sanitized); the code is edited under it |
| Math | `math`, `props.latex` | A KaTeX formula (`trust: false`) |
| Synced block | `synced` with `children`; copies are `synced-ref`, `props.pageId`, `props.blockId` | See [Links & Backlinks §4](links-and-backlinks.md#4-synced-blocks) |
| Template button | `template-button`, `props.label`, template in `children` | A click inserts a copy below it, with fresh ids |
| Decision | `decision`, `props.status` (`decided`, `rejected`, or none), text in `content` | A line with its status as a pill |
| Breadcrumb | `breadcrumb` | The page's space, folders and parent pages |
| Sub-page list | `subpages` | The page's sub-pages, in order |
| Pages by tag | `tag-pages`, `props.tags` | The pages carrying those tags |
| Page properties | `properties`, `props.rows[]` of `{ key, value }`, each a list of inlines | A two-column table; read by the folder's table view |
| AI block | `ai`, `props.prompt`, `props.output`, `props.generatedAt` | See [AI §5](ai.md#5-ai-block) |
| Tags | `tags`, `props.tags` (names) | Edited in the page header, saved as the first block; see [Tags](tags.md) |

### Data (live blocks)

These store only their configuration. Live figures never enter the document, autosave, search,
versions or templates, and a public page shows "Private content" without querying anything.

| Block | Configuration | Shows |
|---|---|---|
| Agenda | `agenda-list`: `{ mode: "today" }`, `{ mode: "tasks", listId }` or `{ mode: "events", days: 7 \| 30, calendarId? }` | Today's events, tasks and reminders; the open tasks of a list; or upcoming events, read-only, with "Open in Agenda" and Refresh |
| Finances | `finance-summary`: `{ mode: "account", accountId }`, `{ mode: "card", cardId }` or `{ mode: "expenses", month? }` | An account's posted and projected balance; a card's total and available limit; or a month's posted expenses by category, per currency |

They reuse the other module's own endpoints, hooks and query keys, and need that module's feature and
read permissions. A missing, archived or foreign source shows "unavailable"; nothing falls back to
another source. Markdown and HTML exports write only `[Agenda]` or `[Finances]`.

### Embeds

| Block | Stored as | Draws as |
|---|---|---|
| Page link | `page-link`, `props.pageId`, `props.look` (`preview`, or a card when absent) | A card, or the first lines of the page; never the whole page |
| Bookmark | `bookmark`, `props.url`, `title`, `description`, `siteName`, `imageUrl`, `iconUrl` | A card; what the site said is saved when the block is made |
| Embed | `embed`, `props.url` | The site's frame for YouTube, Vimeo, Loom, Figma, Google Maps, Drive and Docs, Miro, CodePen and gists; the site's HTML for X, Bluesky and any oEmbed site; otherwise a card |

`GET /notes/links/preview` reads a URL on the server, refusing private addresses (SSRF), to fill a
bookmark or an embed. Every frame is sandboxed; HTML from oEmbed or a gist runs in a frame without
Pandora's origin, which only reports its height.

## 4. The editor

`components/visual/VisualEditor.tsx`, TipTap 3. `lib/blocks.ts` converts between blocks and TipTap's
JSON, and `lib/visualSchema.ts` gives every block node a `blockId`.

- **Autosave** 800 ms after the last change, with "Saving…" / "Saved" in the header.
- **`/` menu** in groups: Basic, Media, Advanced, Data, Inline, Embed. Typing also finds colours
  (`/red`, `/blue background`; `/vermelho`, `/fundo azul`) and "turn into" commands, which act on the
  block the slash was typed in.
- **`@`** lists pages, then dates (`@today`, `@amanhã 14h`, `@25/12`, `@2026-12-25`) and, once something
  is typed, items of other modules. `@remind tomorrow 9:00` (`@lembrar …`) creates the date with its
  Agenda reminder. Removing the date from the text does not delete the reminder.
- **`:`** plus a word offers emoji (`:fire`, `:fogo`); the system panel has the rest.
- **Block menu** on the drag handle: turn into, colour, alignment, copy link to block, duplicate, move
  to page, comment, block dates, delete.
- **Several blocks:** Ctrl/Cmd-drag, Shift+↑/↓ or Ctrl/Cmd+A select whole blocks; the same actions
  apply to all, and the handle drags them together. Moving to another page appends them there with
  fresh ids.
- **Link to a block:** `/notes/<page>#block-<id>` scrolls to the block and marks it for a moment.
- **Selection bar:** marks, link, colour, comment, and the AI actions.
- **Pasting** a file sends it to the library (an image becomes an image block, audio an audio block, a
  PDF a PDF block, other files a file card). A link alone on a line asks: link, bookmark or embed.
- **Read-only** when the page is locked, on a public page, and on a phone until "Edit".
- **Table of contents** (headings 1–3) beside the page on a wide screen, folded above it otherwise,
  marking the section being read.

Mermaid and KaTeX load the first time a page shows one of them (separate chunks).
