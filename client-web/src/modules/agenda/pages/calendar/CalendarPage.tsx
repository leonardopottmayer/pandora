import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, Skeleton, Button, Calendar, Segmented, Typography } from 'antd'
import { LeftOutlined, PlusOutlined, RightOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import type { CalendarDto, EventOccurrenceDto } from '../../models'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { dayKey, keyToDate, occurrenceDays, startOfWeek } from '../../lib/datetime'
import { useCalendars } from '../../hooks/useCalendars'
import { useEventOccurrences } from '../../hooks/useEvents'
import { CalendarSidebar } from './CalendarSidebar'
import { EventFormModal } from './EventFormModal'
import { EventDetailModal } from './EventDetailModal'
import { MonthGrid } from './MonthGrid'
import { WeekDayGrid } from './WeekDayGrid'

type CalendarView = 'month' | 'week' | 'day'
const VIEWS: CalendarView[] = ['month', 'week', 'day']
const KEY = 'YYYY-MM-DD'

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

export function CalendarPage() {
  const { t, i18n } = useTranslation()
  const { weekStartsOn, timeZone } = usePreferences()
  const [searchParams, setSearchParams] = useSearchParams()
  const [createOpen, setCreateOpen] = useState(false)
  const [createStart, setCreateStart] = useState<Dayjs | null>(null)
  const [detail, setDetail] = useState<EventOccurrenceDto | null>(null)

  // View and date live in the URL, so a reload or a shared link opens on the same page.
  const today = dayKey(dayjs(), timeZone)
  const view = (VIEWS as string[]).includes(searchParams.get('view') ?? '') ? (searchParams.get('view') as CalendarView) : 'month'
  const dateParam = searchParams.get('date') ?? ''
  const cursor = /^\d{4}-\d{2}-\d{2}$/.test(dateParam) && dayjs(dateParam).isValid() ? dateParam : today

  function go(nextView: CalendarView, nextDate: string) {
    const params: Record<string, string> = {}
    if (nextView !== 'month') params.view = nextView
    if (nextDate !== today) params.date = nextDate
    setSearchParams(params, { replace: true })
  }

  const { data: calendarList, isLoading: calendarsLoading, error: calendarError } = useCalendars()
  const calendars = useMemo<CalendarDto[]>(() => (calendarList ?? []).filter((c) => !c.archivedAt), [calendarList])
  const visibleIds = useMemo(() => new Set(calendars.filter((c) => c.isVisible).map((c) => c.id)), [calendars])
  const colorOf = useMemo(() => new Map(calendars.map((c) => [c.id, c.color ?? '#1677ff'])), [calendars])

  // The dates on screen: one, a week, or the whole weeks around the month.
  const days = useMemo(() => {
    const date = dayjs(cursor)
    if (view === 'day') return [cursor]
    const first = startOfWeek(view === 'week' ? date : date.startOf('month'), weekStartsOn)
    const last = view === 'week' ? first.add(6, 'day') : startOfWeek(date.endOf('month'), weekStartsOn).add(6, 'day')
    return Array.from({ length: last.diff(first, 'day') + 1 }, (_, i) => first.add(i, 'day').format(KEY))
  }, [view, cursor, weekStartsOn])

  const { data: occurrences, isLoading: occurrencesLoading, error: occurrenceError } = useEventOccurrences({
    from: dayjs.tz(days[0], timeZone).toISOString(),
    to: dayjs.tz(days[days.length - 1], timeZone).add(1, 'day').toISOString(),
  })

  // Each occurrence under every date it touches: all-day ones first, then by start.
  const byDay = useMemo(() => {
    const map = new Map<string, EventOccurrenceDto[]>()
    const visible = (occurrences ?? [])
      .filter((occ) => visibleIds.has(occ.calendarId))
      .sort((a, b) => Number(b.isAllDay) - Number(a.isAllDay) || a.startsAt.localeCompare(b.startsAt))
    for (const occ of visible) {
      for (const day of occurrenceDays(occ.startsAt, occ.endsAt, timeZone)) {
        map.set(day, [...(map.get(day) ?? []), occ])
      }
    }
    return map
  }, [occurrences, visibleIds, timeZone])

  function openCreate(start: Dayjs) {
    setCreateStart(start)
    setCreateOpen(true)
  }

  const at = (day: string, hour: number) => dayjs.tz(`${day}T${String(hour).padStart(2, '0')}:00`, timeZone)

  function openNew() {
    // Today starts at the next full hour; any other day at 9.
    openCreate(cursor === today ? dayjs().tz(timeZone).add(1, 'hour').startOf('hour') : at(cursor, 9))
  }

  function shift(direction: 1 | -1) {
    go(view, dayjs(cursor).add(direction, view).format(KEY))
  }

  const rangeLabel = (() => {
    const fmt = (options: Intl.DateTimeFormatOptions) =>
      new Intl.DateTimeFormat(i18n.language, { ...options, timeZone: 'UTC' })
    if (view === 'day') {
      return capitalize(fmt({ weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(keyToDate(cursor)))
    }
    if (view === 'week') {
      return fmt({ day: 'numeric', month: 'short', year: 'numeric' }).formatRange(keyToDate(days[0]), keyToDate(days[6]))
    }
    return capitalize(fmt({ month: 'long', year: 'numeric' }).format(keyToDate(cursor)))
  })()

  const loadError = calendarError ?? occurrenceError
  if (loadError) return <Alert type="error" showIcon title={loadError.message} />
  if (calendarsLoading || occurrencesLoading) return <Skeleton active />

  return (
    <div className="mx-auto flex max-w-7xl flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2">
          <Button onClick={() => go(view, today)} disabled={cursor === today}>
            {t('agenda.events.today')}
          </Button>
          <Button
            type="text"
            icon={<LeftOutlined />}
            title={t('agenda.events.prev')}
            aria-label={t('agenda.events.prev')}
            onClick={() => shift(-1)}
          />
          <Button
            type="text"
            icon={<RightOutlined />}
            title={t('agenda.events.next')}
            aria-label={t('agenda.events.next')}
            onClick={() => shift(1)}
          />
          <Typography.Title level={4} className="!mb-0 truncate">
            {rangeLabel}
          </Typography.Title>
        </div>
        <div className="flex items-center gap-2">
          <Segmented<CalendarView>
            value={view}
            onChange={(next) => go(next, cursor)}
            options={VIEWS.map((v) => ({ value: v, label: t(`agenda.events.view.${v}`) }))}
          />
          <Button type="primary" icon={<PlusOutlined />} disabled={calendars.length === 0} onClick={openNew}>
            {t('agenda.events.new')}
          </Button>
        </div>
      </div>

      {/* minmax(0, …) on phones too: an implicit column grows to its widest content and pushes the page sideways. */}
      <div className="grid grid-cols-[minmax(0,1fr)] items-start gap-6 lg:grid-cols-[256px_minmax(0,1fr)]">
        <div className="flex flex-col gap-6">
          <SettingsSection bodyClassName="p-1">
            <Calendar
              fullscreen={false}
              value={dayjs(cursor)}
              onSelect={(date, info) => {
                if (info.source === 'date') go(view, date.format(KEY))
              }}
              onPanelChange={(date) => go(view, date.format(KEY))}
            />
          </SettingsSection>
          <CalendarSidebar />
        </div>

        <SettingsSection>
          {view === 'month' ? (
            <MonthGrid
              days={days}
              month={cursor.slice(0, 7)}
              today={today}
              byDay={byDay}
              colorOf={colorOf}
              onSelectOccurrence={setDetail}
              onOpenDay={(day) => go('day', day)}
              onCreateOn={(day) => calendars.length > 0 && openCreate(at(day, 9))}
            />
          ) : (
            <WeekDayGrid
              days={days}
              today={today}
              byDay={byDay}
              colorOf={colorOf}
              onSelectOccurrence={setDetail}
              onCreateAt={(day, hour) => calendars.length > 0 && openCreate(at(day, hour))}
            />
          )}
        </SettingsSection>
      </div>

      <EventFormModal
        open={createOpen}
        calendars={calendars}
        defaultStart={createStart}
        onClose={() => setCreateOpen(false)}
      />
      <EventDetailModal
        open={!!detail}
        occurrence={detail}
        calendars={calendars}
        onClose={() => setDetail(null)}
      />
    </div>
  )
}
