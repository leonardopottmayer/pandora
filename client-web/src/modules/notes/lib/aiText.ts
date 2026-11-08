import type { Editor, JSONContent } from '@tiptap/core'
import type { Node } from '@tiptap/pm/model'
import type { TextAction } from '../models'

export const MAX_AI_TEXT = 8000
export const AI_EDIT_META = 'notesAiEdit'

/** A preview belongs to this document and selection, even if focus moves to the dialog. */
export interface AiTextTarget {
  pageSummary?: boolean
  doc: Node
  from: number
  to: number
  below: number
  action: TextAction
  text: string
  context?: string
}

export function aiTextTarget(editor: Editor, action: TextAction): AiTextTarget {
  const { doc, selection } = editor.state
  const { from, to, $to } = selection
  return {
    doc, from, to, action,
    below: $to.depth > 0 ? $to.after(1) : to,
    text: action === 'continue' ? '' : doc.textBetween(from, to, '\n'),
    context: action === 'continue' ? doc.textBetween(0, from, '\n').slice(-MAX_AI_TEXT) : undefined,
  }
}

/** Uses JSON text nodes so generated HTML or markdown can never become executable editor content. */
export function applyAiText(editor: Editor, target: AiTextTarget, text: string, placement: 'replace' | 'below', summaryTitle?: string): boolean {
  if (editor.isDestroyed || !editor.isEditable || !editor.state.doc.eq(target.doc) || !text.trim()) return false
  const lines = text.replace(/\r\n?/g, '\n').split('\n')
  const paragraphs: JSONContent[] = lines.map((line) => ({
    type: 'paragraph', content: line ? [{ type: 'text', text: line }] : [],
  }))
  if (target.pageSummary) {
    return editor.chain().focus().setMeta(AI_EDIT_META, true).insertContentAt(0, {
      type: 'callout', attrs: { title: summaryTitle, calloutType: 'info' }, content: paragraphs,
    }).run()
  }
  const content = placement === 'replace' && lines.length === 1
    ? [{ type: 'text', text: lines[0] }]
    : paragraphs
  return editor.chain().focus().setMeta(AI_EDIT_META, true)
    .insertContentAt(placement === 'below' ? target.below : { from: target.from, to: target.to }, content).run()
}
