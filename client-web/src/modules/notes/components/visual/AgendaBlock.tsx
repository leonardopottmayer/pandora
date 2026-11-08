import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQueryClient, type QueryKey } from '@tanstack/react-query'
import { Alert, Button, Select, Spin, Typography } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { useFeature, usePermission } from '@/modules/identity/hooks/useAccess'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { useToday } from '@/modules/agenda/hooks/useToday'
import { useTasks } from '@/modules/agenda/hooks/useTasks'
import { useTaskLists } from '@/modules/agenda/hooks/useTaskLists'
import { agendaKeys } from '@/modules/agenda/hooks/queryKeys'
import { formatDue } from '@/modules/agenda/lib/datetime'
import { EVENT_STATUS_META, REMINDER_STATUS_META, TASK_STATUS_META, type EnumMeta } from '@/modules/agenda/lib/enums'
import type { TodayItemKind } from '@/modules/agenda/models'
import { useVisualEditorContext } from './visualContext'
import { AgendaEvents } from './AgendaEvents'

interface AgendaBlockProps {
  props: Record<string, unknown>
  editable: boolean
  update: (props: Record<string, unknown>) => void
}

const DAY_STATUS: Record<TodayItemKind, Record<string, EnumMeta>> = {
  task: TASK_STATUS_META, event: EVENT_STATUS_META, reminder: REMINDER_STATUS_META,
}

/** Live Agenda data stays in the query cache; the note stores only source configuration. */
export function AgendaBlock({ props, editable, update }: AgendaBlockProps) {
  const { t } = useTranslation()
  const { publicView } = useVisualEditorContext()
  const client = useQueryClient()
  const enabled = useFeature('agenda')
  const tasks = usePermission('agenda.tasks.read')
  const events = usePermission('agenda.events.read')
  const reminders = usePermission('agenda.reminders.read')
  const lists = usePermission('agenda.lists.read')
  const calendars = usePermission('agenda.calendars.read')
  const canToday = tasks && events && reminders
  const canLists = tasks && lists
  const canEvents = events && calendars
  const mode = props.mode === 'tasks' || props.mode === 'events' ? props.mode : 'today'
  const allowed = { today: canToday, tasks: canLists, events: canEvents }[mode]
  const [refreshing, setRefreshing] = useState(false)

  // Guard before mounting any Agenda query, including when an owner's data is already cached.
  if (publicView) return <div contentEditable={false}><strong>{t('notes.agendaBlock.title')}</strong><p>{t('notes.agendaBlock.private')}</p></div>
  if (!enabled) return <p contentEditable={false}>{t('notes.agendaBlock.unavailable')}</p>

  async function refresh() {
    let keys: QueryKey[] = [agendaKeys.today()]
    if (mode === 'tasks') keys = [agendaKeys.tasks(), agendaKeys.taskLists()]
    else if (mode === 'events') keys = [agendaKeys.events(), agendaKeys.calendars()]
    setRefreshing(true)
    try {
      await Promise.all(keys.map((queryKey) => client.invalidateQueries({ queryKey })))
    } finally { setRefreshing(false) }
  }

  let body: ReactNode
  if (!allowed) body = <Typography.Text type="secondary">{t('notes.agendaBlock.unavailable')}</Typography.Text>
  else if (mode === 'today') body = <TodayItems />
  else if (mode === 'events') body = <AgendaEvents props={props} editable={editable} update={update} />
  else body = <ListItems listId={typeof props.listId === 'string' ? props.listId : null} editable={editable} onSelect={(listId) => update({ mode: 'tasks', listId })} />

  return (
    <section contentEditable={false} aria-label={t('notes.agendaBlock.title')} className="flex flex-col gap-3 rounded-lg border border-solid border-[var(--ant-color-border-secondary)] p-4">
      <div className="flex flex-wrap items-center gap-2">
        <strong>{t('notes.agendaBlock.title')}</strong>
        {editable && (canToday || canLists || canEvents) ? <Select
          aria-label={t('notes.agendaBlock.source')}
          value={mode}
          className="min-w-48"
          options={[
            { value: 'today', label: t('notes.agendaBlock.today'), disabled: !canToday },
            { value: 'tasks', label: t('notes.agendaBlock.tasks'), disabled: !canLists },
            { value: 'events', label: t('notes.agendaBlock.events'), disabled: !canEvents },
          ]}
          onChange={(value) => update({ mode: value })}
        /> : <Typography.Text type="secondary">{t(`notes.agendaBlock.${mode}`)}</Typography.Text>}
        {allowed && <Button type="text" size="small" icon={<ReloadOutlined />} aria-label={t('notes.agendaBlock.refresh')} loading={refreshing} onClick={() => void refresh()}>{t('notes.agendaBlock.refresh')}</Button>}
      </div>
      {body}
      {allowed && <Link to={{ today: '/agenda', tasks: '/agenda/tasks', events: '/agenda/calendar' }[mode]}>{t('notes.agendaBlock.open')}</Link>}
    </section>
  )
}

