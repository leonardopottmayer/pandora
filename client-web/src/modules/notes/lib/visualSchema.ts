import { Extension, InputRule, Mark, Node, type AnyExtension, type NodeViewRenderer } from '@tiptap/core'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import StarterKit from '@tiptap/starter-kit'
import { TaskItem, TaskList } from '@tiptap/extension-list'
import { TableKit } from '@tiptap/extension-table'
import { NodeRange } from '@tiptap/extension-node-range'
import { newBlockId } from './blocks'
import { STYLED_NODES } from './blockStyle'
import { CalloutBlock, DetailsBlock } from './visualBlocks'

// The editor's schema. Each node that is a block of the page (see lib/blocks.ts) carries a `blockId`
// that survives edits, so comments, links to a block and history can point at it.

/** Nodes that are blocks of the page; the first paragraph of a list item or quote belongs to its container. */
export const BLOCK_NODES = [
  'paragraph', 'heading', 'blockquote', 'codeBlock', 'horizontalRule', 'listItem', 'taskItem',
  'callout', 'details', 'table', 'image', 'mediaFile', 'unknownBlock', 'columns', 'column', 'pageLink', 'richBlock', 'synced', 'templateButton', 'decision',
]

/**
 * Gives every block node an id and keeps them unique: a split or a paste copies the attribute, and
 * the copy gets a fresh id.
 */
export const BlockIds = Extension.create({
  name: 'blockIds',

  addGlobalAttributes() {
    return [
      {
        types: BLOCK_NODES,
        attributes: {
          blockId: {
            default: null,
            keepOnSplit: false,
            parseHTML: (element) => element.getAttribute('data-block-id'),
            renderHTML: (attributes) => (attributes.blockId ? { 'data-block-id': attributes.blockId } : {}),
          },
        },
      },
    ]
  },

  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: new PluginKey('blockIds'),
        appendTransaction: (transactions, _old, state) => {
          if (!transactions.some((transaction) => transaction.docChanged)) return null
          const seen = new Set<string>()
          let tr = state.tr
          let changed = false
          state.doc.descendants((node, pos) => {
            if (!BLOCK_NODES.includes(node.type.name)) return
            const id = node.attrs.blockId as string | null
            if (id && !seen.has(id)) {
              seen.add(id)
              return
            }
            const fresh = newBlockId()
            seen.add(fresh)
            tr = tr.setNodeMarkup(pos, undefined, { ...node.attrs, blockId: fresh })
            changed = true
          })
          return changed ? tr : null
        },
      }),
    ]
  },
})

/** A block's colour, background and alignment, as `data-color`, `data-background` and `text-align`. */
export const BlockStyle = Extension.create({
  name: 'blockStyle',

  addGlobalAttributes() {
    const data = (name: string) => ({
      default: null,
      parseHTML: (element: HTMLElement) => element.getAttribute(`data-${name}`),
      renderHTML: (attributes: Record<string, unknown>) => (attributes[name] ? { [`data-${name}`]: attributes[name] } : {}),
    })
    return [
      {
        types: STYLED_NODES,
        attributes: {
          color: data('color'),
          background: data('background'),
          align: {
            default: null,
            parseHTML: (element) => element.style.textAlign || null,
            renderHTML: (attributes) => (attributes.align ? { style: `text-align: ${attributes.align}` } : {}),
          },
        },
      },
    ]
  },
})

/** A colour on a stretch of text (`color`) or behind it (`highlight`), from the same eight as blocks. */
const colorMark = (name: 'color' | 'highlight', data: string) =>
  Mark.create({
    name,
    addAttributes() {
      return { color: { default: null } }
    },
    parseHTML() {
      return [{ tag: `span[data-${data}]`, getAttrs: (element) => ({ color: element.getAttribute(`data-${data}`) }) }]
    },
    renderHTML({ mark }) {
      return ['span', { [`data-${data}`]: mark.attrs.color }, 0]
    },
  })

export const TextColor = colorMark('color', 'color')
export const TextHighlight = colorMark('highlight', 'background')

/**
 * The part of the text a comment thread is about. It stores only the thread's id; the page paints the
 * marks of open threads, so a resolved or deleted thread leaves no trace in the text. Typing at its
 * edge does not grow it, and two threads may cover the same words.
 */
export const CommentMark = Mark.create({
  name: 'comment',
  inclusive: false,
  excludes: '',
  addAttributes() {
    return { thread: { default: null } }
  },
  parseHTML() {
    return [{ tag: 'span[data-thread]', getAttrs: (element) => ({ thread: element.getAttribute('data-thread') }) }]
  },
  renderHTML({ mark }) {
    return ['span', { 'data-thread': mark.attrs.thread, class: 'notes-comment-mark' }, 0]
  },
})

