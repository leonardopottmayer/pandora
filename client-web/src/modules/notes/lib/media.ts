// Library media are served by an authenticated endpoint, so the browser cannot reach one on its own:
// no Authorization header rides along with an <img src> or with a click on a link. Everything here
// exists to bridge that: the blocks name an item by id, and its bytes come through the API client.

export const MEDIA_PREFIX = '/api/v1/notes/media/'

// A public page shows the library items it uses through its own link, read without signing in.
let publicToken: string | null = null

/** Set while a public page is open, cleared when it closes. */
export function setPublicMediaToken(token: string | null) {
  publicToken = token
}

/** The path a library item is downloaded from: the owner's authenticated one, or the public page's. */
export function mediaPath(id: string): string {
  return publicToken ? `/api/v1/public/notes/pages/${encodeURIComponent(publicToken)}/media/${id}` : `${MEDIA_PREFIX}${id}`
}

/** Whether a URL points at the media endpoint. */
export function isMediaUrl(url: string): boolean {
  return url.includes(MEDIA_PREFIX)
}

/** The name to save a downloaded item as: its original file name, or the id as the last resort. */
export function mediaFileName(path: string, label?: string | null): string {
  const trimmed = label?.trim()
  if (trimmed) return trimmed

  const id = path.slice(path.lastIndexOf('/') + 1)
  return id.length > 0 ? id : 'file'
}

/** The drag payload a library item carries to a folder: its id, under a type only the library reads. */
export const MEDIA_DRAG_TYPE = 'application/x-pandora-media'

/** An emoji for a file card; it reads at any size. */
export function fileGlyph(contentType: string): string {
  const type = contentType.toLowerCase()
  if (type.includes('pdf')) return '📕'
  if (type.includes('zip') || type.includes('compressed')) return '🗜️'
  if (type.includes('sheet') || type.includes('excel') || type.includes('csv')) return '📊'
  if (type.startsWith('text/') || type.includes('word') || type.includes('document')) return '📄'
  if (type.startsWith('image/')) return '🖼️'
  return '📎'
}