function TodayItems() {
  const { t, i18n } = useTranslation()
  const { timeZone } = usePreferences()
  const query = useToday()
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.agendaBlock.error'))} />
  if (query.isLoading) return <Spin size="small" />
  if (!query.data?.length) return <Typography.Text type="secondary">{t('notes.agendaBlock.emptyToday')}</Typography.Text>
  return <ul className="m-0 flex list-none flex-col gap-2 p-0">{query.data.map((item) => (
    <li key={`${item.kind}-${item.id}-${item.at}`} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 border-0 border-b border-solid border-[var(--ant-color-border-secondary)] pb-2">
      <span className="min-w-0 flex-1 break-words">{item.title}</span>
      <Typography.Text type="secondary" className="text-xs">{t(`notes.agendaBlock.kinds.${item.kind}`)}</Typography.Text>
      {item.status && DAY_STATUS[item.kind][item.status] && <Typography.Text type="secondary" className="text-xs">{t(DAY_STATUS[item.kind][item.status].labelKey)}</Typography.Text>}
      <Typography.Text type="secondary" className="text-xs tabular-nums">{item.isAllDay ? t('agenda.today.allDay') : formatDue(item.at, true, timeZone, i18n.language)}</Typography.Text>
    </li>
  ))}</ul>
}

function ListItems({ listId, editable, onSelect }: { listId: string | null; editable: boolean; onSelect: (id: string) => void }) {
  const { t } = useTranslation()
  const query = useTaskLists()
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.agendaBlock.error'))} />
  if (query.isLoading) return <Spin size="small" />
  const active = (query.data ?? []).filter((list) => !list.archivedAt)
  const selected = active.find((list) => list.id === listId)
  return <>
    {editable ? <Select
      aria-label={t('notes.agendaBlock.list')}
      value={selected?.id}
      placeholder={t('notes.agendaBlock.chooseList')}
      showSearch={{ optionFilterProp: 'label' }}
      options={active.map((list) => ({ value: list.id, label: list.name }))}
      onChange={onSelect}
    /> : selected && <Typography.Text strong>{selected.name}</Typography.Text>}
    {selected ? <PendingTasks listId={selected.id} /> : <Typography.Text type="secondary">{t(listId ? 'notes.agendaBlock.missingList' : 'notes.agendaBlock.chooseList')}</Typography.Text>}
  </>
}

function PendingTasks({ listId }: { listId: string }) {
  const { t, i18n } = useTranslation()
  const { timeZone } = usePreferences()
  const query = useTasks({ listId })
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.agendaBlock.error'))} />
  if (query.isLoading) return <Spin size="small" />
  const pending = (query.data ?? []).filter((task) => task.status !== 'Done' && task.status !== 'Cancelled')
  if (!pending.length) return <Typography.Text type="secondary">{t('notes.agendaBlock.emptyTasks')}</Typography.Text>
  return <ul className="m-0 flex list-none flex-col gap-2 p-0">{pending.map((task) => (
    <li key={task.id} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 border-0 border-b border-solid border-[var(--ant-color-border-secondary)] pb-2">
      <span className="min-w-0 flex-1 break-words">{task.title}</span>
      <Typography.Text type="secondary" className="text-xs">{t(TASK_STATUS_META[task.status].labelKey)}</Typography.Text>
      {task.dueAt && <Typography.Text type="secondary" className="text-xs">{formatDue(task.dueAt, task.dueHasTime, timeZone, i18n.language)}</Typography.Text>}
    </li>
  ))}</ul>
}
