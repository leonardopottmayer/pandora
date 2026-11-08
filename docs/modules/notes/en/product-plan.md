# Notes — Blocks revamp plan

[← Back to index](../README.md) · 🇧🇷 [Versão em português](../pt-BR/product-plan.md)

> **Status:** approved on 2026-11-07. B1 to B10 built (uncommitted). B10 includes the approved read-only folder table. B11 partially built (email-to-page, Agenda and Finances): [handoff](../../../handoff/notes-revamp.md). The other topic files describe the module
> as built before the revamp, markdown included, until each step rewrites them.

Notes stops storing markdown. A page becomes a **document of blocks**, like Notion: every paragraph,
heading, list item, image or table is a block with an id, a type, attributes and, when it holds text,
inline content. New components are new block types with new attributes, and stable block ids are what
comments, links to a block, synced blocks and (later) simultaneous editing hang on.

The navigation changes too: folders in the sidebar, an overview page per space, a table of contents
per page, tags as a block at the top of the page and page links inside the text, boxed like
Confluence's. The navigable prototype the user approved is the visual reference.

---

## 1. Decisions

| # | Decision | Chosen | Over |
|---|---|---|---|
| 1 | Storage | One `jsonb` document per page, stable id per block; links, tags, media and search text derived from it on save | One row per block |
| 2 | Editor | TipTap (already in use), one node per block type, a converter between TipTap's JSON and Pandora's block format, so the database does not depend on the editor | An editor engine of our own |
| 3 | Existing pages | None kept: the local database is dropped and recreated, migrations are edited in place | Converting markdown to blocks |
| 4 | Markdown mode | CodeMirror and the markdown preview leave; markdown comes back as export/import (B7) | Keeping a second editor and a two-way conversion |

Decisions of 2026-11-02 this reverses: cover image, table of contents, page templates and public links
are in again. A synced block shows one block in several pages; a whole page is still never drawn inside
another.

## 2. The block format

The page's `content` column (`jsonb`):

```json
{
  "version": 1,
  "blocks": [
    { "id": "b8f3k2", "type": "tags", "props": { "tags": ["dotnet", "messaging"] } },
    { "id": "c1x9qa", "type": "heading", "props": { "level": 1 }, "content": [{ "text": "Why an outbox" }] },
    { "id": "d72mpe", "type": "paragraph", "content": [
        { "text": "Related: " },
        { "mention": { "page": "0198f6a2-…" } },
        { "text": " and ", "marks": [{ "type": "bold" }] }
    ] },
    { "id": "e4a0zz", "type": "bulleted", "content": [{ "text": "Consumers are idempotent." }], "children": [
        { "id": "f0q1rr", "type": "bulleted", "content": [{ "text": "Keep the processed ids." }] }
    ] }
  ]
}
```

- **Block:** `id` (unique in the page), `type` (`[a-z][a-z0-9-]*`), optional `props`, `content` (inline)
  and `children` (blocks).
- **Inline:** `{ text, marks? }` or a mention: `{ mention: { page } }` now; dates, statuses and items of
  other modules later (B6). Marks: `bold`, `italic`, `underline`, `strike`, `code`, `link` (`href`),
  `color`, `highlight`.
- **Media:** any `props.mediaId` (and `props.items[].mediaId` in a gallery) is a use of a library item.
- **The backend checks the shape, not each type.** Size, depth, block count, unique ids and the type
  pattern are validated; what each type's props mean is the frontend's. A new block type needs no
  backend change unless it adds derived data.

## 3. Derived data, rebuilt on every save

| Data | From | Was |
|---|---|---|
| Search text (`content_text`, behind the `tsvector`) | All inline text, table cells, captions, code | The markdown |
| Page links (`nte004`) | Inline page mentions (`wikilink` kind) and page-link blocks (`embed` kind) | `[[…]]` / `![[…]]` parsed from the text |
| Tags (`nte006`) | The `tags` block's `props.tags` | `#tag` parsed from the text |
| Media use (`nte010`) | `mediaId` in any block's props | Library paths parsed from the text |

