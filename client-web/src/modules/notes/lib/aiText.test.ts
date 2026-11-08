import { afterEach, describe, expect, it } from 'vitest'
import { Editor } from '@tiptap/core'
import { aiTextTarget, applyAiText } from './aiText'
import { visualExtensions } from './visualSchema'
import { toBlocks, toEditorDoc } from './blocks'

let editor: Editor
afterEach(() => editor?.destroy())

function start(text = 'Original text') {
  editor = new Editor({ extensions: visualExtensions(), content: toEditorDoc([{ id: 'p1', type: 'paragraph', content: [{ text }] }]) })
  editor.commands.setTextSelection({ from: 1, to: text.length + 1 })
  return editor
}

describe('AI writing in the editor', () => {
  it('puts an accepted page summary in a callout at the top, keeping the original blocks', () => {
    start()
    const target = { ...aiTextTarget(editor, 'summarize'), pageSummary: true }
    expect(applyAiText(editor, target, 'Summary line\nNext line', 'below', 'Summary')).toBe(true)
    const blocks = toBlocks(editor.getJSON().content)
    expect(blocks[0]).toMatchObject({
      type: 'callout', props: { tone: 'info', title: 'Summary' },
      children: [
        { type: 'paragraph', content: [{ text: 'Summary line' }] },
        { type: 'paragraph', content: [{ text: 'Next line' }] },
      ],
    })
    expect(blocks[1]).toMatchObject({ id: 'p1', content: [{ text: 'Original text' }] })
    const ids = [blocks[0].id, ...blocks[0].children!.map((b) => b.id), blocks[1].id]
    expect(new Set(ids).size).toBe(ids.length)
  })

  it('only replaces the captured selection and treats generated HTML as plain text', () => {
    start()
    editor.commands.setTextSelection({ from: 1, to: 9 })
    const target = aiTextTarget(editor, 'improve')
    editor.commands.setTextSelection(12)
    expect(applyAiText(editor, target, '<img src=x onerror=alert(1)>', 'replace')).toBe(true)
    expect(editor.getText()).toBe('<img src=x onerror=alert(1)> text')
    expect(editor.getHTML()).not.toContain('<img')
    expect(toBlocks(editor.getJSON().content)[0].id).toBe('p1')
  })

  it('inserts new paragraphs below without replacing the original, with distinct ids', () => {
    start()
    expect(applyAiText(editor, aiTextTarget(editor, 'summarize'), 'Summary\nAnother line', 'below')).toBe(true)
    const blocks = toBlocks(editor.getJSON().content)
    expect(blocks.map((b) => b.content)).toEqual([[{ text: 'Original text' }], [{ text: 'Summary' }], [{ text: 'Another line' }]])
    expect(new Set(blocks.map((b) => b.id)).size).toBe(3)
  })

  it('rejects a stale preview or a page that has become read only', () => {
    start()
    const target = aiTextTarget(editor, 'improve')
    editor.commands.insertContent('Changed')
    expect(applyAiText(editor, target, 'Generated', 'replace')).toBe(false)
    const next = aiTextTarget(editor, 'improve')
    editor.setEditable(false)
    expect(applyAiText(editor, next, 'Generated', 'below')).toBe(false)
  })

  it('continues from only the last 8000 characters before the cursor', () => {
    start('x'.repeat(9000) + 'After')
    editor.commands.setTextSelection(9001)
    const target = aiTextTarget(editor, 'continue')
    expect(target.text).toBe('')
    expect(target.context).toBe('x'.repeat(8000))
  })
})
