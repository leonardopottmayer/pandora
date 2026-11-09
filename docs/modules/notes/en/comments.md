# Comments & Block Dates

[← Back to index](../README.md) · Related: [Blocks & Editor](blocks.md), [Version History](version-history.md)

Two tables hang on stable block ids: `nte013_page_comment` and `nte014_block_stamp`. Neither is part of
a page's versions; deleting a page for good removes both.

---

## 1. Comment on text or on a block

| Where | How |
|---|---|
| **On words** | "Comment" in the selection bar. The thread stores the block id and the quoted words; the editor marks the words with a `comment` mark holding only the thread id, so one block can carry several threads over the same words. |
| **On a whole block** | "Comment" in the block menu (the drag handle). An open thread on a block shows as a line at the block's side. |

## 2. Threads

- A thread is its first comment; replies point at it (`thread_id`). A reply cannot be answered.
- Edit, delete (a thread goes with its replies), resolve and reopen. Only the first comment is
  resolved.
- The page paints only the marks of **open** threads. A resolved or deleted thread leaves the text
  untouched, with no change to the document. Clicking commented words opens their thread.

## 3. The comments panel

The toolbar button shows the number of open threads. The panel lists open and resolved threads; a
thread whose block was removed stays, tagged "Block removed"; clicking a quote goes to the block.

The app has one user per page, so a comment's author is always the page's owner.

## 4. Block dates

Every save compares a **fingerprint** of each block (an MD5 of its own JSON, children excluded) with
the stored one (`BlockStampSynchronizer`):

- a new id is a new block: `created_at` and `updated_at` are set;
- a different fingerprint is an edit: `updated_at` and `updated_source` change;
- an unchanged block keeps its stamp.

`updated_source` is `edit`, `assistant` (an accepted AI result, a capture, an Assistant tool) or
`restore`. The block menu shows when the block was written and last edited, and by what.

## 5. API

| Method | Route | Purpose |
|---|---|---|
| GET | `/notes/pages/{id}/comments` | Every thread of the page with its replies |
| POST | `/notes/pages/{id}/comments` | Open a thread: `{ blockId, quote?, body }` |
| POST | `/notes/comments/{id}/replies` | Reply: `{ body }` |
| PUT | `/notes/comments/{id}` | Edit: `{ body }` |
| PUT | `/notes/comments/{id}/resolved` | Resolve or reopen: `{ resolved }` |
| DELETE | `/notes/comments/{id}` | Delete a comment (a first comment takes its replies) |
| GET | `/notes/pages/{id}/block-stamps` | Every block's dates and source |