Mentions point at a page **id**, so renaming a page never breaks a link, and nothing resolves titles
any more.

## 4. Steps

| Step | Delivers |
|---|---|
| **B1 Foundation** | The block document, validation, derived data, history on blocks, the Assistant on blocks, the TipTap editor saving blocks, the markdown editor and preview removed |
| **B2 Navigation** | Folders (pages inside folders, sub-pages inside pages, never a folder inside a page), the space overview (cards and tree), the folder page, the new sidebar, the table of contents, the tags block, boxed page links and "Mentioned in" |
| **B3 Basic blocks** | Collapsible headings, toggle list, columns, callout tones, text and background colors, alignment, links with three looks (inline, card, embed), multi-block selection, link to a block, `/color` and `/turn into` commands, the `/` menu in categories |
| **B4 Media** | Gallery, video, audio, PDF reader, bookmark, embeds (YouTube, Figma, Maps, GitHub, Loom, Drive, Miro, CodePen, X/Bluesky, oEmbed) |
| **B5 Advanced** | Mermaid, math, synced block, template button, decision, breadcrumb, sub-page list, pages by tag, page properties |
| **B6 Mentions** | Date and reminder (Agenda), items of other modules, status, inline math, emoji |
| **B7 Page** | Cover, width, font, small text, lock, recents, trash with restore, verification, public link, markdown/PDF/HTML export, markdown import |
| **B8 Comments** | Comments on a block or a text range; author and dates per block |
| **B9 AI** | Ask your notes, summarize, continue, improve, translate, AI block, capture from Telegram |
| **B10 Organization** | Templates, daily notes, drag to move in the tree, a properties table in the folder |
| **B11 Integrations** | Radar item and email to page; Finances and Agenda blocks; Databases and Projects blocks once those modules exist |

## 5. What B1 built

- **Backend:** `PageContent` (Domain) validates the document and derives text, mentions, page-link
  blocks, tags and media ids in one pass. `nte001.content` is `jsonb`, `content_text` feeds the
  `tsvector`. The markdown parsers (`WikilinkParser`, `TagParser`, `MediaReferenceParser`) and the
  title lookup are gone: links point at ids. Versions keep the document and compare it as JSON, so
  Postgres' key order does not make an unchanged save a new version. The Assistant writes text as
  blocks (one per line, markdown line starts read as headings and lists) and reads a note back as text.
- **Frontend:** `lib/blocks.ts` converts between blocks and TipTap; `lib/visualSchema.ts` is the
  editor's schema with a `blockId` on every block node. Page links are boxed mentions typed with `@` or
  `[[`; an unknown block type is kept and saved back untouched; tags are edited in the header and saved
  as the first block. The markdown editor, the reading preview and the view modes are gone; a phone
  reads the same editor, read-only, until "Edit".

## 6. What B2 built

- **Folders** (`nte012_folder`): a folder holds folders and pages and lives in one space; a page sits in
  a folder (`nte001.folder_id`) only when it is top level, since a sub-page lives under its parent. The
  API is `/notes/folders` (list by space, create, update, move, delete) with the `notes.folders.*`
  permissions; creating and moving a page take a `folderId`. Deleting a folder soft-deletes its
  subfolders and every page in them with their sub-pages; a space holding folders is not empty.
  Versions keep the folder, and restoring a version whose folder is gone puts the page at the top of
  its space.
- **Sidebar:** "Overview" first, then folders and pages in one tree (folders first). The `+` of a folder
  creates a page or a folder in it; drag and drop moves pages into folders or under pages and folders
  into folders, never a folder into a page. What is open stays expanded.
- **Overview** of a space: counts, recently edited pages, and the structure as cards (each item with
  what it holds) or as a tree. **Folder page** (`/notes/f/:id`): name and description edited in
  place, its content as a list or as cards.
- **Page:** the table of contents (headings 1 to 3) beside the page on a wide screen and folded above it
  otherwise, marking the section being read; under the page, its sub-pages and "Mentioned in"; the
  breadcrumb walks folders too.

