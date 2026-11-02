import type { RendererObject, Tokens } from 'marked'
import { formatBytes } from '@/lib/formatBytes'
import type { MediaItemDto } from '../models'
import { isMediaUrl, MEDIA_PREFIX } from './media'

// How library media read in a note. An image takes a width and a caption from its alt text, the
// Obsidian way (`![Jurerê beach|400](…)`); a file link draws as a card with its size. Both stay plain
// markdown: the text is still the one source of truth, and an exported .md still shows them.

const ID = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'
const ID_IN_PATH = new RegExp(`${MEDIA_PREFIX}(${ID})`)
/** A link to an item that is not an image embed: `[label](/api/v1/notes/media/{id})`. */
const FILE_LINK = new RegExp(String.raw`(?<!!)\[[^\]]*\]\(${MEDIA_PREFIX}(${ID})\)`, 'g')

function escapeHtml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}

/** What an item's path points at, or null for any other URL. */
export function mediaIdOf(href: string): string | null {
  return href.match(ID_IN_PATH)?.[1] ?? null
}

/** The items a page links to as files (not images), for the cards to show their size. */
export function parseMediaFileIds(markdown: string): string[] {
  const ids: string[] = []
  for (const match of markdown.matchAll(FILE_LINK)) if (!ids.includes(match[1])) ids.push(match[1])
  return ids
}

/** A name like `IMG_2034.jpg`: what pasting writes, and not worth showing as a caption. */
const looksLikeFileName = (text: string) => /\.[a-z0-9]{2,5}$/i.test(text.trim())

/**
 * Splits an image's alt text into what it says and how wide it is: `Beach|400` is "Beach" at 400px.
 * The text becomes a caption unless it is just the file name.
 */
export function parseImageAlt(alt: string): { text: string; width: number | null; caption: string | null } {
  const sized = alt.match(/^(.*)\|\s*(\d{1,4})\s*$/)
  const text = (sized ? sized[1] : alt).trim()
  const width = sized ? Number(sized[2]) : null
  return { text, width: width && width > 0 ? width : null, caption: text && !looksLikeFileName(text) ? text : null }
}

/** An emoji for a file card: no icon font in sanitized HTML, and these read at any size. */
function fileGlyph(contentType: string): string {
  const type = contentType.toLowerCase()
  if (type.includes('pdf')) return '📕'
  if (type.includes('zip') || type.includes('compressed')) return '🗜️'
  if (type.includes('sheet') || type.includes('excel') || type.includes('csv')) return '📊'
  if (type.startsWith('text/') || type.includes('word') || type.includes('document')) return '📄'
  if (type.startsWith('image/')) return '🖼️'
  return '📎'
}

/**
 * The image and link renderers for library media; anything else falls through to marked's own.
 * `files` are the items behind the file links, looked up by id — a card draws without them, just
 * with no size until they arrive.
 */
export function mediaRenderer(files: ReadonlyMap<string, MediaItemDto>, removedLabel: string): RendererObject {
  return {
    image({ href, text }: Tokens.Image) {
      if (!isMediaUrl(href)) return false
      const { text: alt, width, caption } = parseImageAlt(text)
      const img = `<img src="${escapeHtml(href)}" alt="${escapeHtml(alt)}" loading="lazy" data-zoom="true"${width ? ` width="${width}"` : ''}>`
      // Spans, not <figure>: the image sits inside the paragraph marked makes, where a block would not.
      return `<span class="notes-figure">${img}${caption ? `<span class="notes-caption">${escapeHtml(caption)}</span>` : ''}</span>`
    },
    link(token: Tokens.Link) {
      const id = mediaIdOf(token.href)
      if (!id) return false
      const label = this.parser.parseInline(token.tokens)
      const item = files.get(id)
      const meta = item
        ? [formatBytes(item.sizeBytes), item.isDeleted ? removedLabel : null].filter(Boolean).join(' · ')
        : ''
      return (
        `<a class="notes-file-card" href="${escapeHtml(token.href)}" data-file-name="${escapeHtml(item?.originalFileName ?? token.text)}">` +
        `<span class="notes-file-glyph">${fileGlyph(item?.contentType ?? '')}</span>` +
        `<span class="notes-file-body"><span class="notes-file-name">${label}</span>` +
        `<span class="notes-file-meta">${escapeHtml(meta)}</span></span></a>`
      )
    },
  }
}
