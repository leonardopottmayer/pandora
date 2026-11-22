import { Fragment, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Checkbox, Empty, Skeleton, Typography, theme } from 'antd'
import { BellOutlined, CalendarOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import type { EventOccurrenceDto, ReminderStatus, TaskDto, TodayItemDto } from '../../models'
import { dayKey, daysFromToday, formatDue, formatTime, occurrenceDays } from '../../lib/datetime'
import { SnoozeModal } from '../../components/SnoozeModal'
import { useToday } from '../../hooks/useToday'
import { useTasks, useCompleteTask, useReopenTask } from '../../hooks/useTasks'
import { useAcknowledgeReminder } from '../../hooks/useReminders'
import { useCalendars } from '../../hooks/useCalendars'
import { useEventOccurrences } from '../../hooks/useEvents'
import { EventDetailModal } from '../calendar/EventDetailModal'

/** A reminder still waiting on the user: it can be acknowledged or snoozed. */
const PENDING_REMINDER: string[] = ['Scheduled', 'Notified', 'Snoozed'] satisfies ReminderStatus[]

type Row =
  | { kind: 'event'; key: string; at: string; allDay: boolean; occ: EventOccurrenceDto }
  | { kind: 'task' | 'reminder'; key: string; at: string; allDay: boolean; item: TodayItemDto }

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

export function TodayPage() {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const { timeZone } = usePreferences()
  const [snoozeId, setSnoozeId] = useState<string | null>(null)
  const [detail, setDetail] = useState<EventOccurrenceDto | null>(null)

  const now = dayjs()
  const today = dayKey(now, timeZone)

  // Tasks and reminders come from the day view; events from the occurrences, which carry what the
  // event card needs to edit one. Overdue tasks are not in the day view, so they come from the tasks.
  const { data: dayItems, isLoading, error: todayError } = useToday()
  const { data: occurrences, error: occurrenceError } = useEventOccurrences({
    from: dayjs.tz(today, timeZone).toISOString(),
    to: dayjs.tz(today, timeZone).add(1, 'day').toISOString(),
  })
  const { data: tasks, error: taskError } = useTasks()
  const { data: calendarList, error: calendarError } = useCalendars()
  const calendars = (calendarList ?? []).filter((c) => !c.archivedAt)
  const calendarOf = new Map(calendars.map((c) => [c.id, c]))

  const completeTask = useCompleteTask()
  const reopenTask = useReopenTask()
  const acknowledge = useAcknowledgeReminder()

  const overdue = (tasks ?? [])
    .filter((task) => task.status !== 'Done' && task.status !== 'Cancelled' && task.dueAt)
    .filter((task) => daysFromToday(task.dueAt!, timeZone) < 0)
    .sort((a, b) => a.dueAt!.localeCompare(b.dueAt!))

  // All-day first, then by time. An event that began on an earlier day reads as all-day today.
  const events: Row[] = (occurrences ?? [])
    .filter((occ) => occurrenceDays(occ.startsAt, occ.endsAt, timeZone).includes(today))
    .map((occ) => ({
      kind: 'event',
      key: `e-${occ.eventId}-${occ.startsAt}`,
      at: occ.startsAt,
      allDay: occ.isAllDay || dayKey(occ.startsAt, timeZone) !== today,
      occ,
    }))
  const others: Row[] = (dayItems ?? [])
    .filter((item) => item.kind !== 'event')
    .map((item) => ({
      kind: item.kind as 'task' | 'reminder',
      key: `${item.kind}-${item.id}-${item.at}`,
      at: item.at,
      allDay: item.isAllDay,
      item,
    }))
  const rows = [...events, ...others].sort(
    (a, b) => Number(b.allDay) - Number(a.allDay) || a.at.localeCompare(b.at),
  )

  // The "now" line goes before the first timed row still to come.
  const timed = rows.filter((row) => !row.allDay)
  const nextIndex = timed.length ? rows.findIndex((row) => !row.allDay && dayjs(row.at).isAfter(now)) : -2
  const nowIndex = nextIndex === -1 ? rows.length : nextIndex

  const counts = (['event', 'task', 'reminder'] as const)
    .map((kind) => ({ kind, count: rows.filter((row) => row.kind === kind).length }))
    .filter(({ count }) => count > 0)
    .map(({ kind, count }) => t(`agenda.today.count.${kind}`, { count }))

  const heading = capitalize(
    new Intl.DateTimeFormat(i18n.language, { weekday: 'long', day: 'numeric', month: 'long', timeZone }).format(
      now.toDate(),
    ),
  )

  // The row changing is the feedback; only failures speak up.
  async function setTaskDone(id: string, done: boolean) {
    try {
      await (done ? completeTask : reopenTask).mutateAsync(id)
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.tasks.saveError')))
    }
  }

  async function handleAcknowledge(id: string) {
    try {
      await acknowledge.mutateAsync(id)
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.reminders.saveError')))
    }
  }

  function rowShell(key: string, first: boolean, muted: boolean, children: ReactNode) {
    return (
      <div
        key={key}
        className="flex items-start gap-3 px-4 py-3"
        style={{ borderTop: first ? undefined : `1px solid ${token.colorBorderSecondary}`, opacity: muted ? 0.55 : 1 }}
      >
        {children}
      </div>
    )
  }

  const timeLabel = (row: Row) => (
    <span className="w-16 shrink-0 pt-0.5 text-xs tabular-nums" style={{ color: token.colorTextSecondary }}>
      {row.allDay ? t('agenda.today.allDay') : formatTime(row.at, timeZone)}
    </span>
  )

  const notesLine = (notes: string | null | undefined) =>
    notes && (
      <Typography.Text type="secondary" className="line-clamp-1 text-xs">
        {notes}
      </Typography.Text>
    )

  function renderRow(row: Row, first: boolean) {
    if (row.kind === 'event') {
      const { occ } = row
      const calendar = calendarOf.get(occ.calendarId)
      const color = calendar?.color ?? token.colorPrimary
      const meta = [
        !occ.isAllDay && `${formatTime(occ.startsAt, timeZone)} – ${formatTime(occ.endsAt, timeZone)}`,
        calendar?.name,
        occ.location,
      ].filter(Boolean)
      return rowShell(
        row.key,
        first,
        dayjs(occ.endsAt).isBefore(now),
        <>
          {timeLabel(row)}
          <span className="mt-0.5 h-5 w-1 shrink-0 rounded-full" style={{ background: color }} />
          <button
            type="button"
            className="flex min-w-0 flex-1 cursor-pointer flex-col items-start gap-0.5 text-left"
            onClick={() => setDetail(occ)}
          >
            <Typography.Text className="break-words">{occ.title}</Typography.Text>
            <span className="text-xs" style={{ color: token.colorTextTertiary }}>
              {meta.join(' · ')}
            </span>
          </button>
        </>,
      )
    }

    const { item } = row
    if (row.kind === 'task') {
      const done = item.status === 'Done'
      return rowShell(
        row.key,
        first,
        done,
        <>
          {timeLabel(row)}
          <Checkbox
            checked={done}
            aria-label={item.title}
            onChange={(e) => setTaskDone(item.id, e.target.checked)}
          />
          <div className="flex min-w-0 flex-1 flex-col gap-0.5">
            <Typography.Text delete={done} className="break-words">
              {item.title}
            </Typography.Text>
            {notesLine(item.notes)}
          </div>
        </>,
      )
    }

    const pending = PENDING_REMINDER.includes(item.status ?? '')
    // A pending reminder whose time has come is the one thing here waiting on the user.
    const due = pending && !dayjs(item.at).isAfter(now)
    return rowShell(
      row.key,
      first,
      !pending,
      <>
        {timeLabel(row)}
        <BellOutlined
          className="mt-1"
          style={{ color: due ? token.colorWarning : token.colorTextTertiary }}
          aria-label={t('agenda.today.reminder')}
        />
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <Typography.Text strong={due} className="break-words">
            {item.title}
          </Typography.Text>
          {notesLine(item.notes)}
        </div>
        {pending && (
          <div className="flex shrink-0 gap-1.5">
            <Button size="small" onClick={() => setSnoozeId(item.id)}>
              {t('agenda.reminders.snooze')}
            </Button>
            <Button size="small" type={due ? 'primary' : 'default'} onClick={() => handleAcknowledge(item.id)}>
              {t('agenda.reminders.acknowledge')}
            </Button>
          </div>
        )}
      </>,
    )
  }

  function renderOverdue(task: TaskDto, first: boolean) {
    return rowShell(
      task.id,
      first,
      false,
      <>
        <Checkbox checked={false} aria-label={task.title} onChange={() => setTaskDone(task.id, true)} />
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <Typography.Text className="break-words">{task.title}</Typography.Text>
          <span className="text-xs" style={{ color: token.colorError }}>
            <CalendarOutlined /> {formatDue(task.dueAt!, task.dueHasTime, timeZone, i18n.language)}
          </span>
        </div>
      </>,
    )
  }

  const nowLine = (
    <div key="now" className="flex items-center gap-2 px-4" aria-label={t('agenda.today.now')}>
      <span className="text-[11px] font-semibold uppercase tracking-wider" style={{ color: token.colorError }}>
        {t('agenda.today.now')}
      </span>
      <span className="h-px flex-1" style={{ background: token.colorError }} />
    </div>
  )

  const loadError = todayError ?? occurrenceError ?? taskError ?? calendarError
  if (loadError) return <Alert type="error" showIcon title={loadError.message} />

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeading title={heading} description={counts.length ? counts.join(' · ') : undefined} />

      {overdue.length > 0 && (
        <SettingsSection
          title={
            <span style={{ color: token.colorError }}>
              {t('agenda.tasks.buckets.overdue')} · {overdue.length}
            </span>
          }
        >
          {overdue.map((task, index) => renderOverdue(task, index === 0))}
        </SettingsSection>
      )}

      {isLoading ? (
        <Skeleton active />
      ) : rows.length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('agenda.today.empty')} />
      ) : (
        <SettingsSection bodyClassName="py-1">
          {rows.map((row, index) => (
            <Fragment key={row.key}>
              {index === nowIndex && nowLine}
              {renderRow(row, index === 0 || index === nowIndex)}
            </Fragment>
          ))}
          {nowIndex === rows.length && nowLine}
        </SettingsSection>
      )}

      <SnoozeModal open={!!snoozeId} reminderId={snoozeId} onClose={() => setSnoozeId(null)} />
      <EventDetailModal open={!!detail} occurrence={detail} calendars={calendars} onClose={() => setDetail(null)} />
    </div>
  )
}
