// Attachments are served by an authenticated endpoint, so the browser cannot reach one on its own:
// no Authorization header rides along with an <img src> or with a click on a link. Everything here
// exists to bridge that — finding the attachment paths a page embeds, and swapping them for object
// URLs fetched through the API client.

export const ATTACHMENT_PREFIX = '/api/v1/notes/attachments/'

/** The path of an image embed: `![alt](/api/v1/notes/attachments/{id})`. */
const IMAGE_EMBED_PATTERN = new RegExp(
  String.raw`!\[[^\]]*\]\((${ATTACHMENT_PREFIX}[^)\s]+)\)`,
  'g',
)

/**
 * The attachment paths the markdown **embeds as images**, deduplicated. Only images: those have to
 * be fetched for the page to render at all, while a plain link (a zip, a PDF) is fetched when it is
 * clicked — prefetching those would download every attachment of a page just for opening it.
 */
export function parseAttachmentImagePaths(markdown: string): string[] {
  const paths: string[] = []
  for (const match of markdown.matchAll(IMAGE_EMBED_PATTERN)) {
    if (!paths.includes(match[1])) paths.push(match[1])
  }
  return paths
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

/**
 * Points every `<img>` at the object URL fetched for it. This runs on the sanitized HTML, on purpose:
 * DOMPurify's URI allow-list does not cover `blob:`, so an inlined object URL would be stripped if it
 * went through it. Substituting afterwards is safe because it only replaces a path this module
 * produced with a URL this module created.
 *
 * Only `src` is rewritten — a link's `href` keeps the API path, which the preview's click handler
 * needs to turn into a download.
 */
export function withAttachmentUrls(html: string, urls: ReadonlyMap<string, string>): string {
  let result = html
  for (const [path, url] of urls) {
    result = result.replace(new RegExp(`src="${escapeRegExp(path)}"`, 'g'), `src="${url}"`)
  }
  return result
}

/** Whether a URL points at the attachment endpoint. */
export function isAttachmentUrl(url: string): boolean {
  return url.includes(ATTACHMENT_PREFIX)
}

/**
 * The name to save a downloaded attachment as. The markdown label is the original file name (that is
 * what the editor writes when embedding), so it is the best source; the id is the last resort.
 */
export function attachmentFileName(path: string, label?: string | null): string {
  const trimmed = label?.trim()
  if (trimmed) return trimmed

  const id = path.slice(path.lastIndexOf('/') + 1)
  return id.length > 0 ? id : 'attachment'
}