The `/` menu groups blocks the way the prototype discussion settled: **Basic**, **Media**, **Advanced**,
**Data** (Databases, Projects, Finances, Agenda), **Inline** (mentions) and **Embed**.

## 7. What B3 built

All in the frontend: the backend checks the shape only, and page-link blocks already fed the links.

| Block or prop | Stored as |
|---|---|
| Text colour, background, alignment | `props.color`, `props.background` (`gray`, `orange`, `yellow`, `green`, `blue`, `purple`, `magenta`, `red`), `props.align` (`center`, `right`) on paragraphs, headings, list items, quotes, code, callouts and toggles |
| Colour on part of the text | Marks `color` and `highlight`, `attrs.color` from the same eight |
| Collapsible heading | `heading` with `props.toggle: true` and `props.open`; the section is its `children` |
| Toggle list | `toggle` (renamed "Lista de alternância" in Portuguese) |
| Columns | `columns` whose `children` are 2 to 4 `column` blocks; a narrow screen stacks them |
| Callout tones | `props.tone`: `note`, `info`, `success`, `warning`, `error`, picked from the callout icon |
| Page link | Inline mention, or a `page-link` block with `props.pageId` and `props.look` (`preview`; a card when absent). A preview shows the first lines of the page, never the whole page |

- **Block menu:** a click on the drag handle selects the block (Shift extends the selection) and opens
  turn into, colour, alignment, copy link to block, duplicate, move to page and delete.
- **Several blocks:** Ctrl/Cmd-drag, Shift+↑/↓ or Ctrl/Cmd+A select whole blocks; a bar over them
  opens the same actions, and the drag handle moves them together. Moving to another page appends
  them there with fresh ids.
- **Link to a block:** `/notes/<page>#block-<id>` scrolls to the block and marks it for a moment.
- **`/` menu:** in categories (Basic, Media, Advanced, Data, Inline, Embed). Typing finds the colours
  (`/red`, `/blue background`; `/vermelho`, `/fundo azul` in Portuguese) and "turn into" commands,
  which act on the block the slash was typed in. Data blocks from other modules come with B11.
- **Changing a page link's look:** a card or preview offers inline, card and preview; a selected
  mention offers card and preview.

## 8. What B4 built

| Block | Stored as | Draws as |
|---|---|---|
| Gallery | `gallery`, `props.items[]` of `{ mediaId, caption? }` | A grid of library images, opening the viewer; a tile adds one more |
| Video | `video`, `props.url` | YouTube, Vimeo or Loom player, or the browser's own for a video file |
| Audio | `audio`, `props.mediaId` (+ `name`) or `props.url` | The browser's player |
| PDF | `pdf`, `props.mediaId` (+ `name`) or `props.url` | The browser's PDF reader, in the page |
| Bookmark | `bookmark`, `props.url`, `title`, `description`, `siteName`, `imageUrl`, `iconUrl` | A card; what the page said is kept in the block when it is made |
| Embed | `embed`, `props.url` | The site's frame for YouTube, Vimeo, Loom, Figma, Google Maps, Drive and Docs, Miro, CodePen and gists; for X, Bluesky and any oEmbed site, the site's HTML; otherwise a card |

- **Link preview** (`GET /notes/links/preview`): the server reads the page, refusing private addresses
  (SSRF), so a bookmark gets its title and image and an embed its oEmbed.
- **Frames:** a site's frame is sandboxed as every frame of a note; HTML from oEmbed or a gist runs in
  a frame we write, sandboxed **without** our origin, which only reports its height.
- **A library PDF** is read as `application/pdf` whatever it was uploaded as, so a file can never run
  as a page of the app.
- **Pasting a link alone on a line** asks: link, bookmark or embed. This replaces the player that
  used to appear under a lone video link.
- **Uploads:** an audio file dropped or picked becomes an audio block, a PDF a PDF block; other files
  stay file cards. Videos are still refused by the library (they need a bucket), so a video block
  takes a link.
- **`/` menu:** Media gains gallery, video, audio and PDF; Embed gains bookmark and embed.

## 9. What B5 built

