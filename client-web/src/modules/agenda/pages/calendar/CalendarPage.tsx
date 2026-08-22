import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Calendar, Card, Flex, Segmented, Tag, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import type { CalendarDto, EventOccurrenceDto } from '../../models'
import { useCalendars } from '../../hooks/useCalendars'
import { useEventOccurrences } from '../../hooks/useEvents'
import { CalendarSidebar } from './CalendarSidebar'
import { EventFormModal } from './EventFormModal'
import { EventDetailModal } from './EventDetailModal'

type CalendarView = 'month' | 'week' | 'day'

export function CalendarPage() {
  const { t } = useTranslation()
  const [view, setView] = useState<CalendarView>('month')
  const [cursor, setCursor] = useState<Dayjs>(() => dayjs())
  const [createOpen, setCreateOpen] = useState(false)
  const [createStart, setCreateStart] = useState<Dayjs | null>(null)
  const [detail, setDetail] = useState<EventOccurrenceDto | null>(null)

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

  // Query the whole visible grid (month ± padding week); filter to visible calendars client-side.
  const from = cursor.startOf('month').startOf('week')
  const to = cursor.endOf('month').endOf('week')
  const { data: occurrences } = useEventOccurrences({
    from: from.toISOString(),
    to: to.toISOString(),
  })

  const byDay = useMemo(() => {
    const map = new Map<string, EventOccurrenceDto[]>()
    for (const occ of occurrences ?? []) {
      if (!visibleIds.has(occ.calendarId)) continue
      const key = dayjs(occ.startsAt).format('YYYY-MM-DD')
      const arr = map.get(key) ?? []
      arr.push(occ)
      map.set(key, arr)
    }
    return map
  }, [occurrences, visibleIds])

  function openCreate(day: Dayjs | null) {
    setCreateStart(day ? day.hour(9).minute(0).second(0) : null)
    setCreateOpen(true)
  }

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
          <Typography.Title level={4} style={{ margin: 0 }}>
            {t('nav.agendaCalendar')}
          </Typography.Title>
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
            onSelect={(date, info) => {
              setCursor(date)
              // A day click (not a panel/month switch) starts a new event on that day.
              if (info.source === 'date') openCreate(date)
            }}
            onPanelChange={(date) => setCursor(date)}
            cellRender={(current, info) => (info.type === 'date' ? cellRender(current) : null)}
          />
        ) : (
          <Alert type="info" showIcon message={t('agenda.events.weekDayComingSoon')} />
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
