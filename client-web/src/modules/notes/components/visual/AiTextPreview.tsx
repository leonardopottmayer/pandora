import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Input, Modal } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { MAX_AI_TEXT, type AiTextTarget } from '../../lib/aiText'
import { useTextTransform } from '../../hooks/useTextTransform'

interface AiTextPreviewProps {
  target: AiTextTarget
  pageId?: string
  onApply: (text: string, placement: 'replace' | 'below') => void
  onClose: () => void
}

/** Keeps generated text outside the document until the user accepts the preview. */
export function AiTextPreview({ target, pageId, onApply, onClose }: AiTextPreviewProps) {
  const { t, i18n } = useTranslation()
  const [language, setLanguage] = useState(i18n.language.startsWith('pt') ? 'English' : 'Português')
  const controller = useRef<AbortController | null>(null)
  const mutation = useTextTransform()
  function generate() {
    controller.current = new AbortController()
    mutation.mutate({
      body: {
        action: target.action,
        text: target.text,
        context: target.context,
        language: target.action === 'translate' ? language.trim() : undefined,
      },
      signal: controller.current.signal,
      pageId: target.pageSummary ? pageId : undefined,
    })
  }
  useEffect(() => () => controller.current?.abort(), [])

  const invalid = !target.pageSummary && (target.text.length > MAX_AI_TEXT || !(target.text.trim() || target.context?.trim()))
  const generated = mutation.data?.text
  return (
    <Modal
      open
      title={t(target.pageSummary ? 'notes.ai.summarizePage' : `notes.ai.${target.action}`)}
      onCancel={onClose}
      width={640}
      styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto' } }}
      footer={
        <div className="flex flex-wrap justify-end gap-2">
          <Button onClick={onClose}>{t('notes.ai.discard')}</Button>
          {generated ? (
            <>
              <Button type={target.pageSummary || target.action === 'continue' ? 'primary' : 'default'} onClick={() => onApply(generated, 'below')}>{t(target.pageSummary ? 'notes.ai.insertSummary' : 'notes.ai.insertBelow')}</Button>
              {!target.pageSummary && target.action !== 'continue' && <Button type="primary" onClick={() => onApply(generated, 'replace')}>{t('notes.ai.replace')}</Button>}
            </>
          ) : (
            <Button type="primary" loading={mutation.isPending} disabled={invalid || (target.action === 'translate' && !language.trim())} onClick={generate}>
              {t('notes.ai.generate')}
            </Button>
          )}
        </div>
      }
    >
      <div className="flex flex-col gap-4">
        <p className="m-0 text-sm text-[var(--ant-color-text-secondary)]">{t(target.pageSummary ? 'notes.ai.pageSummaryHint' : 'notes.ai.previewHint')}</p>
        {invalid && <Alert type="warning" title={t('notes.ai.textLimit')} />}
        {target.action === 'translate' && (
          <label>
            <span className="mb-1 block">{t('notes.ai.language')}</span>
            <Input value={language} maxLength={80} disabled={mutation.isPending} onChange={(event) => {
              setLanguage(event.target.value)
              mutation.reset()
            }} />
          </label>
        )}
        {mutation.isError && <Alert type="error" title={toErrorMessage(mutation.error, t('notes.ai.error'))} />}
        <div className="whitespace-pre-wrap break-words rounded-lg border border-solid border-[var(--ant-color-border-secondary)] bg-[var(--ant-color-fill-alter)] p-4">
          {generated ?? (target.pageSummary ? t('notes.ai.pageSummaryBeforeGenerate') : target.text || target.context)}
        </div>
      </div>
    </Modal>
  )
}