| Block | Stored as | What it does |
|---|---|---|
| Mermaid | `mermaid`, `props.code` | The diagram, drawn by Mermaid (strict security, labels as SVG text, the SVG sanitized); the code is edited under it while the block is selected |
| Math | `math`, `props.latex` | The formula, drawn by KaTeX (`trust: false`) |
| Synced block | `synced` with `children`; copies are `synced-ref` with `props.pageId` and `props.blockId` | The original is edited where it lives. "Copy to sync" puts it on the clipboard; pasted into another page it becomes a copy that shows the original's blocks read-only, with a way to edit the original. A copy counts as a page link (`embed` kind) |
| Template button | `template-button`, `props.label`, the template in `children` | A click puts a copy of the template below it, with fresh ids; "Edit template" opens the body |
| Decision | `decision`, `props.status` (`decided`, `rejected`; undecided when absent), the text in `content` | A line with its status as a pill, changed from the pill |
| Breadcrumb | `breadcrumb` | The page's space, folders and parent pages |
| Sub-page list | `subpages` | The page's sub-pages, in their order |
| Pages by tag | `tag-pages`, `props.tags` (names) | The pages that carry those tags |
| Page properties | `properties`, `props.rows[]` of `{ key, value }`, each an inline list | A two-column table; its text is searched like the rest of the page |

- Mermaid and KaTeX load the first time a page shows one of them (separate chunks).
- A synced copy is read-only: editing happens in the original, which avoids two pages writing the same
  block. A report of properties across pages (Confluence's "page properties report") is left for later.

## 10. What B6 built

New inline kinds beside `text` and `mention`; the search text reads each as its words:

| Inline | Stored as | In the editor |
|---|---|---|
| Date | `{ date: { value, reminderId? } }`, `value` `YYYY-MM-DD` or `YYYY-MM-DDTHH:mm` (local) | "Today", "Tomorrow" or the date; a click changes it, adds a time, and turns its Agenda reminder on or off |
| Status | `{ status: { label, color? } }`, `color` one of the eight | A pill; a click renames it and picks its colour |
| Another module's item | `{ item: { module, kind, id, label } }` | Agenda tasks and reminders, Finances accounts and cards; the label is kept as it was when linked, and a click opens the item (its page, or the module's list) |
| Inline formula | `{ math: { latex } }` | KaTeX in the line; `$x^2$` typed in the text becomes one |

- **`@`** lists pages, then dates (`@today`, `@amanhã 14h`, `@25/12`, `@2026-12-25`) and, once
  something is typed, items of the other modules. `@remind tomorrow 9:00` (`@lembrar …`) makes the date
  with its reminder at once; a whole day rings at 9:00. The reminder is named after the line, or the
  page when the line holds nothing else.
- **`:`** followed by a word offers emoji (`:fire`, `:fogo`, `:check`) from a short list in both
  languages; the system emoji panel has the rest.
- **`/` menu, Inline:** date or reminder, status, inline formula.
- Removing a date from the text does not delete its reminder in Agenda; it stays there to be done or
  dismissed.

## 11. What B7 built

New columns on `nte001_page` (in its CREATE TABLE): `appearance` (jsonb), `is_locked`, `verified_at`,
`verified_until`, `viewed_at` and `public_token` (unique while set). None of them is part of a page's
versions.

| Feature | How it works |
|---|---|
| Cover | A library image (moved up and down) or one of six gradients, from the page menu or over the cover; a library cover counts as a use of the item |
| Width, font, small text | `appearance.width` (`normal`, `wide`), `font` (`default`, `serif`, `mono`), `smallText` |
| Lock | A locked page opens to read, on every screen; the "Locked" tag in the header unlocks it |
| Verification | Verified for 7, 30, 90 or 365 days, or with no expiry; the header shows until when, and "expired" once it runs out |
| Recent pages | Opening a page stamps `viewed_at` straight in the store (not an edit, so neither "edited" nor the versions move); the sidebar shows the last five |
| Trash | Deleting stamps one moment for the page and its sub-pages, so they come back together. Restoring puts the page under its parent, or at the top of its space (in its folder) when the parent is gone, in the default space when its space is gone, with a numbered slug when a live page took it; links, media uses and tags are worked out again. Delete for good (one, or the whole trash) removes the rows, their versions and every link from or to them |
| Public link | A 32-byte random token; `/p/<token>` reads the page without signing in. Only the library items the page uses are served through the link, a page it mentions reads as "Private page", and turning the link off and on makes a new one |
| Export | Markdown and a standalone HTML file (its library images inside as data URLs); PDF through the browser's print dialog, with a stylesheet that leaves only the page |
| Import | A Markdown file from the `+` menu becomes a page: its first heading is the title, `[[Title]]` becomes a mention of a page that exists |

