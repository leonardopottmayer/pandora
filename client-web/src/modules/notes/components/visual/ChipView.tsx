import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { NodeViewWrapper, type NodeViewProps } from '@tiptap/react'
import dayjs from 'dayjs'
import { App, Checkbox, DatePicker, Input, Popover, Switch } from 'antd'
import { createReminder, deleteReminder } from '@/modules/agenda/services/reminders.service'
import { toErrorMessage } from '@/lib/api/envelope'
import { BLOCK_COLORS } from '../../lib/blockStyle'
import type { ChipData, DateChip, ItemChip, MathChip, StatusChip } from '../../lib/blocks'
import { formatChipDate, hasTime, reminderMoment } from '../../lib/chipDates'
import { loadKatex } from '../../lib/lazyRenderers'
import { ITEM_ICONS, itemRoute } from '../../services/moduleItems.service'
import { useVisualEditorContext } from './visualContext'

// A chip in the text: a date (and its Agenda reminder), a status, an item of another module, or a
// formula. A click opens what changes it while the page is being written.

function InlineFormula({ latex }: { latex: string }) {
  const [html, setHtml] = useState<string | null>(null)
  useEffect(() => {
    let current = true
    void loadKatex().then((katex) => {
      const out = katex.renderToString(latex, { displayMode: false, throwOnError: false, trust: false, maxSize: 20, maxExpand: 500 })
      if (current) setHtml(out)
    })
    return () => {
      current = false
    }
  }, [latex])
  return html ? <span dangerouslySetInnerHTML={{ __html: html }} /> : <span>{latex}</span>
}

export function ChipView({ node, editor, getPos, updateAttributes }: NodeViewProps) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const { pagesById, pageId } = useVisualEditorContext()
  const kind = node.attrs.kind as keyof ChipData
  const data = (node.attrs.data ?? {}) as Record<string, unknown>
  const editable = editor.isEditable
  const set = (next: Record<string, unknown>) => updateAttributes({ data: next })

  /** What a reminder is called: the line the date is in, or the page when the line says nothing else. */
  function reminderTitle(): string {
    const pos = getPos()
    const line = typeof pos === 'number' ? editor.state.doc.resolve(pos).parent.textContent.replace(/\s+/g, ' ').trim() : ''
    const page = pagesById.get(pageId)
    return line || page?.title || t('notes.untitled')
  }

  async function setReminder(date: DateChip, on: boolean) {
    try {
      if (date.reminderId) await deleteReminder(date.reminderId).catch(() => undefined)
      if (!on) {
        set({ value: date.value })
        return
      }
      const reminder = await createReminder({
        title: reminderTitle(),
        remindAt: reminderMoment(date.value),
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      })
      set({ value: date.value, reminderId: reminder.id })
      message.success(t('notes.chips.reminderSet'))
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.chips.reminderError')))
    }
  }

  let face: React.ReactNode
  let panel: React.ReactNode = null

  if (kind === 'date') {
    const date = data as unknown as DateChip
    const words = (word: 'today' | 'tomorrow' | 'yesterday') => t(`notes.chips.${word}`)
    face = (
      <>
        {date.reminderId ? '⏰' : '📅'} {formatChipDate(date.value, i18n.language, words)}
      </>
    )
    const timed = hasTime(date.value)
    panel = (
      <div className="notes-chip-editor">
        <DatePicker
          value={dayjs(date.value)}
          showTime={timed ? { format: 'HH:mm' } : false}
          format={timed ? 'DD/MM/YYYY HH:mm' : 'DD/MM/YYYY'}
          allowClear={false}
          aria-label={t('notes.chips.date')}
          onChange={(value) => {
            if (!value) return
            const next = { value: value.format(timed ? 'YYYY-MM-DDTHH:mm' : 'YYYY-MM-DD') }
            if (date.reminderId) void setReminder({ ...next, reminderId: date.reminderId }, true)
            else set(next)
          }}
        />
        <Checkbox
          checked={timed}
          onChange={(event) => set({ ...date, value: event.target.checked ? `${date.value}T09:00` : date.value.slice(0, 10) })}
        >
          {t('notes.chips.withTime')}
        </Checkbox>
        <label className="notes-chip-row">
          <Switch size="small" checked={!!date.reminderId} onChange={(on) => void setReminder(date, on)} />
          {t('notes.chips.remind')}
        </label>
      </div>
    )
  } else if (kind === 'status') {
    const status = data as unknown as StatusChip
    face = status.label || t('notes.chips.status')
    panel = (
      <div className="notes-chip-editor">
        <Input size="small" autoFocus value={status.label} aria-label={t('notes.chips.status')} onChange={(event) => set({ ...status, label: event.target.value })} />
        <div className="notes-chip-colors">
          {BLOCK_COLORS.map((color) => (
            <button
              key={color}
              type="button"
              className="notes-swatch notes-swatch-background"
              data-swatch={color}
              aria-pressed={status.color === color}
              aria-label={t(`notes.colors.${color}`)}
              onClick={() => set({ ...status, color })}
            />
          ))}
        </div>
      </div>
    )
  } else if (kind === 'item') {
    const item = data as unknown as ItemChip
    face = (
      <>
        <span aria-hidden="true">{ITEM_ICONS[item.kind] ?? '🔗'}</span> {item.label}
      </>
    )
  } else {
    const math = data as unknown as MathChip
    face = <InlineFormula latex={math.latex || '?'} />
    panel = (
      <div className="notes-chip-editor">
        <Input size="small" autoFocus value={math.latex} aria-label={t('notes.advanced.mathSource')} className="notes-source-field" onChange={(event) => set({ latex: event.target.value })} />
      </div>
    )
  }

  const route = kind === 'item' ? itemRoute(data as unknown as ItemChip) : null
  const chip = (
    <span
      className={`notes-chip notes-chip-${kind}`}
      data-color={kind === 'status' ? ((data.color as string | undefined) ?? 'gray') : undefined}
      role={route ? 'link' : undefined}
      tabIndex={route ? 0 : undefined}
      onClick={() => route && navigate(route)}
      onKeyDown={(event) => event.key === 'Enter' && route && navigate(route)}
    >
      {face}
    </span>
  )

  return (
    <NodeViewWrapper as="span" className="notes-chip-host">
      {editable && panel ? (
        <Popover trigger="click" content={panel} destroyOnHidden>
          {chip}
        </Popover>
      ) : chip}
    </NodeViewWrapper>
  )
}
