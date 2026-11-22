# Pages & Hierarchy

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Spaces](spaces.md), [Blocks & Editor](blocks.md)

---

## 1. Pages, folders and sub-pages

- A **page** (`nte001`) holds a [block document](blocks.md), a title, an emoji icon, a slug and its place
  in the tree.
- A **folder** (`nte012`) holds folders and pages and lives in one [space](spaces.md). It has a name,
  an icon and a description, edited in place on its own page (`/notes/f/:id`).
- A **top-level page** sits in a folder (`folder_id`) or at the top of its space. A **sub-page** sits
  under its parent page (`parent_id`) and lives in its parent's space.
- **Never a folder inside a page.**

Siblings are ordered by `order_index`; folders come before pages. The frontend nests the flat lists
from `GET /notes/pages` and `GET /notes/folders` with `lib/buildTree.ts`.

**Cycles are rejected.** `PageHierarchy.WouldCreateCycle` walks upward from the prospective parent and
reports a cycle if it reaches the moving page; folders use the same rule.

## 2. The sidebar

From top to bottom (`components/NotesSidebar.tsx`):

1. **The space switcher**, search (`Ctrl+K`) and the graph.
2. **Shortcuts:** the space **Overview**, **Today** (the daily note, with its settings gear) and
   **Recent** (the last five pages opened).
3. **The tree:** folders and pages together. A folder's `+` creates a page or a folder in it.
4. **Templates**, in their own section.

**Drag and drop** goes through one atomic request, `POST /notes/tree/move`: dropping on a folder puts
the item inside it, on a page makes a sub-page, and gaps reorder siblings. The backend checks owner,
space, destination and cycles for the whole batch before moving anything, so a failed drop moves
nothing and writes no version. Dragging is on only in one selected space with no tag filter, because
the other views do not show a complete hierarchy. "Move to another space" in the page menu uses the
single-page `POST /notes/pages/{id}/move`.

A **tag filter** turns the tree into a flat list and disables dragging.

## 3. Overview, folder page and page

- **Space overview** (`/notes/s/:spaceId`): counts, recently edited pages, and the structure as cards
  or as a tree.
- **Folder page** (`/notes/f/:id`): its content as a list, cards, or a **table**. The table has one row
  per page directly in the folder and one column per property found in their properties blocks
  (`GET /notes/folders/{id}/properties`). It is read-only.
- **Page** (`/notes/:id`): breadcrumb (space, folders, parents), header (icon, title, tags), the
  document, then its sub-pages and "Mentioned in".

## 4. Slugs

`Slugger` derives the slug from the title (lower-case, accents stripped, non-alphanumerics collapsed
into hyphens, 80 characters at most, `untitled` when nothing is left). `CreatePage` appends `-2`,
`-3`, … until it is free. The slug is **fixed at creation**; a page in the trash frees it, because the
unique index covers live pages only.

## 5. States

| State | Storage | Meaning |
|---|---|---|
| **Favorite** | `is_favorite` | A marker for the sidebar. |
| **Archived** | `archived_at` | Hidden from the default tree, still editable, still in search (flagged) and in the graph when "Show archived" is on. |
| **Template** | `is_template` | Listed under Templates; see §7. |
| **In the trash** | `deleted_at` | See §6. |

## 6. Trash

- **Deleting** a page stamps one moment on it and every sub-page, so they come back together. Deleting
  a folder sends its subfolders and pages along.
- **Restoring** (`POST /notes/trash/{id}/restore`) puts the page back under its parent, or at the top
  of its space in its folder when the parent is gone, or in the default space when its space is gone.
  A live page that took the slug meanwhile gives the restored page a numbered slug. Links, media uses
  and tags are worked out again.
- **Delete for good**, one page or the whole trash, removes the rows, their versions, comments, block
  stamps, and every link from or to them.

## 7. Templates

- Mark or unmark a page in its options menu (`PUT /notes/pages/{id}/template`). `is_template` is not
  versioned.
- **New from template** (`POST /notes/pages/from-template`) copies the content and icon into the
  chosen space and folder, with fresh ids for every block, no comment anchors, and `{{date}}`
  (the account's local date) and `{{title}}` expanded in visible text. A title may use `{{date}}`.
- Sub-pages and page settings are not copied.

## 8. Page settings

| Setting | How it works |
|---|---|
| **Cover** | A library image (moved up and down) or one of six gradients. A library cover counts as a use of the item. |
| **Width, font, small text** | `appearance.width` (`normal`, `wide`), `font` (`default`, `serif`, `mono`), `smallText`. |
| **Lock** | A locked page opens read-only on every screen; the "Locked" tag in the header unlocks it. |
| **Verification** | Verified for 7, 30, 90 or 365 days, or with no expiry. The header shows until when, and "expired" once it runs out. |
| **Recent pages** | Opening a page stamps `viewed_at` straight in the store. It is not an edit: neither "edited" nor the versions move. |
| **Public link** | A 32-byte random token. `/p/<token>` reads the page without signing in. Only the library items the page uses are served through the link, a mentioned page reads as "Private page", and live blocks show "Private content". Turning the link off and on makes a new one. |

None of these are part of a page's versions.

## 9. Daily notes

**Today** (in the sidebar and on Home) opens or creates the page of a date
(`POST /notes/daily { date? }`; omitted means today in the account's zone).

- A new daily page is titled `YYYY-MM-DD` and goes into the **daily folder**: a top-level folder called
  "Daily" ("Diário" in Portuguese) in the default space. The first time, an existing top-level folder
  with that name is adopted; its id is then stored in `nte016.daily_folder_id`, so renaming it keeps it
  in use. A missing folder is created again.
- The **daily template** (`nte016.daily_template_id`, set with the gear next to Today) fills new pages,
  with `{{date}}` and `{{title}}` expanded for the note's date. An unavailable template gives an empty
  page; existing pages are never touched.
- Which page belongs to which date is stored in `nte017`, not read from the title: renaming, moving,
  archiving or locking keeps the link. A page in the trash gets a replacement on the next visit.
- A per-user advisory lock serializes first visits, so two tabs never create two pages for one date.

## 10. Export and import

- **Export** from the page menu: Markdown, a standalone HTML file (library images inside as data
  URLs), or PDF through the browser's print dialog with a stylesheet that leaves only the page.
- **Import** a Markdown file from the `+` menu: its first heading becomes the title, and `[[Title]]`
  becomes a mention of a page that exists.

## 11. Commands and queries

| Use case | Notes |
|---|---|
| `CreatePage`, `UpdatePage` | Validate the document, then rebuild links, tags, media uses and block stamps. `UpdatePage` takes `source: edit \| assistant`. |
| `MovePage`, `MoveTreeItems` | One page (also across spaces, with its sub-pages), or a batch from the sidebar. |
| `SetPageFavorite`, `SetPageArchived`, `SetPageTemplate` | Flags. |
| `PageSettings` commands | Appearance, lock, verification, public link, viewed. |
| `DeletePage`, `Trash` commands | Trash, restore, delete for good, empty. |
| `CreatePageFromTemplate`, `OpenDailyNote` | §7 and §9. |
| `CreateFolder`, `UpdateFolder`, `MoveFolder`, `DeleteFolder` | Folders. |
| `GetPage`, `GetPageTree`, `GetFolders`, `GetFolderProperties`, `PageViews` | Reads. |