## 12. What B8 built

Two new tables: `nte013_page_comment` and `nte014_block_stamp`. Neither is part of a page's versions;
deleting a page for good removes both.

| Feature | How it works |
|---|---|
| Comment on text | "Comment" in the selection bar. The thread stores the block and the quoted words; the editor marks the words with a `comment` mark holding only the thread id, so a block can carry several threads over the same words |
| Comment on a block | "Comment" in the block menu (the drag handle). An open thread on a whole block shows as a line at the block's side |
| Threads | Replies, edit, delete (a thread goes with its replies), resolve and reopen. Only a thread's first comment is resolved, and a reply cannot be answered |
| Highlight | The page paints only the marks of open threads; a resolved or deleted thread leaves the text untouched, with no editor change. Clicking commented words opens their thread |
| Comments panel | The toolbar button, with the count of open threads. Open and resolved lists; a thread whose block was removed stays, tagged "Block removed"; clicking a quote goes to the block |
| Block dates | Every save compares a fingerprint of each block (its own JSON without its children) with the stored one: a new id is a new block, a different fingerprint an edit. The block menu shows when it was written and edited, and whether the last edit came from the Assistant or a restore |

The app has one user per page, so a comment's author is always the page's owner; the block dates say
*what* wrote a change (an edit, the Assistant, a restore) instead.

## 13. What B9 has built

- AI in the selection bar: summarize, improve and translate into a supplied language. `/Continue with AI`
  uses the last 8000 characters before the cursor. Selections are also limited to 8000 characters.
- A preview shows the outgoing text before Generate, then offers replace selection, insert below or
  discard. Continue offers insert below. Generating and discarding never save anything; accepting
  requires the document to still match the preview. Generated content enters as plain text.
- `POST /notes/ai/transform` delegates to `ITextCompletion` in `Assistant.Abstractions`, through an
  authorized Assistant command reusing the user's profile, model and Integrations key. Requires
  `notes.pages.update` and `assistant.commands.run`, both modules enabled and an enabled profile.
  No tools, conversation history, persisted prompts or new tables.
- Page PUT accepts `source: edit | assistant` (default `edit`). The first autosave after accepting AI
  marks changed blocks and the version as `assistant`; subsequent manual saves return to `edit`.
  Attribution is per save: manual changes grouped into that autosave share its source.
- Provider failures, timeouts, empty responses or responses over 16000 characters become friendly
  errors without provider details. Closing the preview cancels the browser request.

- Whole-page summary: the editable page's options menu offers “Summarize page with AI”. Pending edits
  are flushed before opening the preview. `POST /notes/pages/{id}/ai/summary` reads only the owner's
  saved page and summarizes its derived text, including table cells, properties and chips. Empty text
  or more than 32000 characters is rejected without calling the provider; nothing is silently truncated.
  Accepting inserts a new “Summary” info callout at the top, preserving the existing blocks and using
  the same Assistant-origin autosave. Discarding leaves the document untouched. A changed document
  cannot accept a stale preview. Only this page's saved text is sent; linked pages are not read.

- Ask your notes: a tab beside page search accepts a question of up to 1000 characters and searches
  the current space or all spaces. Keyword retrieval removes common Portuguese/English question words
  and uses alternatives over the existing tsvector index; ordinary page search is unchanged. At most
  five saved pages (archived included, deleted excluded) contribute passages of 4000 characters plus
  truncation indicators, centered on a matching word. Titles and passages go to the user's Assistant.
  There are no embeddings, semantic/synonym matching, tools, conversation history or page writes.
