# Version history

[← Back to index](../README.md) · Related: [Pages & Hierarchy](pages-and-hierarchy.md), [Comments](comments.md)

Every save that changes a page preserves a complete immutable snapshot in
`notes.nte011_page_version`. Versions never expire and intermediate saves are never discarded.
Identical saves produce no version; a restore always produces a new version.

## Usage

Open **Version history** from the clock button in the page toolbar. Pending and in-flight saves must
finish first; failures preserve the draft and prevent opening history.

Versions are listed newest first in batches of 100. Consecutive manual edits separated by at most
five minutes are grouped only for display; expand a group to inspect every save. Creation, assistant
actions and restores remain separate.

Choose any two loaded versions in either direction. The body is compared line by line over its readable
text (one line per block, as the Assistant reads it);
metadata shows before/after values. Preview renders the selected version without modifying the page.
Diff computation has a one-second timeout; both versions remain available in preview if exceeded.

## Restoration and integrity

Restore reinstates title, body, icon, space, parent, position, favorite and archive state. The page's
identity and slug stay fixed. Tags, links, media uses and block stamps are rebuilt from the restored document; restored blocks are
stamped `restore`.
Moving spaces moves descendants too, recording their own versions.

The backend validates ownership, destination space and parent, and rejects hierarchy cycles. A
missing destination or parent now in a different space prevents restoration without partial writes.
The client sends its latest known version number; changes since then require reloading history.
The counter also guards simultaneous database writes.
Regaining window focus or reconnecting does not automatically advance that reference revision.
Metadata differences compare original values, including space and parent IDs, even when their
display names are identical.

Every version has a source: `initial`, `edit`, `assistant` (an accepted AI result, a capture, an
Assistant tool) or `restore` (with `restoredFrom`). `PageVersionSaveChangesInterceptor` captures page
changes and snapshots in the same transaction, including Assistant edits, subtree moves and deletion.

## Deletion and media

A page in the trash keeps its versions; deleting it for good removes them. Appearance, lock,
verification, the public link and the template mark are not versioned.

Snapshots keep media references without copying bytes. The library keeps bytes after an item is
deleted, so an old version still shows its images; any future purge of bytes must account for
historical references. Page history is not a database backup.

## API

All routes require authentication and page ownership:

| Method | Route | Response |
|---|---|---|
| GET | `/api/v1/notes/pages/{id}/versions?before={number}` | Up to 100 summaries, descending; exclusive cursor |
| GET | `/api/v1/notes/pages/{id}/versions/{number}` | Complete version snapshot |
| POST | `/api/v1/notes/pages/{id}/versions/{number}/restore` | Restored page; body `{ "expectedVersion": 18 }` |

Missing or foreign resources return 404; concurrent changes and invalid destinations return 409
(or 404 for a missing space/parent). Listing does not load historical bodies from the database.
