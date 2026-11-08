import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Input, Segmented } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { useAskNotes } from '../hooks/useAskNotes'

interface AskNotesPanelProps {
  spaceId: string | null
  onSelect: (id: string) => void
}

/** A temporary answer with the passages behind it; opening a source returns to the page. */
export function AskNotesPanel({ spaceId, onSelect }: AskNotesPanelProps) {
  const { t } = useTranslation()
  const [question, setQuestion] = useState('')
  const [everySpace, setEverySpace] = useState(false)
  const controller = useRef<AbortController | null>(null)
  const mutation = useAskNotes()
  useEffect(() => () => controller.current?.abort(), [])

  function ask() {
    controller.current = new AbortController()
    mutation.mutate({ body: { question: question.trim(), spaceId: everySpace ? null : spaceId }, signal: controller.current.signal })
  }

  return (
    <div className="flex flex-col gap-3 p-3">
      <p className="m-0 text-sm text-[var(--ant-color-text-secondary)]">{t('notes.ask.hint')}</p>
      {spaceId && <Segmented
        size="small"
        value={everySpace ? 'all' : 'here'}
        disabled={mutation.isPending}
        options={[{ value: 'here', label: t('notes.spaces.thisSpace') }, { value: 'all', label: t('notes.spaces.all') }]}
        onChange={(value) => {
          setEverySpace(value === 'all')
          mutation.reset()
        }}
      />}
      <Input.TextArea
        autoFocus
        aria-label={t('notes.ask.question')}
        placeholder={t('notes.ask.placeholder')}
        value={question}
        rows={3}
        maxLength={1000}
        disabled={mutation.isPending}
        onChange={(event) => {
          setQuestion(event.target.value)
          mutation.reset()
        }}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && !event.shiftKey && question.trim()) {
            event.preventDefault()
            ask()
          }
        }}
      />
      <Button type="primary" className="self-end" loading={mutation.isPending} disabled={!question.trim()} onClick={ask}>
        {t('notes.ask.submit')}
      </Button>
      {mutation.isError && <Alert type="error" title={toErrorMessage(mutation.error, t('notes.ai.error'))} />}
      {mutation.data && (
        <div className="flex max-h-[50dvh] flex-col gap-3 overflow-y-auto">
          {mutation.data.answer ? (
            <>
              <div className="whitespace-pre-wrap break-words rounded-lg border border-solid border-[var(--ant-color-border-secondary)] p-3" role="region" aria-label={t('notes.ask.answer')}>
                {mutation.data.answer}
              </div>
              <p className="m-0 text-sm text-[var(--ant-color-text-secondary)]">{t('notes.ask.sources')}</p>
              {mutation.data.sources.map((source) => (
                <div key={source.pageId} className="rounded-lg border border-solid border-[var(--ant-color-border-secondary)] p-3">
                  <button type="button" className="cursor-pointer border-0 bg-transparent p-0 text-left text-[var(--ant-color-link)]" onClick={() => onSelect(source.pageId)}>
                    {source.icon ?? '📄'} {source.title}
                  </button>
                  <p className="mb-0 mt-1 whitespace-pre-wrap break-words text-xs text-[var(--ant-color-text-secondary)]">{source.excerpt}</p>
                </div>
              ))}
            </>
          ) : <Alert type="info" title={t('notes.ask.noInformation')} />}
        </div>
      )}
    </div>
  )
}
