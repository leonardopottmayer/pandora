# Attachments & Storage

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Editor](editor.md), [API Reference](api-reference.md)

---

## 1. What an attachment is

Any binary file uploaded to the module: an image to embed, a PDF, a zip. Two rows are involved:

- **`nte002_attachment`** — the metadata plus the `(storage_backend, storage_key)` pair that locates
  the bytes. Write-once: nothing edits an attachment after upload, which is why it carries only a
  `created_at`.
- **`nte003_file_blob`** — the bytes themselves, in the MVP a `bytea` column.

`page_id` is optional and holds **no foreign key**: an attachment can exist before being embedded
anywhere, and a page can be soft-deleted while its attachment lingers.

## 2. The storage abstraction

`IFileStorage` (in `Persistence/Storage`) is the port: save, read, delete a blob. There is exactly
one real implementation — **`DatabaseFileStorage`**, over `IFileBlobRepository`.

The abstraction is not the interesting part; the **self-describing rows** are. Each attachment
records *which backend* holds its bytes and *the key inside it*. When an S3/MinIO backend is added,
new uploads write `storage_backend = S3` and a bucket key, old rows keep saying `Database`, and
**reads need no migration** — the row tells the reader where to look.

> The natural trigger for turning S3 on is size: `bytea` is fine for images and small/medium files;
> heavy video or large archives is where the abstraction earns its keep.

## 3. Upload and download

| Endpoint | Behaviour |
|---|---|
| `POST /notes/attachments` | `multipart/form-data`: `file` plus an optional `pageId`. An empty content type falls back to `application/octet-stream`. Returns `AttachmentDto`, whose `url` is `/api/v1/notes/attachments/{id}` — the exact path the editor writes into the markdown. |
| `GET /notes/attachments/{id}` | Streams the bytes with the stored `Content-Type` and a `Content-Disposition: inline` carrying the original filename. |

`inline` rather than `attachment` so an embedded image renders in place; the filename still rides
along for files the user chooses to save.

Both endpoints are `[Authorize]`. Serving a file by a direct path was never an option.

## 4. The consequence: the browser cannot fetch an attachment by itself

The download endpoint is authenticated and the token lives in `localStorage`, **not in a cookie**.
So neither a browser navigation nor an `<img src>` reaches it on its own. Both paths that depended on
this were originally half-handled, and both were fixed the same way — the client fetches the blob
through `apiClient` and hands the resulting object URL to the DOM.

**Attachment links.** The markdown stores a relative path, so clicking the anchor navigated the
current tab to `/api/...` **on the frontend's origin** — in dev the Vite server with no proxy (hence
a blank page), in production nginx and a 401. The click is now intercepted by the same handler that
already dealt with wikilinks and tags: fetch the blob, hand it to a throwaway `<a download>`. No new
tab, no navigation. The filename comes from the markdown label, which is the original name the editor
wrote when embedding.

**Preview images.** The object URL used to be written straight onto the DOM node (`img.src = …`),
outside React's model. Any re-render that did **not** change the markdown rewrote the HTML and
restored the original path, while the effect that would refetch depended only on `[html]`, which had
not changed — a broken image until the next keystroke. Switching theme, switching language, and any
refetch that recreated `pageIndex`/`tagIndex` (autosave, window focus) all did it.

The fix, in `hooks/useAttachmentUrls.ts`:

- Attachments are resolved **before** rendering, so the URL is part of the `html` itself. The
  `combine` of `useQueries` memoizes the map, so its identity only changes when the results do.
- The substitution happens **after** DOMPurify — its URI allow-list does not cover `blob:`, and an
  object URL embedded earlier would be stripped.
- As a bonus, each attachment is downloaded **once per session** instead of on every keystroke; the
  old effect refetched every image on every markdown change.
- Nobody revokes the object URL: an id addresses immutable bytes, so there is at most one per
  attachment per session, and the reload that ends the session releases them.

The regression test is the one that reproduces the bug: render, wait for the `blob:`, then re-render
with fresh `pageIndex`/`tagIndex` — exactly what a refetch produces.
