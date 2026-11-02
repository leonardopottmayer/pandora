import { Extension, Node, type AnyExtension, type JSONContent, type NodeViewRenderer } from '@tiptap/core'
import { MarkdownManager } from '@tiptap/markdown'
import StarterKit from '@tiptap/starter-kit'
import { TaskItem, TaskList } from '@tiptap/extension-list'
import { TableKit } from '@tiptap/extension-table'
import { Paragraph } from '@tiptap/extension-paragraph'
import { Marked, type marked } from 'marked'
import { isMediaUrl } from './media'

// The visual editor reads and writes the same markdown the code editor does: the text stays the one
// source of truth. TipTap's markdown bridge knows the CommonMark/GFM blocks; what is ours is taught
// here. Anything it would not give back as it was — a callout, a <details>, raw HTML — is kept as a
// *raw block*: its source goes back out untouched, and the editor draws it with the preview.

/** `[[target]]`, `[[target|alias]]`, `![[target]]` — the same shape the preview and the backend read. */
const WIKILINK = /^(!)?\[\[([^[\]|]*)(?:\|([^[\]]*))?\]\]/

export const Wikilink = Node.create({
  name: 'wikilink',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: true,

  addAttributes() {
    return { target: { default: '' }, alias: { default: null }, embed: { default: false } }
  },

  parseHTML() {
    return [{ tag: 'span[data-wikilink]', getAttrs: (el) => ({ target: el.getAttribute('data-wikilink') }) }]
  },

  renderHTML({ node }) {
    return ['span', { 'data-wikilink': node.attrs.target }, `[[${node.attrs.target}]]`]
  },

  markdownTokenizer: {
    name: 'wikilink',
    level: 'inline',
    start: (src: string) => src.search(/!?\[\[/),
    tokenize(src: string) {
      const match = WIKILINK.exec(src)
      if (!match || !match[2].trim()) return undefined
      return { type: 'wikilink', raw: match[0], embed: !!match[1], target: match[2], alias: match[3] ?? null }
    },
  },

  parseMarkdown: (token) => ({
    type: 'wikilink',
    attrs: { target: token.target, alias: token.alias, embed: token.embed },
  }),

  renderMarkdown: (node) => {
    const { target, alias, embed } = node.attrs ?? {}
    return `${embed ? '!' : ''}[[${target}${alias != null ? `|${alias}` : ''}]]`
  },
})

/** An image, inline as markdown has it; width, alignment and caption ride in the alt (`Beach|300|right`). */
export const MediaImage = Node.create({
  name: 'image',
  group: 'inline',
  inline: true,
  atom: true,
  draggable: true,

  addAttributes() {
    return { src: { default: '' }, alt: { default: '' }, title: { default: null } }
  },

  parseHTML() {
    return [
      {
        tag: 'img',
        // A hand-written <img width="200"> keeps its width the markdown way.
        getAttrs: (el) => {
          const src = el.getAttribute('src') ?? el.getAttribute('data-media-src')
          if (!src) return false
          const width = el.getAttribute('width')
          const alt = el.getAttribute('alt') ?? ''
          return { src, alt: width && /^\d+$/.test(width) ? `${alt}|${width}` : alt }
        },
      },
    ]
  },

  // What copying or dragging puts on the clipboard. A library image goes without a `src`: the browser
  // fetches an <img> even off screen, and the API path answers 401 without the token.
  renderHTML({ node }) {
    const { src, alt } = node.attrs
    return ['img', isMediaUrl(src) ? { 'data-media-src': src, alt } : { src, alt }]
  },

  markdownTokenName: 'image',

  parseMarkdown: (token) => ({
    type: 'image',
    attrs: { src: token.href, alt: token.text ?? '', title: token.title ?? null },
  }),

  renderMarkdown: (node) => {
    const { src, alt, title } = node.attrs ?? {}
    return `![${alt}](${src}${title ? ` "${title}"` : ''})`
  },
})

/**
 * A paragraph holding only an image stays a paragraph. TipTap's own lifts the image out, its image being
 * a block; ours sits inline, as markdown has it, and a bare image at the top would be an invalid
 * document. Any other paragraph reads the stock way.
 */
const ImageParagraph = Paragraph.extend({
  parseMarkdown(token, helpers) {
    const tokens = token.tokens ?? []
    if (tokens.length === 1 && tokens[0].type === 'image') {
      return helpers.createNode('paragraph', undefined, helpers.parseInline(tokens))
    }
    return this.parent?.(token, helpers) ?? []
  },
})

/** Callouts, foldable or not: a run of `>` lines opening with `[!type]`. */
const CALLOUT = /^ {0,3}> *\[!\w+\][+-]?[^\n]*(?:\n|$)(?: {0,3}>[^\n]*(?:\n|$))*/

/** Where a `<details>` at the start of `src` closes, nested ones included; -1 when it never does. */
function detailsEnd(src: string): number {
  const tag = /<(\/?)details\b[^>]*>/gi
  let depth = 0
  for (let match = tag.exec(src); match; match = tag.exec(src)) {
    depth += match[1] ? -1 : 1
    if (depth === 0) {
      const lineEnd = src.indexOf('\n', tag.lastIndex)
      return lineEnd === -1 ? src.length : lineEnd + 1
    }
  }
  return -1
}

/**
 * Markdown the visual editor does not edit as blocks, kept verbatim. A `<details>` spans its blank
 * lines whole — marked would cut it into pieces at the first one — and HTML blocks are taken from
 * marked's own `html` token.
 */
export const RawBlock = Node.create({
  name: 'rawBlock',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    // `autoEdit` only lives in the editor: a block just made from the `/` menu opens on its source.
    return { source: { default: '' }, autoEdit: { default: false, rendered: false } }
  },

  parseHTML() {
    return [{ tag: 'div[data-raw-block]', getAttrs: (el) => ({ source: el.getAttribute('data-raw-block') }) }]
  },

  renderHTML({ node }) {
    return ['div', { 'data-raw-block': node.attrs.source }]
  },

  markdownTokenizer: {
    name: 'rawBlock',
    level: 'block',
    start: (src: string) => src.search(/^ {0,3}(?:> *\[!|<details\b)/im),
    tokenize(src: string) {
      const callout = CALLOUT.exec(src)
      if (callout) return { type: 'rawBlock', raw: callout[0] }
      if (!/^ {0,3}<details\b/i.test(src)) return undefined
      const end = detailsEnd(src)
      return end === -1 ? undefined : { type: 'rawBlock', raw: src.slice(0, end) }
    },
  },

  parseMarkdown: (token) => ({ type: 'rawBlock', attrs: { source: (token.raw ?? '').replace(/\n+$/, '') } }),

  renderMarkdown: (node) => node.attrs?.source ?? '',
})

/**
 * What keeps two lists apart in markdown. A task list and a bullet list one blank line apart are one
 * list to CommonMark — the preview would merge them, and so would the next load. An empty comment
 * between them is the spec's own way out; it is written on save and dropped on load.
 */
const LIST_BREAK = '<!-- -->'

/** Block HTML (an <iframe>, a hand-written <img>, a <div>) becomes a raw block instead of being reshaped. */
const RawHtml = Extension.create({
  name: 'rawHtml',
  markdownTokenName: 'html',
  // Inline HTML never reaches here: the bridge hands it to the schema directly. A list break passes
  // (`[]`) to the bridge, which drops a comment.
  parseMarkdown: (token) => {
    const source = (token.raw ?? '').replace(/\n+$/, '')
    return token.block && source.trim() !== LIST_BREAK ? { type: 'rawBlock', attrs: { source } } : []
  },
})

/** Lists markdown would read as one when they follow each other: same kind of marker. */
const LIST_MARKER: Record<string, string> = { bulletList: '-', taskList: '-', orderedList: '1.' }

const isEmptyParagraph = (node: JSONContent) => node.type === 'paragraph' && !node.content?.length

/** The top-level blocks with a list break between two lists that would otherwise merge. */
function withListBreaks(doc: JSONContent): JSONContent {
  const blocks: JSONContent[] = []
  let lastMarker: string | undefined
  for (const block of doc.content ?? []) {
    const marker = block.type ? LIST_MARKER[block.type] : undefined
    if (marker && marker === lastMarker) blocks.push({ type: 'rawBlock', attrs: { source: LIST_BREAK } })
    // Empty paragraphs are blank lines, which do not end a list either.
    if (!isEmptyParagraph(block)) lastMarker = marker
    blocks.push(block)
  }
  return { ...doc, content: blocks }
}

/** How the editor draws our own nodes; left out, they draw with their plain HTML (the tests). */
export interface VisualNodeViews {
  wikilink?: NodeViewRenderer
  image?: NodeViewRenderer
  rawBlock?: NodeViewRenderer
}

const withView = (node: Node, view?: NodeViewRenderer) => (view ? node.extend({ addNodeView: () => view }) : node)

/** The schema and markdown rules both editors' text goes through. */
export function visualExtensions(views: VisualNodeViews = {}): AnyExtension[] {
  return [
    StarterKit.configure({
      // Underline has no markdown: it would come back as `++text++`, which nothing renders.
      underline: false,
      paragraph: false,
      link: { openOnClick: false, autolink: true },
    }),
    ImageParagraph,
    TaskList,
    TaskItem.configure({ nested: true }),
    TableKit.configure({ table: { resizable: false } }),
    withView(Wikilink, views.wikilink),
    withView(MediaImage, views.image),
    withView(RawBlock, views.rawBlock),
    RawHtml,
  ]
}

/** A marked of its own: the bridge registers tokenizers on it, which must not leak into the preview's. */
export function newMarked() {
  return new Marked() as unknown as typeof marked
}

/** The bridge outside an editor — what the round-trip tests go through. */
export function createMarkdownManager(extensions: AnyExtension[] = visualExtensions()) {
  return new MarkdownManager({ marked: newMarked(), extensions })
}

const LINK = /\[((?:\\.|[^\]\\\n])+)\]\((\S+?)\)/g

/**
 * The document as markdown. A link whose text is its own URL goes back to being a bare URL: written as
 * `[url](url)` a video link would no longer sit alone on its line, and stop drawing as the player.
 * The empty paragraph the editor keeps at the end, to have somewhere to type after a block, is not
 * written out, and two lists in a row stay two lists.
 */
export function serializeMarkdown(manager: MarkdownManager, doc: JSONContent): string {
  return manager
    .serialize(withListBreaks(doc))
    .replace(LINK, (link, text: string, href: string) => (text.replace(/\\(.)/g, '$1') === href ? href : link))
    .replace(/\n+$/, '')
}
