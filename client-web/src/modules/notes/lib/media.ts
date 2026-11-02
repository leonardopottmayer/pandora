// Library media are served by an authenticated endpoint, so the browser cannot reach one on its own:
// no Authorization header rides along with an <img src> or with a click on a link. Everything here
// exists to bridge that — finding the media paths a page embeds, and swapping them for object
// URLs fetched through the API client.

export const MEDIA_PREFIX = '/api/v1/notes/media/'

/** The path of an image embed: `![alt](/api/v1/notes/media/{id})` or `<img src="/api/v1/notes/media/{id}">`. */
const IMAGE_EMBED_PATTERN = new RegExp(
  String.raw`!\[[^\]]*\]\((${MEDIA_PREFIX}[^)\s]+)\)|<img\b[^>]*?\bsrc\s*=\s*["']?(${MEDIA_PREFIX}[^"'\s>]+)`,
  'gi',
)

/**
 * The media paths the markdown **embeds as images**, deduplicated. Only images: those have to
 * be fetched for the page to render at all, while a plain link (a zip, a PDF) is fetched when it is
 * clicked — prefetching those would download every file of a page just for opening it.
 */
export function parseMediaImagePaths(markdown: string): string[] {
  const paths: string[] = []
  for (const match of markdown.matchAll(IMAGE_EMBED_PATTERN)) {
    const path = match[1] ?? match[2]
    if (!paths.includes(path)) paths.push(path)
  }
  return paths
}

const MEDIA_SRC = new RegExp(`src="(${MEDIA_PREFIX}[^"]*)"`, 'g')

/**
 * Points every `<img>` at the object URL fetched for it. This runs on the sanitized HTML, on purpose:
 * DOMPurify's URI allow-list does not cover `blob:`, so an inlined object URL would be stripped if it
 * went through it. Substituting afterwards is safe because it only replaces a path this module
 * produced with a URL this module created.
 *
 * An image whose URL is not there yet (still loading, or an id the library does not have) loses its
 * `src` until it is: left on the API path, the browser would fetch it with no token and log a 401.
 *
 * Only `src` is rewritten — a link's `href` keeps the API path, which the preview's click handler
 * needs to turn into a download.
 */
export function withMediaUrls(html: string, urls: ReadonlyMap<string, string>): string {
  return html.replace(MEDIA_SRC, (_, path: string) => {
    const url = urls.get(path)
    return url ? `src="${url}"` : `data-media-src="${path}"`
  })
}

/** Whether a URL points at the media endpoint. */
export function isMediaUrl(url: string): boolean {
  return url.includes(MEDIA_PREFIX)
}

/**
 * The name to save a downloaded item as. The markdown label is the original file name (that is
 * what the editor writes when embedding), so it is the best source; the id is the last resort.
 */
export function mediaFileName(path: string, label?: string | null): string {
  const trimmed = label?.trim()
  if (trimmed) return trimmed

  const id = path.slice(path.lastIndexOf('/') + 1)
  return id.length > 0 ? id : 'file'
}

/** The drag payload a library item carries to a folder: its id, under a type only the library reads. */
export const MEDIA_DRAG_TYPE = 'application/x-pandora-media'

/** The markdown a note embeds an item with: an image draws inline, a file is a link. */
export function embedMarkdown(item: { kind: string; name: string; url: string }): string {
  return item.kind === 'image' ? `![${item.name}](${item.url})` : `[${item.name}](${item.url})`
}
