# Spaces

[← Back to index](../README.md) · Related: [Pages & Hierarchy](pages-and-hierarchy.md), [Data Model](data-model.md), [API Reference](api-reference.md)

---

## 1. What a space is

A **space** groups pages the way a Confluence space does: "Personal", "Work", "Studies". Every page
belongs to exactly one (`nte001_page.space_id`), and the sidebar shows one space's tree at a time.
Beyond that a space is only organization — it **isolates nothing**:

- **Tags** belong to the user, not to the space. `#pandora` gathers pages from any space; to see one
  space only, use the space filter of search or the graph.
- **`[[Wikilinks]]`** reach any space (see §4).
- **Search and graph** start in the space on screen and can widen to all of them.

## 2. The default space

Each user has at most one space with `is_default` (partial unique index `uq_nte007_user_default`).
A page created with no space goes there — from the assistant, or from an API call that named none.
The first space a user creates becomes the default; a user with no space at all who creates a page
without one gets a default called `Personal` (`SpaceResolver.FallbackName`) — the web app always
names the space, so that path is API-only.

The default **can be neither archived nor deleted** (`Spaces.IsDefault`, 409). Making another space
the default (`POST /spaces/{id}/default`) takes the mark off the previous one in a transaction of its
own, before putting it on the new one, so the unique index never sees two — the same pattern as
Agenda's default calendar. An archived space made the default comes back from the archive.

## 3. Pages and spaces

- **A child lives in its parent's space**, whatever space the `POST /pages` asked for. The tree never
  crosses spaces.
- **Moving to another space takes the subtree.** `POST /pages/{id}/move` with `parentId: null` and a
  `spaceId` puts the page at the top of that space; moving it under a page of another space changes
  its space too. Either way `MovePageCommandHandler` walks the subtree (`PageHierarchy.Subtree`) and
  moves all of it.
- **Deleting a space** is only accepted once it is empty — no live page, archived ones included
  (`Spaces.NotEmpty`, 409). The delete is soft, and the `fk_nte001_space` foreign key makes sure no
  page points at a space that does not exist.

## 4. Wikilinks across spaces

`PageLinkSynchronizer` still resolves title before slug, but among pages sharing a title, **the one
in the linking page's own space wins**. Two spaces may each hold their own "Meeting", and `[[Meeting]]`
points at the one next to it; a title that only exists in another space still resolves there. The
frontend's `buildPageIndex` follows the same order, so the preview resolves what the save will.

## 5. Commands and queries

| Use case | Type | Notes |
|---|---|---|
| `CreateSpace` | Command | The user's first one becomes the default. |
| `UpdateSpace` | Command | Name, emoji icon, hex color (`#rrggbb`) and description. |
| `SetDefaultSpace` | Command | Two transactions, see §2. |
| `SetSpaceArchived` | Command | Refuses the default. |
| `DeleteSpace` | Command | Refuses the default and a space with pages. |
| `GetSpaces` | Query | The default first, then by name, each with `PageCount` and `LastEditedAt`. |

## 6. In the frontend

- **`/notes`** (`pages/SpacesPage.tsx`) lists the spaces as cards: icon, name, description, how many
  pages and when one was last edited. Each card's menu edits, makes default, archives and deletes —
  the default offers neither archive nor delete, and a space with pages shows "Delete" disabled.
- **`/notes/s/:spaceId`** opens the space: the sidebar with its tree and, where a page would be, the
  space overview (`components/SpaceOverview.tsx`) listing its top-level pages.
- **`/notes/:id`** opens a page; the space comes from the page itself.
- **The switcher at the top of the sidebar** changes space or goes back to all of them. Each page's
  menu gained "Move to space".
- **Search (Ctrl+K)** starts in the space on screen and has "This space / All spaces"; with all,
  each hit shows its space. **The graph** has a space picker.
- **The assistant** takes `space` in `create_note` ("note in the Work space…"): the name matched
  ignoring case, then by fragment, listing the spaces there are when none matches.
