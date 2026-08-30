import { useMemo } from 'react'
import { Flex, Tag, Typography, theme } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import type { EventOccurrenceDto } from '../../models'
import { formatTime } from '../../lib/datetime'

const HOUR_HEIGHT = 44
const GUTTER_WIDTH = 56
const HOURS = Array.from({ length: 24 }, (_, h) => h)

interface WeekDayGridProps {
  /** One column per day (1 for the day view, 7 for the week view), each at start-of-day. */
  days: Dayjs[]
  /** Occurrences already filtered to the visible calendars. */
  occurrences: EventOccurrenceDto[]
  colorOf: Map<string, string>
  onSelectOccurrence: (occ: EventOccurrenceDto) => void
  onCreateAt: (start: Dayjs) => void
}

interface Positioned {
  occ: EventOccurrenceDto
  startM: number
  endM: number
  col: number
  cols: number
}

/** Greedy lane packing: overlapping events in a day share the column width side by side. */
function packDay(day: Dayjs, timed: EventOccurrenceDto[]): Positioned[] {
  const items = timed
    .map((occ) => {
      const startM = Math.max(0, Math.min(1440, dayjs(occ.startsAt).diff(day, 'minute')))
      const endRaw = Math.max(0, Math.min(1440, dayjs(occ.endsAt).diff(day, 'minute')))
      return { occ, startM, endM: Math.max(endRaw, startM + 15) }
    })
    .sort((a, b) => a.startM - b.startM || a.endM - b.endM)

  const result: Positioned[] = []
  let cluster: typeof items = []
  let clusterEnd = -1

  const flush = () => {
    const laneEnds: number[] = []
    const placed = cluster.map((it) => {
      let col = laneEnds.findIndex((end) => end <= it.startM)
      if (col === -1) {
        col = laneEnds.length
        laneEnds.push(it.endM)
      } else {
        laneEnds[col] = it.endM
      }
      return { it, col }
    })
    for (const p of placed) {
      result.push({ occ: p.it.occ, startM: p.it.startM, endM: p.it.endM, col: p.col, cols: laneEnds.length })
    }
    cluster = []
    clusterEnd = -1
  }

  for (const it of items) {
    if (cluster.length && it.startM >= clusterEnd) flush()
    cluster.push(it)
    clusterEnd = Math.max(clusterEnd, it.endM)
  }
  if (cluster.length) flush()
  return result
}