- The model returns an answer and cited page ids. The backend rejects malformed answers, uncited
  assertions and citations outside the consulted pages. No hits or an explicit insufficient-evidence
  response shows a localized no-information message. This validates citation identity, not the truth
  of every assertion; the UI displays plain text and source passages with buttons to open the pages.
  Changing the question/scope clears the answer; closing or switching away cancels the browser request.

- AI block: `/AI block` inserts a `richBlock` of kind `ai`, stored as `{ prompt, output, generatedAt }`
  in its props. `output` is a plain-text inline array, so the existing derived-text walk indexes it.
  `POST /notes/ai/generate { prompt }` sends only the prompt (1–8000 characters) through the configured
  Assistant and returns plain text with the server's UTC completion time. No tools, page context or
  automatic generation on opening. The prior accepted output stays while the new result is previewed;
  accept replaces it in one Assistant-origin transaction, discard leaves it intact. Prompt edits are
  ordinary edits and clear an obsolete preview. Acceptance checks that the targeted block is unchanged.
  Read-only/public views show the stored prompt and result without generation controls. Both are part
  of the shared page; Markdown/HTML/PDF exports and readable text include the accepted result, while
  version snapshots retain all props and text diffs include prompt/output. No new schema or synchronizer.

- Telegram capture: `/nota <text>` on the linked Assistant bot appends one literal paragraph to a
  dedicated Capture page, created in the default space if needed. Each paragraph prefixes the inbound
  receipt date/time in the account's effective zone. It bypasses Gemini, needs no enabled AI profile
  or API key, but requires Assistant run and Notes read/create/update/space-read permissions/features.
  Renaming/moving/archiving the inbox keeps its identity; a deleted inbox is replaced without restoring
  it. An unrelated page with the same title is never reused. A locked inbox refuses the capture.
- `nte015_note_capture` stores inbound event id, owner, page id and processing time atomically with the
  page/history/derived data. Re-delivery does not append or create another version, even after the
  page or a captured paragraph is removed. Receipts retain no text and survive permanent page deletion.
  The existing serial Channels outbox relay delivers captures one at a time. A retried acknowledgment
  may repeat; the captured content does not. Text-only captures preserve newlines and Markdown literally;
  media and ordinary natural language keep their existing paths. No new HTTP endpoint.

**B9 implementation complete.** Live browser/provider/Telegram verification and user review remain pending.
**Visual and real-provider validation:** pending; uncommitted.

## B10: page templates

Built, uncommitted. Mark/unmark a page in its options menu, browse Templates in the sidebar and
create from any owned active template into the current space/folder. Copies retain content and icon,
receive fresh IDs for all nested blocks, remove comment anchors and expand `{{date}}` (account-local
ISO date) and `{{title}}` (final title) in visible text. Titles accept `{{date}}`. Subpages and page
settings are not copied; `is_template` is not versioned. The folder properties table is built below; tree dragging is reviewed below. The original page CREATE
gains `is_template`; the local database was not recreated or migrated. Browser validation is
pending.

## B10: daily notes

Built, uncommitted. Today in the Notes sidebar and Home opens or creates the account's daily page.
`POST /notes/daily { date? }` accepts a calendar date; omitted means today in the account's
effective time zone. New pages are titled YYYY-MM-DD, inside Daily (Diário in Portuguese) in the
default space. An existing root folder with that name is reused initially; its stored identity
survives renames. Missing/deleted folders are replaced.

Daily-note identity is stored separately from page title/location in `nte017_daily_note`, unique by
user and date. Rename/move/archive/lock keep the same page; reopening does not edit it or create a
version. Deleted/purged pages get a replacement on the next visit; restoring an old page does not
replace the current daily entry. Ordinary pages with the same date title are not reused. Slug
conflicts receive the normal numeric suffix.

