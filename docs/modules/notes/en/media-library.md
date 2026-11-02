# Media Library

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Editor](editor.md), [API Reference](api-reference.md)

---

## 1. What it is

The **library** holds the images and files notes use, organized in **virtual folders** — names in a
tree, nothing on disk. It belongs to the user and is shared by every [space](spaces.md): one image can
appear in notes of different spaces.

Pasting or dropping a file in the editor sends it to the library, **"Unsorted"** (no folder), and
inserts the markdown that embeds it. The `/notes/library` screen organizes everything afterwards.

## 2. The tables

| Table | What it holds |
|---|---|
| `nte002_media` | The item: kind, name, original name, content type, size, dimensions, folder, and the `(storage_backend, storage_key)` pair locating the bytes |
| `nte008_media_folder` | The folders, a tree by `parent_id` |
| `nte009_media_rendition` | Versions derived from an item, with bytes of their own — the thumbnail today |
| `nte003_file_blob` | The bytes of both items and renditions, in the `DatabaseFileStorage` backend |

`name` is what the library shows and can be renamed; `original_file_name` stays the name of a download.

## 3. Kinds, and what is ready for video

`kind` is `image`, `file` or `video` (`MediaKind`). The upload decides:

- an `image/*` content type the server can read becomes `image`, with width and height;
- an image the server cannot read (unknown format, broken bytes) becomes `file` — kept, just not drawn;
- anything else becomes `file`;
- `video/*` is refused (`Media.UnsupportedKind`, 422): `MediaKind.Accepted` has no video yet.

Video already has its place in the schema and the code. Accepting it takes, besides changing that
constant: a bucket backend (each row's `storage_backend` already allows one, with no migration),
ranged downloads (HTTP 206), and a new `purpose` in `nte009` for the poster frame. The screen already
picks how to show an item by its `kind`.

## 4. Thumbnails

On upload, an image whose longer side is above **400 px** gets a WebP thumbnail of up to 400 px
(`RenditionPurpose.Thumbnail`), stored as an `nte009` row with bytes of its own in the blob store. A
smaller image gets none: it is its own thumbnail, and the screen uses the original when `thumbnailUrl`
is null.

The work sits behind the `IImageProcessor` port, implemented with **ImageSharp 3.1**
(`Infrastructure/Images/ImageSharpProcessor.cs`):

- **EXIF rotation.** Phone photos often keep "turn 90°" in their metadata instead of in the pixels.
  Dimensions and thumbnail come out upright.
- **Why 3.1 and not 4.x.** 4.x checks for a license key at build time and **fails a Release build**
  without one. 3.1 carries the same license (Six Labors Split: Apache 2.0 below 1M USD of revenue)
  without that check. It is pure .NET code — nothing native in the Docker image.

## 5. Folders

Create, rename, move and delete, in a tree like the pages'. Moving a folder into itself or one of its
subfolders is refused (`Media.FolderCycle`, 409), with the same upward walk the page tree uses
(`PageHierarchy.WouldCreateCycle`).

**Deleting a folder deletes nothing in it**: its subfolders and items move up to its parent (or to
"Unsorted"/the top) before the folder goes. The `fk_nte002_folder` and `fk_nte008_parent` foreign keys
are declared to EF so it orders those updates before the delete.

## 6. Deleting an item

The delete is soft (`deleted_at`): the item leaves the library, but **its bytes keep being served**. A
note may still embed it, and which ones do is only tracked once notes record the media they use (the
next phase of the plan). That is why the download does not filter deleted items.

## 7. Serving the bytes

`GET /notes/media/{id}` and `GET /notes/media/{id}/thumbnail` are authenticated. The token lives in
`localStorage`, not in a cookie, so neither an `<img src>` nor a click reaches them alone: the client
fetches the blob through the `apiClient` (`lib/api/blob.ts`, shared with Communications) and uses an
object URL.

Responses carry **`Cache-Control: private, max-age=31536000, immutable`**: an id points at bytes that
never change. The browser keeps both the image and its thumbnail, across sessions too — which is what
keeps a grid of dozens of thumbnails cheap, with no signed links needed.

The notes preview still resolves embedded images before rendering (`hooks/useMediaUrls.ts`),
swapping the path for the object URL **after** DOMPurify, whose URI allow-list does not cover `blob:`.
A click on a file link becomes an in-place download, without sending the tab to the API.

## 8. The screen

`/notes/library` (`pages/LibraryPage.tsx`):

- **On the left**, "Everything", "Unsorted" and the folder tree, each with its item count. Each folder
  has "+" (subfolder) and "⋯" (rename, move, delete).
- **In the middle**, the thumbnail grid, with a name search (across the whole library), an Images /
  Files filter, and uploads by button or by dragging files from the computer — into the open folder.
- **Dragging an item** onto a folder (or onto "Unsorted") moves it.
- **Clicking an item** opens its details: preview, editable name, folder, download, copy markdown,
  delete, plus kind, size, dimensions, original name and date.
- The open folder is in the URL (`?folder=`), and the app menu and the notes sidebar lead to it.
