# Version history

Every save that changes a page preserves a complete immutable snapshot in
`notes.nte011_page_version`. Versions never expire and intermediate saves are never discarded.
Identical saves produce no version; a restore always produces a new version.

## Usage

Open **Version history** from the clock button in the page toolbar. Pending and in-flight saves must
finish first; failures preserve the draft and prevent opening history.

Versions are listed newest first in batches of 100. Consecutive manual edits separated by at most
five minutes are grouped only for display; expand a group to inspect every save. Creation, assistant
actions and restores remain separate.

Choose any two loaded versions in either direction. The body uses a line-based Markdown diff;
metadata shows before/after values. Preview renders the selected version without modifying the page.
Diff computation has a one-second timeout; both versions remain available in preview if exceeded.

## Restoration and integrity

Restore reinstates title, body, icon, space, parent, position, favorite and archive state. The page's
identity and slug stay fixed. Tags, wikilinks and media usage are rebuilt from the restored body.
Moving spaces moves descendants too, recording their own versions.

The backend validates ownership, destination space and parent, and rejects hierarchy cycles. A
missing destination or parent now in a different space prevents restoration without partial writes.
The client sends its latest known version number; changes since then require reloading history.
The counter also guards simultaneous database writes.
Regaining window focus or reconnecting does not automatically advance that reference revision.
Metadata differences compare original values, including space and parent IDs, even when their
display names are identical.

Restoration appends a version with source `restore` and `restoredFrom`. The Notes persistence
interceptor captures page changes and snapshots in the same transaction, including assistant edits,
subtree moves and soft deletion.

## Existing pages, deletion and media

Migration `20261102140000` snapshots the current state of existing pages, including deleted pages;
it cannot recover edits made before deployment. Soft deletion retains versions in the database,
but endpoints do not expose deleted pages. This feature adds neither a trash view nor page undeletion.

Following the project's pre-1.0 convention, `history_number` is added to the original page
`CREATE TABLE`; the new migration creates only the new table. A local database with the old schema
must be updated/recreated before running this version. The implementation does not automatically
recreate or erase the local database.

Snapshots preserve media references without copying bytes. The current library keeps bytes after
soft deletion; any future physical purge must account for historical references. Page history is
not an independent database backup.

## API

All routes require authentication and page ownership:

| Method | Route | Response |
|---|---|---|
| GET | `/api/v1/notes/pages/{id}/versions?before={number}` | Up to 100 summaries, descending; exclusive cursor |
| GET | `/api/v1/notes/pages/{id}/versions/{number}` | Complete version snapshot |
| POST | `/api/v1/notes/pages/{id}/versions/{number}/restore` | Restored page; body `{ "expectedVersion": 18 }` |

Missing or foreign resources return 404; concurrent changes and invalid destinations return 409
(or 404 for a missing space/parent). Listing does not load historical bodies from the database.
