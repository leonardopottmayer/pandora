import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Checkbox, DatePicker, InputNumber, Radio, Select, Space } from 'antd'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { fromPicker, toPicker } from '../lib/datetime'
import {
  buildRrule,
  parseRrule,
  WEEKDAYS,
  type EndMode,
  type Frequency,
  type RecurrenceValue,
} from '../lib/rrule'

interface RecurrencePickerProps {
  /** Current RRULE string (null = does not repeat). */
  value: string | null | undefined
  onChange: (rrule: string | null) => void
  /** Tasks reject COUNT — hide the "after N occurrences" option when false. */
  allowCount?: boolean
}

const FREQUENCIES: Frequency[] = ['none', 'daily', 'weekly', 'monthly', 'yearly']

/** Simple RFC 5545 subset picker shared by events, tasks and reminders. */
export function RecurrencePicker({ value, onChange, allowCount = true }: RecurrencePickerProps) {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const [state, setState] = useState<RecurrenceValue>(() => parseRrule(value))
  const [seededFrom, setSeededFrom] = useState<string | null | undefined>(value)

  // Re-seed when the external value changes (e.g. opening the modal on a new entity).
  // Adjusting state during render (not in an effect) is the recommended pattern for
  // deriving state from a changing prop.
  if (value !== seededFrom) {
    setSeededFrom(value)
    setState(parseRrule(value))
  }

  function update(patch: Partial<RecurrenceValue>) {
    const next = { ...state, ...patch }
    setState(next)
    onChange(buildRrule(next))
  }

  const endModes: EndMode[] = allowCount ? ['never', 'until', 'count'] : ['never', 'until']

  return (
    <div className="flex min-w-0 flex-col gap-2">
      <Select<Frequency>
        value={state.frequency}
        onChange={(frequency) => update({ frequency })}
        options={FREQUENCIES.map((f) => ({ value: f, label: t(`agenda.recurrence.${f}`) }))}
        style={{ width: '100%' }}
        aria-label={t('agenda.recurrence.frequency')}
      />

      {state.frequency !== 'none' && (
        <>
          <Space wrap>
            <span>{t('agenda.recurrence.interval')}</span>
            <InputNumber
              min={1}
              value={state.interval}
              onChange={(interval) => update({ interval: interval ?? 1 })}
              aria-label={t('agenda.recurrence.interval')}
            />
            <span>{t(`agenda.recurrence.intervalSuffix.${state.frequency}`)}</span>
          </Space>

          {state.frequency === 'weekly' && (
            <Checkbox.Group
              value={state.byDay}
              onChange={(byDay) => update({ byDay: byDay as RecurrenceValue['byDay'] })}
              options={WEEKDAYS.map((d) => ({ value: d, label: t(`agenda.recurrence.day.${d}`) }))}
            />
          )}

          <Radio.Group
            className="flex flex-wrap gap-2"
            value={state.endMode}
            onChange={(event) => update({ endMode: event.target.value as EndMode })}
            options={endModes.map((m) => ({
              value: m,
              label: t(
                m === 'never'
                  ? 'agenda.recurrence.endNever'
                  : m === 'until'
                    ? 'agenda.recurrence.endUntil'
                    : 'agenda.recurrence.endCount',
              ),
            }))}
          />

          {state.endMode === 'until' && (
            <DatePicker
              format="DD/MM/YYYY"
              value={state.until ? toPicker(state.until, timeZone) : null}
              // Until the end of the picked day in the account zone, so that day's occurrence is kept.
              onChange={(d) => update({ until: d ? fromPicker(d.endOf('day'), timeZone) : null })}
              style={{ width: '100%' }}
              aria-label={t('agenda.recurrence.endUntil')}
            />
          )}

          {state.endMode === 'count' && (
            <Space wrap>
              <InputNumber
                min={1}
                value={state.count ?? 1}
                onChange={(count) => update({ count: count ?? 1 })}
                aria-label={t('agenda.recurrence.endCount')}
              />
              <span>{t('agenda.recurrence.occurrences')}</span>
            </Space>
          )}
        </>
      )}
    </div>
  )
}
