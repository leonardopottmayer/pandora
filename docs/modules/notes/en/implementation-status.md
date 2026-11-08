# Implementation Status

[← Back to index](../README.md)

## Blocks revamp — 2026-11-07 update

B1–B10 are built and uncommitted. The first B9 slice adds summarize/improve/translate selection and
continue through `/`, with a preview before applying, Assistant configuration and `assistant` origin
on the accepted autosave. See [revamp plan](product-plan.md). B10 is built, including the read-only folder table; B11 email-to-page, Finance summaries and Agenda Today/tasks/upcoming-events blocks are built below.
Browser and real-provider validation are pending. Historical sections below describe the previous
Markdown implementation until their documentation is updated.

The second B9 slice adds whole-page summaries in the editable page's options menu: flush pending
edits, generate a preview, and accept it as a new Summary callout at the top. The owner-scoped endpoint
reads saved text (including tables/properties/chips), with an explicit 32000-character limit and no
page writes until acceptance.

The third B9 slice adds “Ask your notes” beside page search, optionally restricted to a space.
Retrieves at most five owner-scoped saved pages using keyword alternatives, sends bounded passages
to the configured Assistant and accepts only citations from those pages. Plain-text answers show
clickable sources and passages; no hits/insufficient evidence shows an explicit message. No note or
conversation writes. Lexical retrieval has no synonym/semantic matching. AI blocks and Telegram
capture are described below. Browser and real-provider validation remain pending.

The fourth B9 slice adds `/AI block`: a saved prompt, accepted plain-text output and server UTC
generation time. Generate/regenerate uses the existing Assistant profile, previews before acceptance,
keeps old output on discard/error and marks acceptance as Assistant-origin. Output is included in
search, readable text, exports and version diffs. Public/read-only views display saved content only;
the prompt is part of the shared page too.

The final B9 slice adds direct `/nota <text>` capture on the linked Assistant Telegram bot, without
AI/profile/key. It creates/reuses a dedicated Capture page, stamps the account-zone receipt time,
keeps text literal and writes history/search/block stamps with Assistant origin. Event receipts in
`nte015_note_capture` prevent duplicate saves even after page purge. Locked pages refuse capture;
deleted inboxes are replaced. Requires the normal Notes/Assistant permissions and features. B9 is
built, uncommitted; live Telegram, browser and provider checks remain pending. B10 is built below; B11 is next.

A snapshot of what is built in the codebase versus what is designed but not yet implemented. Use it
to tell the difference between "documented because it exists" and "documented as a plan".

The module was built in nine sequential phases (scaffold → pages → attachments → editor → wikilinks →
search → graph → rich blocks → tags), all of which are **closed**. What follows is the state that
left behind, not the phase log.

---

## Implemented

