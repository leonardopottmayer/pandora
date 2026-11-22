import { afterEach, describe, expect, it } from 'vitest'
import { Editor } from '@tiptap/core'
import { NodeRangeSelection } from '@tiptap/extension-node-range'
import { TextSelection } from '@tiptap/pm/state'
import { toBlocks, toEditorDoc, type Block } from './blocks'
import { setBlockStyle, targetBlocks, turnInto } from './blockStyle'
import { visualExtensions } from './visualSchema'

let editor: Editor | null = null
afterEach(() => editor?.destroy())

function open(blocks: Block[]) {
  editor = new Editor({ extensions: visualExtensions(), content: toEditorDoc(blocks) })
  return editor
}

/** Puts the cursor inside the text `inside`. */
function cursorIn(instance: Editor, inside: string) {
  let at = -1
  instance.state.doc.descendants((node, pos) => {
    if (at < 0 && node.isText && node.text?.includes(inside)) at = pos + 1
  })
  instance.view.dispatch(instance.state.tr.setSelection(TextSelection.create(instance.state.doc, at)))
}

/** What the page saves: the editor's own empty paragraph at the end left out, as the editor does. */
const saved = (instance: Editor) => {
  const blocks = toBlocks(instance.getJSON().content)
  const last = blocks.at(-1)
  if (last?.type === 'paragraph' && !last.content && !last.props) blocks.pop()
  return blocks
}
const para = (id: string, text: string): Block => ({ id, type: 'paragraph', content: [{ text }] })

describe('block commands', () => {
  it('paint the list item the cursor is in, not the paragraph inside it', () => {
    const instance = open([{ id: 'a', type: 'bulleted', content: [{ text: 'item' }] }])
    cursorIn(instance, 'item')
    setBlockStyle(instance, 'color', 'red')
    expect(saved(instance)).toEqual([{ id: 'a', type: 'bulleted', props: { color: 'red' }, content: [{ text: 'item' }] }])
  })

  it('paint the paragraph inside a callout, not the callout', () => {
    const instance = open([{ id: 'c', type: 'callout', props: { tone: 'info' }, children: [para('p', 'dentro')] }])
    cursorIn(instance, 'dentro')
    setBlockStyle(instance, 'background', 'yellow')
    expect(saved(instance)[0].children).toEqual([{ id: 'p', type: 'paragraph', props: { background: 'yellow' }, content: [{ text: 'dentro' }] }])
  })

  it('act on every block of a block selection', () => {
    const instance = open([para('a', 'um'), para('b', 'dois'), para('c', 'três')])
    const { doc } = instance.state
    const second = doc.child(0).nodeSize + doc.child(1).nodeSize
    instance.view.dispatch(instance.state.tr.setSelection(NodeRangeSelection.create(doc, 1, second - 1)))
    expect(targetBlocks(instance.state)).toHaveLength(2)

    setBlockStyle(instance, 'align', 'center')
    expect(saved(instance).map((block) => block.props?.align ?? null)).toEqual(['center', 'center', null])
  })

  it('clear a style with null', () => {
    const instance = open([{ id: 'a', type: 'paragraph', props: { color: 'red' }, content: [{ text: 'x' }] }])
    cursorIn(instance, 'x')
    setBlockStyle(instance, 'color', null)
    expect(saved(instance)).toEqual([para('a', 'x')])
  })

  it('turn a list item into a heading and back into a list, keeping the text', () => {
    const instance = open([{ id: 'a', type: 'bulleted', content: [{ text: 'tema' }] }])
    cursorIn(instance, 'tema')
    turnInto(instance, 'h2')
    expect(saved(instance)).toMatchObject([{ type: 'heading', props: { level: 2 }, content: [{ text: 'tema' }] }])
    turnInto(instance, 'todo')
    expect(saved(instance)).toMatchObject([{ type: 'todo', content: [{ text: 'tema' }] }])
  })
})
