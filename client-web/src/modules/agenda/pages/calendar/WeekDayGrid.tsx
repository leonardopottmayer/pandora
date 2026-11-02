import { useEffect, useMemo, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { theme } from 'antd'
import dayjs from 'dayjs'
import { onAccent } from '@/app/theme'
import type { EventOccurrenceDto } from '../../models'
import { tint } from '../../lib/color'
import { dayKey, formatTime, keyToDate, minuteOfDay } from '../../lib/datetime'
import { usePreferences } from '@/modules/identity/context/preferences-context'

const HOUR_HEIGHT = 48
const GUTTER_WIDTH = 48
const HOURS = Array.from({ length: 24 }, (_, h) => h)
/** Where the grid opens when today is not on screen. */
const FIRST_HOUR = 7

interface WeekDayGridProps {
  /** One column per date (1 for the day view, 7 for the week view), as YYYY-MM-DD. */
  days: string[]
  today: string
  /** Occurrences per date they touch, already filtered to the visible calendars. */
  byDay: Map<string, EventOccurrenceDto[]>
  colorOf: Map<string, string>
  onSelectOccurrence: (occ: EventOccurrenceDto) => void
  onCreateAt: (day: string, hour: number) => void
}

interface Segment {
  occ: EventOccurrenceDto
  startM: number
  endM: number
}

interface Positioned extends Segment {
  col: number
  cols: number
}

/** Greedy lane packing: overlapping events in a day share the column width side by side. */
function packDay(segments: Segment[]): Positioned[] {
  const items = [...segments].sort((a, b) => a.startM - b.startM || a.endM - b.endM)

  const result: Positioned[] = []
  let cluster: Segment[] = []
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
    for (const p of placed) result.push({ ...p.it, col: p.col, cols: laneEnds.length })
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

export function WeekDayGrid({ days, today, byDay, colorOf, onSelectOccurrence, onCreateAt }: WeekDayGridProps) {
  const { i18n } = useTranslation()
  const { token } = theme.useToken()
  const { timeZone } = usePreferences()
  const nowMinutes = minuteOfDay(dayjs().toISOString(), timeZone)
  const scrollRef = useRef<HTMLDivElement>(null)

  const perDay = useMemo(
    () =>
      days.map((day) => {
        const items = byDay.get(day) ?? []
        // A day of a longer event runs from midnight, or to it, on the days it only passes through.
        const segments = items
          .filter((occ) => !occ.isAllDay)
          .map((occ) => {
            const startM = dayKey(occ.startsAt, timeZone) === day ? minuteOfDay(occ.startsAt, timeZone) : 0
            const endM = dayKey(occ.endsAt, timeZone) === day ? minuteOfDay(occ.endsAt, timeZone) : 1440
            return { occ, startM, endM: Math.max(endM, startM + 15) }
          })
        return { day, allDay: items.filter((occ) => occ.isAllDay), positioned: packDay(segments) }
      }),
    [days, byDay, timeZone],
  )

  // Open on the working day, or an hour before now when today is on screen.
  const showsToday = days.includes(today)
  const firstDay = days[0]
  useEffect(() => {
    const target = showsToday ? Math.max(0, nowMinutes / 60 - 1) : FIRST_HOUR
    if (scrollRef.current) scrollRef.current.scrollTop = target * HOUR_HEIGHT
    // Only when the dates change, not as the clock moves.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [firstDay, days.length])

  const border = `1px solid ${token.colorBorderSecondary}`
  const weekday = new Intl.DateTimeFormat(i18n.language, { weekday: 'short', timeZone: 'UTC' })
  const hasAllDay = perDay.some(({ allDay }) => allDay.length > 0)

  return (
    // Seven narrow columns on a phone, never a sideways scroll: titles truncate instead.
    <div ref={scrollRef} className="h-[calc(100dvh-290px)] min-h-[420px] overflow-y-auto overflow-x-hidden">
      <div>
        {/* Header: weekday, date and all-day events, kept in view while the hours scroll. */}
        <div className="sticky top-0 z-10" style={{ background: token.colorBgContainer, borderBottom: border }}>
          <div className="flex">
            <div style={{ width: GUTTER_WIDTH, flexShrink: 0 }} />
            {perDay.map(({ day }) => {
              const isToday = day === today
              return (
                <div key={day} className="flex min-w-0 flex-1 flex-col items-center gap-0.5 py-2">
                  <span className="text-xs font-medium uppercase" style={{ color: token.colorTextSecondary }}>
                    {weekday.format(keyToDate(day)).replace('.', '')}
                  </span>
                  <span
                    className="flex size-8 items-center justify-center rounded-full text-base tabular-nums"
                    style={
                      isToday
                        ? { background: token.colorPrimary, color: onAccent(token.colorPrimary), fontWeight: 600 }
                        : { color: token.colorText }
                    }
                  >
                    {Number(day.slice(8))}
                  </span>
                </div>
              )
            })}
          </div>

          {hasAllDay && (
            <div className="flex" style={{ borderTop: border }}>
              <div style={{ width: GUTTER_WIDTH, flexShrink: 0 }} />
              {perDay.map(({ day, allDay }) => (
                <div key={day} className="flex min-w-0 flex-1 flex-col gap-0.5 p-0.5" style={{ borderLeft: border }}>
                  {allDay.map((occ) => {
                    const color = colorOf.get(occ.calendarId) ?? token.colorPrimary
                    return (
                      <button
                        key={`${occ.eventId}-${occ.startsAt}`}
                        type="button"
                        title={occ.title}
                        className="cursor-pointer truncate rounded px-1.5 text-left text-xs leading-5"
                        style={{ background: tint(color), borderLeft: `3px solid ${color}`, color: token.colorText }}
                        onClick={() => onSelectOccurrence(occ)}
                      >
                        {occ.title}
                      </button>
                    )
                  })}
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Time grid. */}
        <div className="relative flex" style={{ height: HOUR_HEIGHT * 24 }}>
          <div style={{ width: GUTTER_WIDTH, flexShrink: 0 }}>
            {HOURS.map((h) => (
              <div key={h} className="relative" style={{ height: HOUR_HEIGHT }}>
                {h > 0 && (
                  <span
                    className="absolute right-2 text-[11px] tabular-nums"
                    style={{ top: -8, color: token.colorTextTertiary }}
                  >
                    {String(h).padStart(2, '0')}:00
                  </span>
                )}
              </div>
            ))}
          </div>

          {perDay.map(({ day, positioned }) => (
            <div key={day} className="relative min-w-0 flex-1" style={{ borderLeft: border }}>
              {HOURS.map((h) => (
                <div
                  key={h}
                  className="cursor-pointer"
                  onClick={() => onCreateAt(day, h)}
                  style={{ height: HOUR_HEIGHT, borderTop: h ? border : undefined }}
                />
              ))}

              {day === today && (
                <div
                  className="pointer-events-none absolute inset-x-0 z-[3]"
                  style={{ top: (nowMinutes / 60) * HOUR_HEIGHT, height: 2, background: token.colorError }}
                >
                  <span
                    className="absolute -left-1 -top-1 size-2.5 rounded-full"
                    style={{ background: token.colorError }}
                  />
                </div>
              )}

              {positioned.map((p) => {
                const color = colorOf.get(p.occ.calendarId) ?? token.colorPrimary
                const height = Math.max(((p.endM - p.startM) / 60) * HOUR_HEIGHT - 2, 18)
                return (
                  <button
                    key={`${p.occ.eventId}-${p.occ.startsAt}`}
                    type="button"
                    onClick={() => onSelectOccurrence(p.occ)}
                    title={p.occ.title}
                    className="absolute z-[2] cursor-pointer overflow-hidden rounded px-1.5 py-0.5 text-left text-xs leading-tight"
                    style={{
                      top: (p.startM / 60) * HOUR_HEIGHT + 1,
                      height,
                      left: `calc(${(p.col / p.cols) * 100}% + 2px)`,
                      width: `calc(${100 / p.cols}% - 4px)`,
                      // Solid base under the tint, so the hour lines do not show through.
                      background: `linear-gradient(${tint(color, '38')}, ${tint(color, '38')}), ${token.colorBgContainer}`,
                      borderLeft: `3px solid ${color}`,
                      color: token.colorText,
                    }}
                  >
                    <div className="truncate font-semibold">{p.occ.title}</div>
                    {height >= 34 && (
                      <div className="truncate tabular-nums" style={{ color: token.colorTextSecondary }}>
                        {formatTime(p.occ.startsAt, timeZone)} – {formatTime(p.occ.endsAt, timeZone)}
                      </div>
                    )}
                  </button>
                )
              })}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