/** Side-by-side columns, 2 to 4, each an ordinary body of blocks. A narrow screen stacks them. */
export const Columns = Node.create({
  name: 'columns',
  group: 'block',
  content: 'column{2,4}',
  isolating: true,
  draggable: true,
  parseHTML: () => [{ tag: 'div[data-columns]' }],
  renderHTML: () => ['div', { 'data-columns': '' }, 0],
})

export const Column = Node.create({
  name: 'column',
  content: 'block+',
  isolating: true,
  parseHTML: () => [{ tag: 'div[data-column]' }],
  renderHTML: () => ['div', { 'data-column': '' }, 0],
})

/** Another page as a block: a card with its icon and title, or a preview with its first lines too. */
export const PageLink = Node.create({
  name: 'pageLink',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return { pageId: { default: '' }, look: { default: 'card' } }
  },

  parseHTML() {
    return [{ tag: 'div[data-page-link]', getAttrs: (element) => ({ pageId: element.getAttribute('data-page-link'), look: element.getAttribute('data-look') || 'card' }) }]
  },

  renderHTML({ node }) {
    return ['div', { 'data-page-link': node.attrs.pageId, 'data-look': node.attrs.look }]
  },
})

/** The media blocks that draw from their props alone: bookmark, embed, video, audio, PDF, gallery. */
export const RICH_BLOCKS = [
  'bookmark', 'embed', 'video', 'audio', 'pdf', 'gallery',
  'mermaid', 'math', 'synced-ref', 'breadcrumb', 'subpages', 'tag-pages', 'properties',
] as const
export type RichBlockKind = (typeof RICH_BLOCKS)[number]

/** One node for all of them: the block's kind and props, drawn by its view. */
export const RichBlock = Node.create({
  name: 'richBlock',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return { kind: { default: 'embed' }, props: { default: {}, rendered: false } }
  },

  parseHTML() {
    return [
      { tag: 'div[data-rich-block]', getAttrs: (element) => ({ kind: element.getAttribute('data-rich-block') }) },
      // What "copy to sync" puts on the clipboard: pasted into a page, the copy of a synced block.
      {
        tag: 'div[data-synced-ref]',
        getAttrs: (element) => ({
          kind: 'synced-ref',
          props: { pageId: element.getAttribute('data-page') ?? '', blockId: element.getAttribute('data-block') ?? '' },
        }),
      },
    ]
  },

  renderHTML({ node }) {
    return ['div', { 'data-rich-block': node.attrs.kind }]
  },
})

/** The original of a synced block: its body is edited here, and its copies in other pages show it. */
export const SyncedBlock = Node.create({
  name: 'synced',
  group: 'block',
  content: 'block+',
  isolating: true,
  draggable: true,
  parseHTML: () => [{ tag: 'div[data-synced]' }],
  renderHTML: () => ['div', { 'data-synced': '' }, 0],
})

/** A button that puts a copy of its body (the template) below it; the body is edited when opened. */
export const TemplateButton = Node.create({
  name: 'templateButton',
  group: 'block',
  content: 'block+',
  isolating: true,
  draggable: true,

  addAttributes() {
    return { label: { default: '' }, editing: { default: false, rendered: false } }
  },

  parseHTML: () => [{ tag: 'div[data-template-button]', getAttrs: (element) => ({ label: element.getAttribute('data-template-button') ?? '' }) }],
  renderHTML: ({ node }) => ['div', { 'data-template-button': node.attrs.label }, 0],
})

/** A decision: its text and where it stands (open, decided, rejected). */
export const Decision = Node.create({
  name: 'decision',
  group: 'block',
  content: 'inline*',
  defining: true,

  addAttributes() {
    return { status: { default: 'open' } }
  },

  parseHTML: () => [{ tag: 'div[data-decision]', getAttrs: (element) => ({ status: element.getAttribute('data-decision') || 'open' }) }],
  renderHTML: ({ node }) => ['div', { 'data-decision': node.attrs.status }, 0],
})

/** A page named inside the text: stored by id, drawn as the page's current icon and title. */
export const Mention = Node.create({
  name: 'mention',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: true,

  addAttributes() {
    return { pageId: { default: '' } }
  },

  parseHTML() {
    return [{ tag: 'span[data-mention]', getAttrs: (element) => ({ pageId: element.getAttribute('data-mention') }) }]
  },

  renderHTML({ node }) {
    return ['span', { 'data-mention': node.attrs.pageId }, '@']
  },

  renderText: () => '@',
})

/**
 * A chip in the text: a date (with its reminder), a status, an item of another module or a formula.
 * One node for all of them, by kind; `$x^2$` typed in the text becomes a formula.
 */
