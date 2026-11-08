import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Input } from 'antd'
import type { NodeViewProps } from '@tiptap/react'
import type { Node } from '@tiptap/pm/model'
import { toErrorMessage } from '@/lib/api/envelope'
import { useGenerateText } from '../../hooks/useGenerateText'
import { AI_EDIT_META } from '../../lib/aiText'
import { plainText, type Inline } from '../../lib/blocks'
import { timeAgo } from '../../lib/timeAgo'
import { useVisualEditorContext } from './visualContext'

/** An AI block keeps its accepted output while another generation waits for review. */
export function AiBlock({ node, editor, getPos, updateAttributes }: Pick<NodeViewProps, 'node' | 'editor' | 'getPos' | 'updateAttributes'>) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const props = (node.attrs.props ?? {}) as Record<string, unknown>
  const prompt = String(props.prompt ?? '')
  const output = plainText((props.output as Inline[] | undefined) ?? [])
  const generatedAt = typeof props.generatedAt === 'string' && Number.isFinite(Date.parse(props.generatedAt)) ? props.generatedAt : null
  const { editable } = useVisualEditorContext()
  const controller = useRef<AbortController | null>(null)
  const snapshot = useRef<Node | null>(null)
  const mutation = useGenerateText()
  useEffect(() => () => controller.current?.abort(), [])

  function generate() {
    if (!editor.isEditable) return
    snapshot.current = node
    controller.current = new AbortController()
    mutation.mutate({ prompt: prompt.trim(), signal: controller.current.signal })
  }

  function accept() {
    const pos = getPos()
    const current = pos === undefined || editor.isDestroyed ? null : editor.state.doc.nodeAt(pos)
    if (!mutation.data || !current || !editor.isEditable || !snapshot.current?.eq(current)) {
      message.warning(t('notes.ai.documentChanged'))
      return
    }
    const data = mutation.data
    editor.chain().setMeta(AI_EDIT_META, true).command(({ tr }) => {
      tr.setNodeMarkup(pos!, undefined, { ...current.attrs, props: {
        ...current.attrs.props, output: [{ text: data.text }], generatedAt: data.generatedAt,
      } })
      return true
    }).run()
    mutation.reset()
  }

  return (
    <div contentEditable={false} className="flex flex-col gap-3 rounded-lg border border-solid border-[var(--ant-color-border-secondary)] p-4">
      <strong>{t('notes.aiBlock.title')}</strong>
      {editable ? <Input.TextArea
        className="notes-ai-controls"
        aria-label={t('notes.aiBlock.prompt')}
        placeholder={t('notes.aiBlock.placeholder')}
        value={prompt}
        rows={3}
        maxLength={8000}
        disabled={mutation.isPending}
        onChange={(event) => {
          if (!editor.isEditable) return
          updateAttributes({ props: { ...props, prompt: event.target.value } })
          mutation.reset()
        }}
      /> : prompt && <p className="notes-ai-prompt m-0 whitespace-pre-wrap break-words text-sm text-[var(--ant-color-text-secondary)]">{prompt}</p>}
      {output ? <div className="whitespace-pre-wrap break-words">{output}</div> : <p className="m-0 text-sm text-[var(--ant-color-text-secondary)]">{t('notes.aiBlock.empty')}</p>}
      {generatedAt && <time dateTime={generatedAt} className="text-xs text-[var(--ant-color-text-secondary)]">{t('notes.aiBlock.generated', { time: timeAgo(generatedAt, i18n.language) })}</time>}
      {editable && <div className="notes-ai-controls flex flex-col gap-3">
        <p className="m-0 text-xs text-[var(--ant-color-text-secondary)]">{t('notes.aiBlock.hint')}</p>
        {mutation.isError && <Alert type="error" title={toErrorMessage(mutation.error, t('notes.ai.error'))} />}
        {mutation.data ? <div className="flex flex-col gap-3 border-t border-solid border-[var(--ant-color-border-secondary)] pt-3">
          <strong>{t('notes.aiBlock.preview')}</strong>
          <div className="whitespace-pre-wrap break-words">{mutation.data.text}</div>
          <div className="flex flex-wrap justify-end gap-2">
            <Button onClick={() => mutation.reset()}>{t('notes.ai.discard')}</Button>
            <Button type="primary" onClick={accept}>{t('notes.aiBlock.accept')}</Button>
          </div>
        </div> : <Button type="primary" className="self-end" loading={mutation.isPending} disabled={!prompt.trim() || prompt.length > 8000} onClick={generate}>
          {t(output ? 'notes.aiBlock.regenerate' : 'notes.ai.generate')}
        </Button>}
      </div>}
    </div>
  )
}