`nte016_notes_preferences` stores the optional daily template and folder id within Notes. Configure
the model through the sidebar's Daily notes settings button. Saving a model requires an owned,
active marked template; null clears it. Only new pages apply the model: fresh nested IDs, removed
comment anchors, icon/content copied, date/title variables expanded using the note's selected date.
An unavailable model falls back to an empty page. Existing entries remain untouched. New pages get
normal history/search/tags/links/media/block stamps.

A PostgreSQL per-user session advisory lock spans reads through the atomic unit-of-work commit,
serializing first visits and preference changes across instances. It is released explicitly on the
same open connection. Database uniqueness also enforces one registry row per user/date. EF
relationships to page links and page tags were declared so pages created with their folder are
inserted before these derived rows.

Permissions: opening requires notes.pages.read/create, notes.folders.create and notes.spaces.read.
Preferences use notes.preferences.read/update; selecting a model also requires notes.pages.read.
These belong to the Notes feature. New migrations create nte016 and nte017; no local database was
recreated or migrated. Browser verification is pending.

## B10: tree drag review

Built, uncommitted. Sidebar drops now move/reorder pages and folders through one atomic `POST
/notes/tree/move` request. Folders stay before pages; dropping on a folder puts the item inside it,
on a page creates a subpage, and gaps reorder siblings. Cycles and folders inside pages are
rejected. Only the main tree's visible siblings are renumbered; templates keep their separate
section and are not dragged or implicitly reordered.

Dragging is enabled in one selected space without tag filters, according to each item's update
permission. All-spaces and tag-filtered views do not represent a complete hierarchy to reorder. The
drop remains disabled until the write and hierarchy refetch finish; target containers expand after a
successful move. Invalid targets are blocked before dropping. Errors refresh the hierarchy and allow
a retry. The existing move-to-another-space menu keeps using the single-page endpoint.

The backend validates owner, selected space, destination, distinct IDs and cycle rules for the
entire batch before updating tracked aggregates. It uses the existing Page.Move/Folder.Move behavior
and one unit-of-work commit, preserving page history. A per-user lock serializes batches; failed
batches do not partially move siblings or create versions. The implementation intentionally does not
dispatch individual movement commands because independent nested writes would not provide the
atomicity this endpoint promises. No new schema or migration. Native pointer/touch dragging remains
unverified in the browser; automated UI tests cover the Tree event contract and API wiring.

## B10: read-only folder properties table

Built, uncommitted. A folder now offers Table alongside its existing List/Cards views. Existing
defaults are preserved; selecting Table uses the same local view preference. Properties load only
while Table is selected. Each direct page gets one row, including pages without properties;
subfolders and subpages are excluded. Archive and intersecting tag filters match the Notes view.
Clicking the title opens the page; cells are read-only and missing/empty values display an em dash.

`GET /notes/folders/{id}/properties` checks folder ownership and both folder/page read permissions.
It reads only the folder's direct, nondeleted pages and returns summaries plus a union of property
names and plain-text values, without full documents. `PageContent.Properties()` walks properties
blocks at any nesting depth, trims keys/values, ignores unnamed rows and reads
text/status/date/item/math inlines. Case variants share the first encountered column label; repeated
keys within one page retain all values separated by newlines. Columns follow first encounter in page
order, and pages follow order_index/id. Existing content, versions and derived data are not written.

The table renders all strings as text, scrolls horizontally when needed and shows explicit
empty/error states. Folder changes clear old results. Projection keys live under the page query
family, so create/move/archive/delete invalidate them; autosave also refreshes the properties
projections explicitly. No new table, column or migration. Browser validation and applying earlier
migrations to the local database remain pending.

B10 implementation is complete for the approved read-only scope. Inline cell editing is not
included. B11 integrations are next; live browser review remains pending.

## B11: email to Notes

Built, uncommitted. Save to Notes in the message reader opens a modal for a title, active space and
existing folder (including nested folders), or the space root. The subject suggests the title,
bounded to 255 characters. Choosing another space clears the folder selection. Saving opens the new
page. Every explicit save creates an independent copy; subsequent email changes do not update the
note. Attachments, inline images and HTML formatting are not copied.