export const Chip = Node.create({
  name: 'chip',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: true,

  addAttributes() {
    return { kind: { default: 'status' }, data: { default: {}, rendered: false } }
  },

  parseHTML() {
    return [{ tag: 'span[data-chip]', getAttrs: (element) => ({ kind: element.getAttribute('data-chip') }) }]
  },

  renderHTML({ node }) {
    return ['span', { 'data-chip': node.attrs.kind }]
  },

  renderText: ({ node }) => {
    const data = node.attrs.data as Record<string, unknown>
    return String(data.label ?? data.latex ?? data.value ?? '')
  },

  addInputRules() {
    return [
      new InputRule({
        find: /(?:^|\s)\$([^$\s][^$]*)\$$/,
        handler: ({ state, range, match }) => {
          const start = range.from + match[0].indexOf('$')
          state.tr.replaceWith(start, range.to, this.type.create({ kind: 'math', data: { latex: match[1] } }))
        },
      }),
    ]
  },
})

/** An image block: a library item (`mediaId`) or an outside address (`src`), with width, alignment and caption. */
export const MediaImage = Node.create({
  name: 'image',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return {
      mediaId: { default: null },
      src: { default: null },
      width: { default: null },
      align: { default: null },
      caption: { default: '' },
    }
  },

  parseHTML() {
    return [
      {
        tag: 'figure[data-image]',
        getAttrs: (element) => ({ mediaId: element.getAttribute('data-media-id') || null, src: element.getAttribute('data-src') || null }),
      },
      // An image pasted from a web page keeps its address.
      { tag: 'img[src]', getAttrs: (element) => (element.getAttribute('src')?.startsWith('http') ? { src: element.getAttribute('src') } : false) },
    ]
  },

  renderHTML({ node }) {
    return ['figure', { 'data-image': '', 'data-media-id': node.attrs.mediaId ?? '', 'data-src': node.attrs.src ?? '' }]
  },
})

/** A library file, drawn as a card. */
export const MediaFile = Node.create({
  name: 'mediaFile',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return { mediaId: { default: null }, name: { default: '' } }
  },

  parseHTML() {
    return [{ tag: 'div[data-media-file]', getAttrs: (element) => ({ mediaId: element.getAttribute('data-media-file'), name: element.textContent ?? '' }) }]
  },

  renderHTML({ node }) {
    return ['div', { 'data-media-file': node.attrs.mediaId ?? '' }, node.attrs.name]
  },
})

/** A block of a type this editor does not draw yet: kept whole and saved back untouched. */
export const UnknownBlock = Node.create({
  name: 'unknownBlock',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return { block: { default: null, rendered: false } }
  },

  renderHTML() {
    return ['div', { 'data-unknown-block': '' }]
  },
})

/** How the editor draws our own nodes; left out, they draw with their plain HTML (the tests). */
export interface VisualNodeViews {
  mention?: NodeViewRenderer
  chip?: NodeViewRenderer
  image?: NodeViewRenderer
  mediaFile?: NodeViewRenderer
  unknownBlock?: NodeViewRenderer
  callout?: NodeViewRenderer
  details?: NodeViewRenderer
  pageLink?: NodeViewRenderer
  richBlock?: NodeViewRenderer
  synced?: NodeViewRenderer
  templateButton?: NodeViewRenderer
  decision?: NodeViewRenderer
}

const withView = (node: Node, view?: NodeViewRenderer) => (view ? node.extend({ addNodeView: () => view }) : node)

export function visualExtensions(views: VisualNodeViews = {}): AnyExtension[] {
  return [
    StarterKit.configure({ link: { openOnClick: false, autolink: true } }),
    TaskList,
    TaskItem.configure({ nested: true }),
    TableKit.configure({ table: { resizable: false } }),
    BlockIds,
    BlockStyle,
    TextColor,
    TextHighlight,
    CommentMark,
    // Ctrl/Cmd-drag, Shift+arrows or Ctrl/Cmd+A select whole blocks, to move, paint or turn them at once.
    NodeRange,
    Columns,
    Column,
    withView(PageLink, views.pageLink),
    withView(RichBlock, views.richBlock),
    withView(SyncedBlock, views.synced),
    withView(TemplateButton, views.templateButton),
    withView(Decision, views.decision),
    withView(Mention, views.mention),
    withView(Chip, views.chip),
    withView(MediaImage, views.image),
    withView(MediaFile, views.mediaFile),
    withView(UnknownBlock, views.unknownBlock),
    withView(CalloutBlock, views.callout),
    withView(DetailsBlock, views.details),
  ]
}