export function WeekDayGrid({ days, occurrences, colorOf, onSelectOccurrence, onCreateAt }: WeekDayGridProps) {
  const { token } = theme.useToken()
  const today = dayjs()
  const nowMinutes = today.hour() * 60 + today.minute()

  const perDay = useMemo(
    () =>
      days.map((day) => {
        const inDay = occurrences.filter((occ) => dayjs(occ.startsAt).isSame(day, 'day'))
        return {
          day,
          allDay: inDay.filter((occ) => occ.isAllDay),
          positioned: packDay(day, inDay.filter((occ) => !occ.isAllDay)),
        }
      }),
    [days, occurrences],
  )

  const columnBorder = `1px solid ${token.colorBorderSecondary}`

  return (
    <div style={{ overflowX: 'auto' }}>
      <div style={{ minWidth: days.length > 1 ? 640 : 280 }}>
        {/* Header: weekday + date per column. */}
        <Flex style={{ borderBottom: columnBorder }}>
          <div style={{ width: GUTTER_WIDTH, flexShrink: 0 }} />
          {perDay.map(({ day }) => {
            const isToday = day.isSame(today, 'day')
            return (
              <div
                key={day.toISOString()}
                style={{ flex: 1, textAlign: 'center', padding: '4px 0', minWidth: 0 }}
              >
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {day.format('ddd')}
                </Typography.Text>
                <div>
                  <Typography.Text
                    strong={isToday}
                    style={{ color: isToday ? token.colorPrimary : undefined }}
                  >
                    {day.format('D')}
                  </Typography.Text>
                </div>
              </div>
            )
          })}
        </Flex>

        {/* All-day strip. */}
        {perDay.some(({ allDay }) => allDay.length > 0) && (
          <Flex style={{ borderBottom: columnBorder, minHeight: 28 }}>
            <div style={{ width: GUTTER_WIDTH, flexShrink: 0 }} />
            {perDay.map(({ day, allDay }) => (
              <div key={day.toISOString()} style={{ flex: 1, padding: 2, minWidth: 0, borderLeft: columnBorder }}>
                <Flex vertical gap={2}>
                  {allDay.map((occ) => (
                    <Tag
                      key={`${occ.eventId}-${occ.startsAt}`}
                      color={colorOf.get(occ.calendarId)}
                      style={{ margin: 0, cursor: 'pointer', overflow: 'hidden', textOverflow: 'ellipsis' }}
                      onClick={() => onSelectOccurrence(occ)}
                    >
                      {occ.title}
                    </Tag>
                  ))}
                </Flex>
              </div>
            ))}
          </Flex>
        )}

        {/* Time grid. */}
        <Flex style={{ position: 'relative', height: HOUR_HEIGHT * 24 }}>
          {/* Hour gutter. */}
          <div style={{ width: GUTTER_WIDTH, flexShrink: 0 }}>
            {HOURS.map((h) => (
              <div key={h} style={{ height: HOUR_HEIGHT, position: 'relative' }}>
                {h > 0 && (
                  <Typography.Text
                    type="secondary"
                    style={{ position: 'absolute', top: -8, right: 6, fontSize: 11 }}
                  >
                    {String(h).padStart(2, '0')}:00
                  </Typography.Text>
                )}
              </div>
            ))}
          </div>

          {/* Day columns. */}
          {perDay.map(({ day, positioned }) => {
            const isToday = day.isSame(today, 'day')
            return (
              <div key={day.toISOString()} style={{ flex: 1, position: 'relative', borderLeft: columnBorder, minWidth: 0 }}>
                {/* Clickable hour cells (create). */}
                {HOURS.map((h) => (
                  <div
                    key={h}
                    onClick={() => onCreateAt(day.hour(h).minute(0).second(0))}
                    style={{ height: HOUR_HEIGHT, borderTop: columnBorder, cursor: 'pointer' }}
                  />
                ))}

                {/* Now indicator. */}
                {isToday && (
                  <div
                    style={{
                      position: 'absolute',
                      top: (nowMinutes / 60) * HOUR_HEIGHT,
                      left: 0,
                      right: 0,
                      height: 2,
                      background: token.colorError,
                      zIndex: 3,
                    }}
                  />
                )}

                {/* Event blocks. */}
                {positioned.map((p) => (
                  <button
                    key={`${p.occ.eventId}-${p.occ.startsAt}`}
                    type="button"
                    onClick={() => onSelectOccurrence(p.occ)}
                    title={p.occ.title}
                    style={{
                      position: 'absolute',
                      top: (p.startM / 60) * HOUR_HEIGHT,
                      height: Math.max(((p.endM - p.startM) / 60) * HOUR_HEIGHT - 2, 16),
                      left: `calc(${(p.col / p.cols) * 100}% + 2px)`,
                      width: `calc(${100 / p.cols}% - 4px)`,
                      background: colorOf.get(p.occ.calendarId) ?? token.colorPrimary,
                      color: '#fff',
                      border: 'none',
                      borderRadius: token.borderRadiusSM,
                      padding: '1px 4px',
                      textAlign: 'left',
                      overflow: 'hidden',
                      cursor: 'pointer',
                      fontSize: 11,
                      lineHeight: 1.3,
                      zIndex: 2,
                    }}
                  >
                    <div style={{ fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {p.occ.title}
                    </div>
                    <div style={{ opacity: 0.85, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {formatTime(p.occ.startsAt)}
                    </div>
                  </button>
                ))}
              </div>
            )
          })}
        </Flex>
      </div>
    </div>
  )
}
