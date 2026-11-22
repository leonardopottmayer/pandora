import { useTranslation } from 'react-i18next'
import { theme } from 'antd'
import { onAccent } from '@/app/theme'
import type { EventOccurrenceDto } from '../../models'
import { tint } from '../../lib/color'
import { dayKey, formatTime, keyToDate } from '../../lib/datetime'
import { usePreferences } from '@/modules/identity/context/preferences-context'

const MAX_CHIPS = 3

interface MonthGridProps {
  /** The dates on screen, whole weeks, as YYYY-MM-DD. */
  days: string[]
  /** The month being shown, as YYYY-MM; days outside it are dimmed. */
  month: string
  today: string
  /** Occurrences per date they touch, in display order. */
  byDay: Map<string, EventOccurrenceDto[]>
  colorOf: Map<string, string>
  onSelectOccurrence: (occ: EventOccurrenceDto) => void
  onOpenDay: (day: string) => void
  onCreateOn: (day: string) => void
}

export function MonthGrid({ days, month, today, byDay, colorOf, onSelectOccurrence, onOpenDay, onCreateOn }: MonthGridProps) {
  const { t, i18n } = useTranslation()
  const { token } = theme.useToken()
  const { timeZone } = usePreferences()
  const weekday = new Intl.DateTimeFormat(i18n.language, { weekday: 'short', timeZone: 'UTC' })
  const border = `1px solid ${token.colorBorderSecondary}`

  return (
    <div className="grid grid-cols-7">
      {days.slice(0, 7).map((day) => (
        <div
          key={day}
          className="px-2 py-1.5 text-center text-xs font-medium uppercase"
          style={{ color: token.colorTextSecondary, borderBottom: border }}
        >
          {weekday.format(keyToDate(day)).replace('.', '')}
        </div>
      ))}

      {days.map((day, index) => {
        const items = byDay.get(day) ?? []
        const isToday = day === today
        const outside = !day.startsWith(month)
        return (
          <div
            key={day}
            className="flex min-h-[7.5rem] min-w-0 cursor-pointer flex-col gap-0.5 p-1"
            style={{
              borderTop: index >= 7 ? border : undefined,
              borderLeft: index % 7 ? border : undefined,
              background: outside ? token.colorFillQuaternary : undefined,
            }}
            onClick={() => onCreateOn(day)}
          >
            <button
              type="button"
              className="mx-auto flex size-7 cursor-pointer items-center justify-center rounded-full text-xs tabular-nums"
              style={
                isToday
                  ? { background: token.colorPrimary, color: onAccent(token.colorPrimary), fontWeight: 600 }
                  : { color: outside ? token.colorTextQuaternary : token.colorText }
              }
              onClick={(e) => {
                e.stopPropagation()
                onOpenDay(day)
              }}
            >
              {Number(day.slice(8))}
            </button>

            {items.slice(0, MAX_CHIPS).map((occ) => {
              const color = colorOf.get(occ.calendarId) ?? token.colorPrimary
              // An all-day event, or a later day of a long one, is a band; a timed start is a dot and its hour.
              const band = occ.isAllDay || dayKey(occ.startsAt, timeZone) !== day
              return (
                <button
                  key={`${occ.eventId}-${occ.startsAt}`}
                  type="button"
                  title={occ.title}
                  className="flex min-w-0 cursor-pointer items-center gap-1.5 rounded px-1.5 py-px text-left text-xs leading-5"
                  style={
                    band
                      ? { background: tint(color), borderLeft: `3px solid ${color}`, color: token.colorText }
                      : { color: token.colorText }
                  }
                  onClick={(e) => {
                    e.stopPropagation()
                    onSelectOccurrence(occ)
                  }}
                >
                  {!band && (
                    <>
                      <span className="size-2 shrink-0 rounded-full" style={{ background: color }} />
                      <span className="shrink-0 tabular-nums" style={{ color: token.colorTextSecondary }}>
                        {formatTime(occ.startsAt, timeZone)}
                      </span>
                    </>
                  )}
                  <span className="truncate">{occ.title}</span>
                </button>
              )
            })}

            {items.length > MAX_CHIPS && (
              <button
                type="button"
                className="cursor-pointer self-start rounded px-1.5 text-xs font-medium"
                style={{ color: token.colorTextSecondary }}
                onClick={(e) => {
                  e.stopPropagation()
                  onOpenDay(day)
                }}
              >
                {t('agenda.events.more', { count: items.length - MAX_CHIPS })}
              </button>
            )}
          </div>
        )
      })}
    </div>
  )
}