| Area | Notes |
|---|---|
| **Frontend review** | Spaces, the library, media picker, graph and editor show query failures instead of empty results. An unavailable page leaves the loading state and shows an explicit unavailable message. |
| **Module scaffold** | All layered projects, `notes` schema, registered in the Host. No jobs, no integration events, no audit event log. |
| **Pages** | `Page` aggregate, tree with cycle rejection on reparent, frozen slugs with collision suffixes, move/reorder, favorite, archive, soft delete of the whole subtree (`nte001`). |
| **Spaces** | `Space` aggregate, one default per user (created on the fly when none), pages placed in a space, moving across spaces with the subtree, wikilinks preferring the own space, space filters on tree / search / graph, spaces screen and sidebar switcher, `create_note` taking a space (`nte007`). |
| **Emoji icon** | Picker next to the title, reduced to the first grapheme via `Intl.Segmenter`, saved through the page's own autosave. |
| **Media library** | Items (image / file; video in the schema, refused on upload) in virtual folders, WebP thumbnails with ImageSharp and EXIF rotation, folder moves without cycles, a deleted folder handing its contents up, soft delete keeping the bytes served, immutable caching, the library screen, paste/drop in the editor into "Unsorted" (`nte002`, `nte003`, `nte008`, `nte009`). |
| **Media inside notes** | Library picker from `/image`, `/file` and the toolbar, width, alignment and caption from the alt text (`![text|400|right]`), library `<img>` in HTML, full-screen zoom, file cards with size, which notes use each item derived on save (`nte010`), "Used in" in the library, an "Unused" place, the space taken (total and unused), a warning before deleting an item in use. |
| **Editor** | A Notion-style visual editor (TipTap 3, saving markdown: `/` `[[` `#` menus, selection bar, table bar, drag handle, native callouts/toggles with editable bodies and titles, the player under a video link, library file cards, inline HTML kept; unsupported HTML kept as raw blocks) as the default; CodeMirror 6 with raw markdown, preview and split mode beside it; 800 ms autosave, DOMPurify sanitization. |
| **Rich blocks** | Slash commands (14 blocks + 6 callouts), Obsidian callouts as a `marked` extension (collapsible with `-`/`+`), toggles (`<details>`), YouTube/Vimeo players from a bare link and an iframe allowlist (YouTube, Vimeo, Google Maps) with a fixed sandbox, assisted markdown tables with Tab/Shift+Tab and idempotent reformatting. |
| **Autocomplete** | Three menus — `[[` (pages by title and slug), `#` (existing tags), `/` (commands) — sharing `filter: false`, ref-based inputs and a body-parented tooltip. |
| **Wikilinks & backlinks** | `WikilinkParser`, title-then-slug resolution, `wikilink`/`embed` kinds, diff-based edge rebuild on create and update, backlinks endpoint and panel, create-on-click (`nte004`). |
| **Search** | Generated `tsvector` + GIN, `simple` configuration, safe prefix `tsquery`, title priority and `ts_rank_cd` relevance with stable ties, filters before the 20-result cap, 160-char plain-text excerpt with safe title/excerpt highlighting, Ctrl+K palette and sidebar button. |
| **Graph** | Global and local graph from one query, in-memory undirected BFS with depth clamped 1..5, backend-computed degree, broken edges filtered out, `react-force-graph-2d` canvas with hover highlighting and zoom-gated labels. |
| **Tags** | `TagParser` with code stripping, `TagName` normalization, tag creation from the text, diff-based edges, orphan sweep preserving colored tags, hex color validation, intersecting filters on the sidebar / search / graph, chips in the preview (`nte005`, `nte006`). |
| **Graph families** | Named/color-coded regions by chosen tags, ordered priority without duplicate pages, counts and family framing. Global graph switch off by default; mode and configuration remembered per account/browser. See [Graph View](graph-view.md#6-tag-families-and-thematic-regions). |
| **Collapsed graph families** | Per-visit collapse/expand controls replace members with a summary node and aggregate external links by direction/kind. Search reveals hidden pages automatically; the original graph is preserved. See [Collapse families](graph-view.md#10-collapse-and-expand-families). |
| **Graph camera controls** | Accessible zoom in/out and fit-all buttons in global/local graphs, including mobile; manual framing takes priority when the simulation settles. See [Camera controls](graph-view.md#11-camera-controls). |
| **Graph visibility** | Show archived now controls both the sidebar and global graph (off by default); Hide isolated removes nodes without remaining links. Search, details, family counts and framing use the same filtered projection, with recalculated degrees. Per-visit choices; no API change. See [Visibility filters](graph-view.md#5-visibility-filters). |
| **Graph inspection** | Title lookup and node selection focus the neighborhood and show a responsive details panel with tags, families and distinct connected notes. An explicit Open note action navigates to the editor. See [Locate a note](graph-view.md#7-locate-a-note) and [Inspect a note](graph-view.md#9-inspect-a-note-without-leaving-the-graph). |
| **Frontend** | React module (`client-web/src/modules/notes`) covering all of the above, i18n EN + PT-BR, unit tests over every `lib/` module. |
| **Tests** | Domain unit tests (`Modules.Notes.Tests`: page, hierarchy, graph, search, slugger, tag name, tag parser, wikilink parser) + integration tests (`IntegrationTests/Modules/Notes`: pages, attachments, backlinks, search, graph, tags). |

## Not yet implemented (designed / planned)

| Area | Status |
|---|---|
| **Tag filters in the URL** | Not implemented. Filters are component state, so "the pages with #x" is not a shareable link. The place would be a search param on `NotesPage`. |
| **Graph navigation ideas** | A bridge overview between families and later layout persistence/automatic communities. See [Graph ideas](graph-view.md#8-improvement-ideas-2026-11-02). |
| **Column alignment from the UI** | Deliberately out. The place would be the separator line in `lib/markdownTables.ts`. |
| **Bucket storage backend and video** | Designed for, not scheduled: not in the current queue. Only the abstraction exists. `storage_backend` + `storage_key` on each media row mean plugging a bucket in needs no migration; video also needs ranged downloads and a poster rendition. See [Media Library](media-library.md#3-kinds-and-what-is-ready-for-video). |
| **Version history** | Implemented — see [Version history](version-history.md). |

## Deliberately out (not debt)

- **Rendering `![[embeds]]` inline** — decided against on 2026-11-02: a page is never drawn inside
  another. `![[x]]` stays a link; the `embed` edge kind still exists (backlinks, dashed in the graph).
- **Cover image, table of contents, page templates and public links/sharing** — out for now (decided
  on 2026-11-02). Dropped from the revamp list; they come back only if the user asks.
- **Live preview inside CodeMirror** — superseded by the visual editor; CodeMirror stays as the raw
  Markdown mode.

- **Global tag rename** — it would mean rewriting the markdown of every page mentioning the tag; that
  is find & replace, not a database operation.
- **Nested-tag rollup** — `#projeto/pandora` is accepted as the tag's text, but `#projeto` does not
  include its children.
- **Tag create/delete endpoints** — the text creates a tag, the sweep removes it.
- **`ON DELETE CASCADE` on the page tree** — the delete command walks the subtree itself so the soft
  delete keeps its history.

## Version history

Implemented: complete immutable snapshots for every changed save, retained indefinitely.
Display-only grouping, arbitrary-pair Markdown and metadata comparison, preview and reversible
restore with ownership, hierarchy and concurrency validation. Existing pages receive an initial
snapshot; soft deletion retains history. See [Version history](version-history.md).

## Known open points

1. **Blob retention.** Media bytes live in `bytea`; revisit moving to object storage if the
   notebook starts carrying heavy files — that is also the trigger for the S3 backend.
2. **Unused bytes.** Deleting an item keeps its bytes, since notes may still show it; nothing sweeps the
   bytes of a deleted item that no note uses anymore. It stays that way by decision (2026-11-02): the
   library's "Unused" place already lists those items, and deleting them is the user's call.
3. **In-memory graph walk.** Loading the user's whole page/edge set is right for a personal notebook;
   it is the first thing to revisit if the graph ever gets big.
4. **Duplicated parsers.** `lib/wikilinks.ts` and `lib/tags.ts` mirror the backend on purpose, so the
   preview resolves what the save will resolve. Any change to a parsing rule has to land on both
   sides — the unit tests on each side are the guard.

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
account validation is pending. The upcoming-events mode is now built below. Radar/Databases/Projects blocks depend on
those modules.

## B11: live Finance summaries

Built, uncommitted. /Finances inserts a finance-summary rich block. Account mode shows current
posted/projected balances; card mode shows total/available limits, distinguishing zero from an
undefined limit. Archived, deleted or foreign sources show an explicit unavailable state. The
selected id must belong to the current active account/card list before its figures are queried. No
source is selected automatically.

Monthly expenses aggregate every page of kind=expense, status=posted transactions by transaction
date (from/to inclusive). Totals are gross: refunds are not deducted; pending/void entries,
income, transfers and statement payments are excluded. Currencies stay separate. Categories are
grouped by identity, including historical inactive names; unknown categories and uncategorized
expenses remain distinct. Any transaction/category query error hides the result instead of
displaying a partial total.

Only configuration is saved: { mode: "account", accountId }, { mode: "card", cardId }, or { mode:
"expenses", month?: "YYYY-MM" }. Empty props default to account mode. Omitting month resolves the
current month in the account time zone; a fixed month remains calendar dates, including leap
years. Invalid months do not issue monetary queries. Switching mode clears prior source/month
configuration. Names, balances and totals are never persisted, autosaved or copied into
versions/templates.

The private frontend reuses Finances hooks/services/query keys. Normal Finance mutations
invalidate active summaries; Refresh explicitly reloads the selected source. Normal cache/focus
behavior applies without polling. Reading/refreshing does not change the note. Locked/read-mode
notes can read and refresh but cannot change configuration.

The Finances feature and source permissions are required: finances.accounts.read,
finances.cards.read, or both finances.transactions.read and finances.categories.read. Public
rendering returns Private content before mounting any source query, even with populated owner
caches. Markdown/HTML emit only [Finances]; browser PDF uses the current rendered owner view. No
new backend route, dependency, schema or migration. Browser/live-account validation remains
pending.

Verification: 893 frontend tests passed (34 added for Finance aggregation, month boundaries,
pagination, source access, public rendering, configuration, refresh and mutation invalidation).
Production build/typecheck, Notes ESLint and antd lint passed. Backend unchanged; its suites were
not rerun. Browser/live-account validation remains pending. No commit or local database change.

## B11: upcoming Agenda events

Built, uncommitted. The existing /Agenda block now offers Upcoming events. Configuration is {
mode: "events", days: 7 | 30, calendarId?: string }; missing/unsupported days defaults to 7.
Missing calendarId means all active calendars, independently of the Calendar screen visibility
toggles. A configured calendar must belong to the current active owner list.
Missing/archived/foreign calendars show an explicit unavailable state and never fall back to all
calendars. An empty active list issues no event query.

The window covers today plus the next 6 or 29 calendar dates in the account time zone. Both local
midnights are converted independently to UTC, preserving DST changes. The exclusive end is the
next midnight after the last date. Ongoing and future events are displayed in start order;
ended/cancelled events and occurrences starting at/after the exclusive end are hidden.
All-day/multi-day ranges use the existing Agenda formatter, including the exclusive midnight end.
The window is resolved on rendering/reopening and Refresh; no background polling or midnight timer
is added.

Reads reuse useCalendars/useEventOccurrences and Agenda query keys. The existing events endpoint
expands recurrence rules and applies occurrence edits/cancellations; Notes does not implement
another recurrence engine or request series details. Calendar filters serialize as repeated
calendarIds keys for ASP.NET query binding. Normal event/calendar mutations invalidate active
blocks; Refresh reloads both families. Query failures hide stale results and allow retry.

Requires the Agenda feature and both agenda.events.read and agenda.calendars.read.
Locked/read-mode notes can view and refresh but cannot configure. Changing source clears old
filters. Only days/calendar identity are persisted; names, occurrences and resolved dates never
enter autosave, history or templates. Public pages guard before source queries, even with owner
caches. Markdown/HTML still emit [Agenda]; browser PDF uses the rendered owner view. No backend,
schema or migration change.

Verification: 909 frontend tests passed, including 16 added cases for account-day/DST bounds,
expanded occurrences, ongoing/all-day events, exclusive end, source access/privacy, configuration
and mutation invalidation. Production build/typecheck, Notes and touched Agenda service ESLint,
and Notes antd lint passed. Backend unchanged; backend suites were not rerun. Browser/live-account
validation remains pending. No commit or local database change.
