# Editor & Rich Blocks

[← Back to index](../README.md) · Related: [Pages & Hierarchy](pages-and-hierarchy.md), [Attachments](attachments.md), [Tags](tags.md)

The editor is **100% frontend**. Every block it produces is ordinary markdown, which
`content_markdown` already stored — nothing here required a backend change.

---

## 1. The surface

`components/MarkdownEditor.tsx` is CodeMirror 6 over the raw markdown; `components/MarkdownPreview.tsx`
renders it with `marked` + DOMPurify. `NotesPage` shows them in one of three view modes — `edit`,
`split` (the default) and `preview`.

The editor stays **raw markdown**: there is no live widget rendering inside CodeMirror. The rendered
result is what the preview pane shows. See [Implementation Status](implementation-status.md).

## 2. Autosave

`hooks/useAutosave.ts` debounces at **800 ms** and saves through a TanStack Query mutation, exposing a
status the page displays. The draft (`PageDraft`) holds title, icon and body together, so all three
save through the same path — the icon is not a separate call.

`hooks/useDebouncedValue.ts` (200 ms) is the other debounce, used by the search palette: one request
per pause in typing, not per keystroke.

## 3. Inline upload

Pasting (`Ctrl+V`) or dropping a file uploads it to `POST /notes/attachments` and inserts
`![name](/api/v1/notes/attachments/{id})` at the cursor. The label is the original filename, which is
also what the download handler later uses to name the saved file. See
[Attachments](attachments.md).

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
| `[[` | `wikilinkTriggerAt` in `lib/wikilinks.ts` | Opens after `[[` and after `![[`, which writes the target the same way. **Refuses** after a `]` or a `\|` — a closed link or half an alias is no longer the target being typed. `filterPages` matches **title and slug**, so `cafe` finds `Café com Pão`; capped at 10 options. On apply, an existing `]]` right after the cursor is **reused** rather than duplicated — which is exactly the state the `/wikilink` command leaves behind. |
| `#` | `lib/tags.ts` | Completes **existing tags only** — a new tag is simply typed, which is the whole point of it living in the text. |
| `/` | `slashTriggerAt` in `lib/slashCommands.ts` | Only opens with the `/` at the start of a line or after whitespace (so `src/lib` mid-sentence never fires), and closes at the first space typed. `filterCommands` matches by **id and translated label**. |

## 5. Slash commands

`SLASH_COMMANDS` inserts plain markdown — every entry is something the user could have typed by hand,
which is what keeps the document round-trippable.

| Group | Entries |
|---|---|
| Blocks | `h1`, `h2`, `h3`, bullet list, numbered list, todo (`- [ ] `), quote, code block, divider, table, wikilink (`[[]]` with the cursor in the middle) |
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

**Collapsible callouts** (`> [!note]-`) are out of scope. If they land, the place is `callouts.ts`.

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
3. **Attachment URLs** are swapped for object URLs **after** DOMPurify — its URI allow-list does not
   cover `blob:`, so an object URL embedded earlier would be stripped. See
   [Attachments](attachments.md#4-the-consequence-the-browser-cannot-fetch-an-attachment-by-itself).

Clicks on wikilinks, tag chips and attachment links are all intercepted by the same handler.
