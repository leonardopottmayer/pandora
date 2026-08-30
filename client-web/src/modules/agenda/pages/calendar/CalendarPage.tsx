import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Calendar, Card, Flex, Segmented, Tag, Typography } from 'antd'
import { LeftOutlined, PlusOutlined, RightOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import updateLocale from 'dayjs/plugin/updateLocale'
import type { CalendarDto, EventOccurrenceDto } from '../../models'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { startOfWeek, weekDays } from '../../lib/datetime'
import { useCalendars } from '../../hooks/useCalendars'
import { useEventOccurrences } from '../../hooks/useEvents'
import { CalendarSidebar } from './CalendarSidebar'
import { EventFormModal } from './EventFormModal'
import { EventDetailModal } from './EventDetailModal'
import { WeekDayGrid } from './WeekDayGrid'

dayjs.extend(updateLocale)

type CalendarView = 'month' | 'week' | 'day'

export function CalendarPage() {
  const { t } = useTranslation()
  const { weekStartsOn } = usePreferences()
  const [view, setView] = useState<CalendarView>('month')
  const [cursor, setCursor] = useState<Dayjs>(() => dayjs())
  const [createOpen, setCreateOpen] = useState(false)
  const [createStart, setCreateStart] = useState<Dayjs | null>(null)
  const [detail, setDetail] = useState<EventOccurrenceDto | null>(null)

  // The antd month grid derives its first column from the dayjs locale; align it with the preference.
  useEffect(() => {
    dayjs.updateLocale(dayjs.locale(), { weekStart: weekStartsOn === 'monday' ? 1 : 0 })
  }, [weekStartsOn])

  const { data: calendarList } = useCalendars()
  const calendars = useMemo<CalendarDto[]>(
    () => (calendarList ?? []).filter((c) => !c.archivedAt),
    [calendarList],
  )
  const visibleIds = useMemo(
    () => new Set(calendars.filter((c) => c.isVisible).map((c) => c.id)),
    [calendars],
  )
  const colorOf = useMemo(() => {
    const map = new Map<string, string>()
    for (const c of calendars) map.set(c.id, c.color ?? '#1677ff')
    return map
  }, [calendars])

  // The visible range depends on the view; the query fetches exactly that window.
  const range = useMemo(() => {
    if (view === 'day') {
      const start = cursor.startOf('day')
      return { days: [start], from: start, to: cursor.endOf('day') }
    }
    if (view === 'week') {
      const days = weekDays(cursor, weekStartsOn)
      return { days, from: days[0], to: days[6].endOf('day') }
    }
    const from = startOfWeek(cursor.startOf('month'), weekStartsOn)
    const to = startOfWeek(cursor.endOf('month'), weekStartsOn).add(6, 'day').endOf('day')
    return { days: [] as Dayjs[], from, to }
  }, [view, cursor, weekStartsOn])

  const { data: occurrences } = useEventOccurrences({
    from: range.from.toISOString(),
    to: range.to.toISOString(),
  })

  const visibleOccurrences = useMemo(
    () => (occurrences ?? []).filter((occ) => visibleIds.has(occ.calendarId)),
    [occurrences, visibleIds],
  )

  const byDay = useMemo(() => {
    const map = new Map<string, EventOccurrenceDto[]>()
    for (const occ of visibleOccurrences) {
      const key = dayjs(occ.startsAt).format('YYYY-MM-DD')
      const arr = map.get(key) ?? []
      arr.push(occ)
      map.set(key, arr)
    }
    return map
  }, [visibleOccurrences])

  function openCreate(start: Dayjs | null) {
    setCreateStart(start ? (start.hour() === 0 && start.minute() === 0 ? start.hour(9) : start) : null)
    setCreateOpen(true)
  }

  function shift(direction: 1 | -1) {
    const unit = view === 'day' ? 'day' : view === 'week' ? 'week' : 'month'
    setCursor((c) => c.add(direction, unit))
  }

  const rangeLabel =
    view === 'day'
      ? cursor.format('ddd, MMM D, YYYY')
      : view === 'week'
        ? `${range.days[0].format('MMM D')} – ${range.days[6].format('MMM D, YYYY')}`
        : cursor.format('MMMM YYYY')

  function cellRender(current: Dayjs) {
    const items = byDay.get(current.format('YYYY-MM-DD')) ?? []
    return (
      <Flex vertical gap={2}>
        {items.slice(0, 3).map((occ) => (
          <Tag
            key={`${occ.eventId}-${occ.startsAt}`}
            color={colorOf.get(occ.calendarId)}
            style={{ margin: 0, cursor: 'pointer', overflow: 'hidden', textOverflow: 'ellipsis' }}
            onClick={(e) => {
              e.stopPropagation()
              setDetail(occ)
            }}
          >
            {occ.title}
          </Tag>
        ))}
        {items.length > 3 && (
          <Typography.Text type="secondary" style={{ fontSize: 11 }}>
            +{items.length - 3}
          </Typography.Text>
        )}
      </Flex>
    )
  }

  return (
    <Flex gap="large" align="flex-start" wrap>
      <Card style={{ width: 240, flexShrink: 0 }}>
        <CalendarSidebar />
      </Card>

      <Card style={{ flex: 1, minWidth: 360 }}>
        <Flex justify="space-between" align="center" wrap gap="small" className="mb-4">
          <Flex gap="small" align="center">
            <Button size="small" onClick={() => setCursor(dayjs())}>
              {t('agenda.events.today')}
            </Button>
            <Button size="small" icon={<LeftOutlined />} aria-label={t('agenda.events.prev')} onClick={() => shift(-1)} />
            <Button size="small" icon={<RightOutlined />} aria-label={t('agenda.events.next')} onClick={() => shift(1)} />
            <Typography.Title level={5} style={{ margin: 0 }}>
              {rangeLabel}
            </Typography.Title>
          </Flex>
          <Flex gap="small" align="center">
            <Segmented<CalendarView>
              value={view}
              onChange={setView}
              options={[
                { value: 'month', label: t('agenda.events.view.month') },
                { value: 'week', label: t('agenda.events.view.week') },
                { value: 'day', label: t('agenda.events.view.day') },
              ]}
            />
            <Button
              type="primary"
              icon={<PlusOutlined />}
              disabled={calendars.length === 0}
              onClick={() => openCreate(cursor)}
            >
              {t('agenda.events.new')}
            </Button>
          </Flex>
        </Flex>

        {view === 'month' ? (
          <Calendar
            value={cursor}
            headerRender={() => null}
            onSelect={(date, info) => {
              setCursor(date)
              // A day click (not a panel/month switch) starts a new event on that day.
              if (info.source === 'date') openCreate(date)
            }}
            onPanelChange={(date) => setCursor(date)}
            cellRender={(current, info) => (info.type === 'date' ? cellRender(current) : null)}
          />
        ) : (
          <WeekDayGrid
            days={range.days}
            occurrences={visibleOccurrences}
            colorOf={colorOf}
            onSelectOccurrence={setDetail}
            onCreateAt={openCreate}
          />
        )}
      </Card>

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
    </Flex>
  )
}
