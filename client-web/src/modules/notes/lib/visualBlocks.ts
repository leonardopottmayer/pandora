import { Node, type Editor, type MarkdownLexerConfiguration, type MarkdownToken } from '@tiptap/core'
import { CALLOUT_TYPES } from './callouts'

const CALLOUT = /^ {0,3}> *\[!(\w+)\]([+-]?) *([^\n]*)(?:\n|$)((?: {0,3}>[^\n]*(?:\n|$))*)/

/** Consume a complete details block, ignoring tags inside code and comments. */
function detailsEnd(src: string): number {
  const parts = /<!--[^]*?-->|^ {0,3}(`{3,}|~{3,})([^\n]*)(?:\n|$)|(`+)[^\n]*?\3(?!`)|<\/?details\b[^>]*>/gim
  let depth = 0
  let fence: string | null = null
  for (const match of src.matchAll(parts)) {
    if (match[1]) {
      if (!fence) fence = match[1]
      else if (match[1][0] === fence[0] && match[1].length >= fence.length && !match[2].trim()) fence = null
      continue
    }
    if (fence) continue
    if (!/^<\/?details\b/i.test(match[0])) continue
    depth += /^<\//.test(match[0]) ? -1 : 1
    if (depth === 0) return match.index + match[0].length
  }
  return -1
}

export function tokenizeVisualBlock(src: string, lexer: MarkdownLexerConfiguration): MarkdownToken | undefined {
  const callout = CALLOUT.exec(src)
  if (callout) {
    const calloutType = callout[1].toLowerCase()
    if (!(CALLOUT_TYPES as readonly string[]).includes(calloutType)) {
      return { type: 'rawBlock', raw: callout[0] }
    }
    return {
      type: 'rawBlock', raw: callout[0], blockType: 'callout',
      attrs: { calloutType, title: callout[3].trim(), fold: callout[2] || null },
      tokens: lexer.blockTokens(callout[4].replace(/^ {0,3}> ?/gm, '')),
    }
  }

  if (!/^ {0,3}<details\b/i.test(src)) return undefined
  const end = detailsEnd(src)
  if (end === -1) return undefined
  const raw = src.slice(0, end)
  // Only the supported HTML shape becomes editable. Custom attributes/summary HTML stay verbatim.
  const details = /^ {0,3}<details(\s+open(?:\s*=\s*(?:""|"open"|'open'|open))?)?\s*>\s*<summary>([^<>\n]*)<\/summary>\s*\n([^]*)<\/details>$/i.exec(raw)
  if (!details) return { type: 'rawBlock', raw }
  const summary = new DOMParser().parseFromString(`<body>${details[2]}</body>`, 'text/html').body.textContent ?? ''
  return {
    type: 'rawBlock', raw, blockType: 'details',
    attrs: { title: summary, open: !!details[1] },
    tokens: lexer.blockTokens(details[3].replace(/^\n+|\n+$/g, '')),
  }
}

const escapeHtml = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

/** Leave the innermost block without lifting or splitting its body. */
function exitBlock(editor: Editor): boolean {
  const { $from } = editor.state.selection
  for (let depth = $from.depth; depth > 0; depth--) {
    if (!['callout', 'details'].includes($from.node(depth).type.name)) continue
    const after = $from.after(depth)
    const next = editor.state.doc.nodeAt(after)
    if (next?.type.name === 'paragraph' && next.content.size === 0) return editor.commands.focus(after + 1)
    return editor.chain().insertContentAt(after, { type: 'paragraph' }).focus(after + 1).run()
  }
  return false
}

/** The heading is an attribute; the body is ordinary ProseMirror content, including nested blocks. */
export const CalloutBlock = Node.create({
  name: 'callout',
  group: 'block',
  content: 'block+',
  defining: true,
  isolating: true,
  draggable: true,

  addKeyboardShortcuts() {
    return { 'Mod-Enter': () => exitBlock(this.editor) }
  },

  addAttributes() {
    return {
      title: { default: '' },
      calloutType: { default: 'note' },
      fold: { default: null },
      autoFocus: { default: false, rendered: false },
    }
  },
  parseHTML() {
    return [{
      tag: 'div[data-callout]',
      getAttrs: (el) => ({ title: el.dataset.title ?? '', calloutType: el.dataset.callout, fold: el.dataset.fold || null }),
    }]
  },
  renderHTML({ node }) {
    return ['div', { 'data-callout': node.attrs.calloutType, 'data-title': node.attrs.title, 'data-fold': node.attrs.fold ?? '' }, 0]
  },
  renderMarkdown(node, helpers) {
    const { calloutType, fold, title } = node.attrs ?? {}
    const body = helpers.renderChildren(node.content ?? [], '\n\n').replace(/\n+$/, '')
    const heading = `> [!${calloutType}]${fold ?? ''}${title ? ` ${title}` : ''}`
    return body ? `${heading}\n${body.split('\n').map((line) => `>${line ? ` ${line}` : ''}`).join('\n')}` : heading
  },
})

export const DetailsBlock = Node.create({
  name: 'details',
  group: 'block',
  content: 'block+',
  defining: true,
  isolating: true,
  draggable: true,

  addKeyboardShortcuts() {
    return { 'Mod-Enter': () => exitBlock(this.editor) }
  },

  addAttributes() {
    return {
      title: { default: '' },
      open: { default: false, parseHTML: (el) => el.hasAttribute('open') },
      autoFocus: { default: false, rendered: false },
    }
  },
  parseHTML() {
    return [{
      tag: 'details',
      priority: 60,
      contentElement: '[data-details-body]',
      getAttrs: (el) => el.querySelector(':scope > [data-details-body]')
        ? { title: el.querySelector('summary')?.textContent ?? '', open: el.hasAttribute('open') }
        : false,
    }]
  },
  renderHTML({ node }) {
    return ['details', node.attrs.open ? { open: '' } : {},
      ['summary', {}, node.attrs.title], ['div', { 'data-details-body': '' }, 0]]
  },
  renderMarkdown(node, helpers) {
    const body = helpers.renderChildren(node.content ?? [], '\n\n').replace(/\n+$/, '')
    return `<details${node.attrs?.open ? ' open' : ''}>\n<summary>${escapeHtml(node.attrs?.title ?? '')}</summary>\n\n${body}\n\n</details>`
  },
})
