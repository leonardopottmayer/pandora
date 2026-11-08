import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Select, Spin, Typography } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { useCalendars } from '@/modules/agenda/hooks/useCalendars'
import { useEventOccurrences } from '@/modules/agenda/hooks/useEvents'
import { formatEventRange } from '@/modules/agenda/lib/datetime'
import { agendaEventWindow } from '../../lib/agendaEventWindow'

/** Recurrences and overrides are resolved by Agenda; Notes persists only days and calendar identity. */
export function AgendaEvents({ props, editable, update }: {
  props: Record<string, unknown>
  editable: boolean
  update: (props: Record<string, unknown>) => void
}) {
  const { t } = useTranslation()
  const query = useCalendars()
  const days = props.days === 30 ? 30 : 7
  const calendarId = typeof props.calendarId === 'string' ? props.calendarId : null
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.agendaBlock.error'))} />
  if (query.isLoading) return <Spin size="small" />
  const active = (query.data ?? []).filter((calendar) => !calendar.archivedAt)
  const selected = active.find((calendar) => calendar.id === calendarId)
  const selectedIds = selected ? [selected.id] : []
  const calendarIds = calendarId ? selectedIds : active.map((calendar) => calendar.id).sort()
  const label = calendarId ? selected?.name : t('notes.agendaBlock.allCalendars')
  let body: ReactNode
  if (calendarId && !selected) body = <Typography.Text type="secondary">{t('notes.agendaBlock.missingCalendar')}</Typography.Text>
  else if (!calendarIds.length) body = <Typography.Text type="secondary">{t('notes.agendaBlock.noCalendars')}</Typography.Text>
  else body = <EventItems days={days} calendarIds={calendarIds} />
  return <>
    {editable ? <div className="flex flex-wrap gap-2">
      <Select aria-label={t('notes.agendaBlock.period')} value={days} className="min-w-48"
        options={[7, 30].map((value) => ({ value, label: t('notes.agendaBlock.nextDays', { count: value }) }))}
        onChange={(value) => update({ mode: 'events', days: value, ...(calendarId ? { calendarId } : {}) })} />
      <Select aria-label={t('notes.agendaBlock.calendar')} value={selected?.id ?? (calendarId ? undefined : '')}
        placeholder={t('notes.agendaBlock.chooseCalendar')} className="min-w-48" showSearch={{ optionFilterProp: 'label' }}
        options={[{ value: '', label: t('notes.agendaBlock.allCalendars') }, ...active.map((calendar) => ({ value: calendar.id, label: calendar.name }))]}
        onChange={(value) => update({ mode: 'events', days, ...(value ? { calendarId: value } : {}) })} />
    </div> : <Typography.Text strong>{t('notes.agendaBlock.nextDays', { count: days })}{label && <> · {label}</>}</Typography.Text>}
    <Typography.Text type="secondary" className="text-xs">{t('notes.agendaBlock.eventHint')}</Typography.Text>
    {body}
  </>
}

function EventItems({ days, calendarIds }: { days: 7 | 30; calendarIds: string[] }) {
  const { t, i18n } = useTranslation()
  const { timeZone } = usePreferences()
  const now = new Date()
  const window = agendaEventWindow(days, timeZone, now)
  const query = useEventOccurrences({ ...window, calendarIds })
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.agendaBlock.error'))} />
  if (query.isLoading) return <Spin size="small" />
  const upcoming = (query.data ?? []).filter((event) => calendarIds.includes(event.calendarId)
    && event.status !== 'Cancelled' && new Date(event.endsAt) > now && new Date(event.startsAt) < new Date(window.to))
    .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime())
  if (!upcoming.length) return <Typography.Text type="secondary">{t('notes.agendaBlock.emptyEvents')}</Typography.Text>
  return <ul className="m-0 flex list-none flex-col gap-2 p-0">{upcoming.map((event) => (
    <li key={`${event.eventId}-${event.originalStartsAt}`} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 border-0 border-b border-solid border-[var(--ant-color-border-secondary)] pb-2">
      <span className="min-w-0 flex-1 break-words">{event.title}</span>
      <Typography.Text type="secondary" className="text-xs tabular-nums">{formatEventRange(event.startsAt, event.endsAt, event.isAllDay, timeZone, i18n.language)}</Typography.Text>
      {event.isAllDay && <Typography.Text type="secondary" className="text-xs">{t('agenda.today.allDay')}</Typography.Text>}
    </li>
  ))}</ul>
}
