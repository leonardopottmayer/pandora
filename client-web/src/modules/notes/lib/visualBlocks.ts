import { Node, type Editor } from '@tiptap/core'

// Callouts and toggles: a title in an attribute, a body of ordinary blocks.

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
      // 1 to 3 makes it a collapsible heading: the title reads as that heading.
      level: { default: null },
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
})
