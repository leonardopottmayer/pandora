# Editor & Rich Blocks

[← Back to index](../README.md) · Related: [Pages & Hierarchy](pages-and-hierarchy.md), [Media Library](media-library.md), [Tags](tags.md)

The editor is **100% frontend**. Every block it produces is ordinary markdown, which
`content_markdown` already stored — nothing here required a backend change.

---

## 1. The surface

Two editors over the same text. `components/visual/VisualEditor.tsx` is the **visual editor** (TipTap),
where one writes on the page as it is drawn, Notion-style — see [§9](#9-visual-editor).
`components/MarkdownEditor.tsx` is CodeMirror 6 over the raw markdown; `components/MarkdownPreview.tsx`
renders it with `marked` + DOMPurify. `NotesPage` has four modes — `visual` (the default, and the one a
phone opens on Edit), `edit` (markdown), `split` (markdown and reading side by side) and `preview`.

What is saved is always the markdown: both editors read and write the same `content_markdown`, and
switching modes carries what is not saved yet (both start from the draft, not from the saved page).
Sections 4 to 7 describe the markdown editor; the visual one reuses the same lists and filters.

## 2. Autosave

`hooks/useAutosave.ts` debounces at **800 ms** and saves through a TanStack Query mutation, exposing a
status the page displays. The draft (`PageDraft`) holds title, icon and body together, so all three
save through the same path — the icon is not a separate call.

`hooks/useDebouncedValue.ts` (200 ms) is the other debounce, used by the search palette: one request
per pause in typing, not per keystroke.

## 3. Inline upload

Pasting (`Ctrl+V`) or dropping a file uploads it to the media library, unsorted (`POST /notes/media`),
and inserts `![name](/api/v1/notes/media/{id})` at the cursor — a link `[name](…)` for a file that is not
an image. The label is the item's name, which is also what the download handler later uses to name
the saved file. `/image` and `/file`, or the toolbar's image button, insert from the library instead —
see [Media inside notes](media-library.md#9-media-inside-notes).

## 4. Three autocomplete menus

All three are registered on `@codemirror/autocomplete` and share the same three mechanics:

- **Our own filtering**, with `filter: false` on the `CompletionResult`. With CodeMirror's filter on,
  an option matched by something other than its `label` — a page matched by its slug, a command
  matched by its translated label — would be discarded right after we chose to include it.
- **Inputs by ref**, not by effect dependency. The page list and the `t` function change often;
  rebuilding the editor on every change would throw away the undo history.
- **A tooltip parented to `document.body`** (`tooltips({ parent })`), because the editor lives inside
  a `Card` with `overflow: hidden` that would clip the menu near the panel's bottom edge.

| Trigger | Source | Rules |
|---|---|---|
| `[[` | `wikilinkTriggerAt` in `lib/wikilinks.ts` | Opens after `[[` and after `![[`, which writes the target the same way. **Refuses** after a `]` or a `\|` — a closed link or half an alias is no longer the target being typed. `filterPages` matches **title and slug**, so `cafe` finds `Café com Pão`; capped at 10 options. On apply, an existing `]]` right after the cursor is **reused** rather than duplicated. |
| `#` | `lib/tags.ts` | Completes **existing tags only** — a new tag is simply typed, which is the whole point of it living in the text. |
| `/` | `slashTriggerAt` in `lib/slashCommands.ts` | Only opens with the `/` at the start of a line or after whitespace (so `src/lib` mid-sentence never fires), and closes at the first space typed. `filterCommands` matches by **id and translated label**. |

## 5. Slash commands

`SLASH_COMMANDS` inserts plain markdown — every entry is something the user could have typed by hand,
which is what keeps the document round-trippable.

| Group | Entries |
|---|---|
| Blocks | `h1`, `h2`, `h3`, bullet list, numbered list, todo (`- [ ] `), quote, code block, divider, toggle (`<details>` with the cursor in the summary, blank lines around the body so markdown inside still renders), table, image, file, link to a page (opens `PagePickerModal`: each space's tree with its icons, and a search showing the path to each page; picked, it writes `[[Title]]`) |
| Callouts | one per callout type — `note`, `tip`, `info`, `warning`, `danger`, `quote` |

Each command declares the text it inserts and the cursor offset within it.

## 6. Callouts

Obsidian syntax, six types: `note`, `tip`, `info`, `warning`, `danger`, `quote`.

`lib/callouts.ts` is a **`marked` extension** (block-level tokenizer + renderer), not a string
pre-process like the wikilinks — so the callout's body keeps going through the lexer and `**bold**`
inside it still works.

- An unknown type (`> [!frobnicate]`) is **refused** by the tokenizer and falls through to `marked`'s
  ordinary blockquote. That is deliberate: the syntax is meant to degrade into a blockquote in any
  other markdown renderer, and that is what keeps the file portable.
- A callout with no title falls back to the **translated** type name, so the parser is a private
  `Marked` instance memoized by `i18n.language` in `MarkdownPreview` — mutating the global `marked`
  via `marked.use()` was avoided on purpose.
- The icon is an emoji embedded in the HTML; the color comes from one `--notes-callout-accent` per
  type, serving as the border and, through `color-mix`, as the background. One token per type.

**Collapsible callouts** take Obsidian's sign after the marker: `> [!tip]- Title` starts closed,
`> [!tip]+ Title` starts open. They render as `<details>`/`<summary>`, so the browser does the folding
with no script, and degrade into a blockquote anywhere else.

## 7. Markdown tables

`lib/markdownTables.ts` is pure functions over the document's lines; `lib/editorCommands.ts` is the
only part that knows about `EditorView`. That split is what allowed testing Tab against a mounted
editor instead of only testing the arithmetic.

- **Tab / Shift+Tab** move between cells, **reformatting the whole table first**, so the pipes
  realign while you type. `formatTable` is idempotent (there is a test) — repeated Tab does not keep
  widening a column.
- Minimum column width is 3 (the `---`), and the alignment colons the user wrote are preserved, with
  only the dashes between them stretched.
- Tab **refuses** — leaving Tab worth what it was worth — outside a table, with an open selection,
  and on a loose line of pipes that is not yet a table: reformatting a line someone is still writing
  would be worse than doing nothing. Shift+Tab on the first cell refuses too.
- Tab on the last cell **appends a row**; the separator line is skipped in both directions.
- There is no "delete row/column" shortcut: it is markdown, you delete the line.

**Column alignment from the UI** is out of scope; if it lands, the place is the separator line in
`markdownTables.ts`.

## 8. Preview

`MarkdownPreview` renders with `marked`, sanitizes with DOMPurify, and then post-processes:

1. **Wikilinks** are resolved against the page list using `lib/wikilinks.ts` — the frontend mirror of
   the backend parser, so what the preview links to is what the next save will link to. An unresolved
   `[[target]]` renders as a "create on click" link. Embeds render as ordinary links.
2. **`#tags`** render as colored chips, painted with the module-wide tag list. The chip sets `color`
   and `border-color` **inline** rather than through a custom property: the HTML goes through
   DOMPurify, whose CSS filter is reason enough not to depend on anything exotic surviving.
3. **Media URLs** are swapped for object URLs **after** DOMPurify — its URI allow-list does not
   cover `blob:`, so an object URL embedded earlier would be stripped. See
   [Media Library](media-library.md#7-serving-the-bytes).

Clicks on wikilinks, tag chips and file links are all intercepted by the same handler.

**Videos and frames** (`lib/embeds.ts`). A YouTube or Vimeo link **alone on a line** draws as the
player (`youtube-nocookie.com` / `player.vimeo.com`, with `?t=` turned into a start time); inside a
sentence it stays a link. An `<iframe>` written by hand survives only when it points at the embed path
of YouTube, Vimeo or Google Maps over https — anything else is dropped. Every frame that survives is
re-stamped with a fixed `sandbox`, `allow`, `referrerpolicy` and `loading="lazy"`, whatever it was
written with. That hook lives on a DOMPurify instance of the module's own, so it never reaches the
other places that sanitize HTML (email).

## 9. Visual editor

TipTap 3 with `@tiptap/markdown`, which reads and writes markdown through the same `marked` the preview
uses (an instance of its own, so its tokenizers never leak into the preview). `lib/visualMarkdown.ts`
defines the schema and the markdown rules; the component only adds the views.

- **Native blocks**: paragraph, headings, lists (task and nested ones included), quote, code block,
  divider, table (column alignment kept), bold, italic, strikethrough, code and link.
- **Our own nodes**: a **wikilink** is one node (`[[target|alias]]`, `![[target]]`), drawn as the preview
  draws it and clickable — it opens the page, or creates the missing one. An **image** keeps width,
  alignment and caption in its alt text, as always; selected, it shows alignment buttons that rewrite it.
- **Raw block**: what the editor does not edit as blocks — a callout, a `<details>` (whole, across its
  blank lines) and block HTML (an `<iframe>`, a hand-written `<img>`) — is kept **as it was** and drawn by
  `MarkdownPreview` itself. A double click, or the corner button, opens its markdown; Esc, Ctrl+Enter or
  leaving the field puts it back. `/toggle` and the callouts of `/` create a raw block already open.
- **Menus** `/`, `[[` and `#` are TipTap suggestions drawn by one React menu. `[[` also offers the typed
  title when no page has it (the page is made on click, as in the preview).
- **Selection bar** (bold, italic, strikethrough, code, link), **table bar** (row, column, delete) and a
  **drag handle** to move blocks.
- **Pasting** a file sends it to the library, as in the other editor; pasting plain text (from a `.md`,
  from the markdown editor) comes in as the blocks it describes.

**Round trip.** Opening a page in the visual editor changes nothing: only an edit triggers a save. On
save, the markdown is rewritten the editor's way — `*` bullets become `-`, `_italic_` becomes `*italic*`,
`<` and `&` in text become `&lt;` and `&amp;`, stray brackets get a `\` — all of which **render the
same**. A link whose text is its own URL goes back to a bare URL (or a video link would stop drawing as
the player), and the empty paragraph the editor keeps at the end is not written. `visualMarkdown.test.ts`
pins the round trip of every construct a note can hold.

Underline stays off: markdown has none, and it would come back as `++text++`, which nothing renders.
