import type { Editor } from '@tiptap/core'
import { NodeSelection, type EditorState } from '@tiptap/pm/state'
import { isNodeRangeSelection } from '@tiptap/extension-node-range'

// What a block can be painted with, and the commands that act on "the blocks being worked on": the
// one the cursor is in, or every block of a selection that spans several.

export const BLOCK_COLORS = ['gray', 'orange', 'yellow', 'green', 'blue', 'purple', 'magenta', 'red'] as const
export type BlockColor = (typeof BLOCK_COLORS)[number]

export const BLOCK_ALIGNMENTS = ['left', 'center', 'right'] as const
export type BlockAlign = (typeof BLOCK_ALIGNMENTS)[number]

/** Nodes that take a text colour, a background and (the textual ones) an alignment. */
export const STYLED_NODES = ['paragraph', 'heading', 'blockquote', 'listItem', 'taskItem', 'callout', 'details', 'codeBlock']

/** The first paragraph of these is their own text, not a block of its own. */
const HEADED = ['listItem', 'taskItem', 'blockquote']

/** Positions of the blocks a block command acts on. */
export function targetBlocks(state: EditorState): number[] {
  const { selection, doc } = state
  const spansBlocks = isNodeRangeSelection(selection) || selection instanceof NodeSelection || !selection.$from.sameParent(selection.$to)
  if (spansBlocks) {
    // Each outermost block in the range: a list's items, not the paragraphs inside them.
    const out: number[] = []
    doc.nodesBetween(selection.from, selection.to, (node, pos) => {
      if (!STYLED_NODES.includes(node.type.name)) return true
      out.push(pos)
      return false
    })
    return out
  }
  const { $from } = selection
  for (let depth = $from.depth; depth > 0; depth--) {
    const node = $from.node(depth)
    if (!STYLED_NODES.includes(node.type.name)) continue
    const parent = $from.node(depth - 1)
    if (node.type.name === 'paragraph' && HEADED.includes(parent.type.name) && $from.index(depth - 1) === 0) continue
    return [$from.before(depth)]
  }
  return []
}

/** Sets one style attribute (`color`, `background`, `align`) on the blocks being worked on; null clears it. */
export function setBlockStyle(editor: Editor, attr: 'color' | 'background' | 'align', value: string | null): boolean {
  const positions = targetBlocks(editor.state)
  if (!positions.length) return false
  const { tr } = editor.state
  for (const pos of positions) {
    const node = tr.doc.nodeAt(pos)
    if (node) tr.setNodeMarkup(pos, undefined, { ...node.attrs, [attr]: value })
  }
  editor.view.dispatch(tr)
  return true
}

export const TURN_INTO = ['paragraph', 'h1', 'h2', 'h3', 'bulletList', 'numberedList', 'todo', 'quote', 'codeBlock'] as const
export type TurnInto = (typeof TURN_INTO)[number]

/** Turns the text blocks being worked on into another kind, keeping their text. */
export function turnInto(editor: Editor, kind: TurnInto): boolean {
  const chain = editor.chain().focus().clearNodes()
  switch (kind) {
    case 'paragraph':
      return chain.run()
    case 'h1':
    case 'h2':
    case 'h3':
      return chain.setNode('heading', { level: Number(kind[1]) }).run()
    case 'bulletList':
      return chain.toggleBulletList().run()
    case 'numberedList':
      return chain.toggleOrderedList().run()
    case 'todo':
      return chain.toggleTaskList().run()
    case 'quote':
      return chain.setBlockquote().run()
    case 'codeBlock':
      return chain.setCodeBlock().run()
  }
}