`POST /communications/messages/{id}/notes` reads the authenticated owner's message through the
existing OpenMessage flow, including its cached/provider body handling. The client supplies only
title/destination, never the message body. Plain-text MIME content is preferred. HTML-only mail is
converted using MimeKit's tokenizer: entities decoded, paragraph/list/line separation retained,
scripts/styles/head/template/noscript/comments discarded, no HTML execution or remote-resource
loading. The copied body is literal paragraph text; markdown-looking lines do not become commands or
tags.

Communications calls `INoteImporter` in Notes.Abstractions. Its Notes implementation dispatches
ImportNoteCommand and normal CreatePageCommand, preserving destination ownership checks, search,
initial history and block stamps. No cross-module Application/Domain/Persistence reference,
cross-module table access or new integration event. A synchronous port returns the committed page id
needed by the UI. Invalid titles/content or unavailable destinations create no partial page. The
normal page content size/block limits apply.

The source is a bookmark to `/mail?message={id}`. MailPage now keeps the selected message in that
URL and can load it outside the current inbox/list. Missing/foreign messages show an error; the
phone's back action returns to the list. The copied note remains independent if the original
disappears. The save endpoint does not send mail, change provider flags or import attachment bytes;
normal body-cache access/retention still occurs.

Requires communications.messages.read and notes.pages.read/create, notes.spaces.read and
notes.folders.read, with both modules enabled. The action is hidden without Notes access and
disabled until the current mail body is loaded. Destination-query errors block saving. Pending saves
block duplicate submission/cancel; failures keep the title/destination for retry. Notes query
families are invalidated after success. No schema or migration change. Browser/live-provider
validation remains pending; local database preserved. B11 live Agenda/Finances blocks are built; Radar/Databases/Projects depend on those modules.

## B11: live Agenda block

Built, uncommitted. `/Agenda` inserts an agenda-list rich block. Source defaults to Today, reading
the existing account-local unified day endpoint (events, tasks and reminders, including their
current statuses). The other mode selects an active task list and shows only Todo/InProgress tasks
in that list. Both modes are read-only: no completion, acknowledgment or event edits inside Notes.
Open in Agenda navigates to the relevant Agenda screen.

Saved configuration is only `{ mode: "today" }` or `{ mode: "tasks", listId }`; empty props default
to today. Live titles, list names, statuses, dates and results never enter the block, autosave,
search text or version snapshots. Changing mode clears the prior list. Unconfigured, deleted,
foreign or archived lists show an explicit unavailable/choose-list state and do not fall back to
loading all tasks. The list id is validated against the authenticated user's current active lists
before mounting the tasks query.

Queries reuse the existing Agenda hooks, services and query-key families. Normal Agenda mutations
refresh active blocks through the same cache; Refresh explicitly invalidates the selected source.
Standard cache staleness/focus behavior still applies; this is not polling or a push subscription.
Reads/refreshes do not update the note. Displayed dates use the account time zone and existing
Agenda formatters; deciding which day is today remains server-side. API failures hide stale rows and
allow retry.

Public view returns Private content before mounting any Agenda query, even with owner data already
cached. Private notes require the Agenda feature and the source's permissions: Today requires
agenda.events.read/tasks.read/reminders.read; list mode requires agenda.tasks.read/lists.read.
Read-mode/locked notes can view and refresh but cannot change source/list. Markdown/HTML exports
emit only [Agenda], without source ids or live values; browser PDF printing uses the currently
rendered owner view. Block ids and configuration round-trip through the editor and versions.

No backend endpoint, schema, migration or cross-module backend dependency change. Browser/live
account validation is pending. Upcoming-event period/calendar configuration is now built; see implementation status. Radar/Databases/Projects blocks depend on
those modules.

## B11 remaining scope

The available-module scope is built: email capture, Agenda Today/tasks/upcoming events (7/30 days
and calendar selection), and Finance summaries. Remaining integrations depend on
Radar/Databases/Projects being implemented. Next is browser/live-account validation, checking
pending local migrations without recreating the database, and fixing issues found in that review.
Commits require an explicit user request. Fixed custom event dates are outside the approved
rolling-window slice.
