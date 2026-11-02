import dayjs, { type Dayjs } from 'dayjs'
import utc from 'dayjs/plugin/utc'
import timezone from 'dayjs/plugin/timezone'
import type { WeekStartsOn } from '@/modules/identity/models'

dayjs.extend(utc)
dayjs.extend(timezone)

/** Start-of-day of the week containing `date`, honouring the user's `weekStartsOn` preference. */
export function startOfWeek(date: Dayjs, weekStartsOn: WeekStartsOn): Dayjs {
  const startIdx = weekStartsOn === 'monday' ? 1 : 0
  const offset = (date.day() - startIdx + 7) % 7
  return date.startOf('day').subtract(offset, 'day')
}

/** The seven start-of-day dates of the week containing `date`, in display order. */
export function weekDays(date: Dayjs, weekStartsOn: WeekStartsOn): Dayjs[] {
  const start = startOfWeek(date, weekStartsOn)
  return Array.from({ length: 7 }, (_, i) => start.add(i, 'day'))
}

/**
 * Formats an ISO instant as a date + time in the account's time zone (e.g. "Aug 21, 2026 14:30").
 * The zone is the user's Identity preference, not the device's, so the displayed time matches when a
 * reminder actually fires regardless of the device the grid is viewed on.
 */
export function formatDateTime(iso: string | null | undefined, timeZone: string): string {
  if (!iso) return '—'
  return dayjs(iso).tz(timeZone).format('MMM D, YYYY HH:mm')
}

/** Formats an ISO instant as a date only, in the account's time zone. */
export function formatDate(iso: string | null | undefined, timeZone: string): string {
  if (!iso) return '—'
  return dayjs(iso).tz(timeZone).format('MMM D, YYYY')
}

/** Calendar days from today to `iso` in the account's time zone: 0 today, 1 tomorrow, -1 yesterday. */
export function daysFromToday(iso: string, timeZone: string, now: Dayjs = dayjs()): number {
  // Compare calendar dates, not instants, so a 23-hour DST day still counts as one.
  const date = (d: Dayjs) => dayjs.utc(d.tz(timeZone).format('YYYY-MM-DD'))
  return date(dayjs(iso)).diff(date(now), 'day')
}

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

/** A due date the way people say it, in the account's zone: "Today", "Tomorrow 14:30", "Friday", "Nov 12". */
export function formatDue(iso: string, hasTime: boolean, timeZone: string, locale: string, now: Dayjs = dayjs()): string {
  const instant = new Date(iso)
  const days = daysFromToday(iso, timeZone, now)
  let day: string
  if (Math.abs(days) <= 1) {
    day = capitalize(new Intl.RelativeTimeFormat(locale, { numeric: 'auto' }).format(days, 'day'))
  } else if (days > 1 && days < 7) {
    day = capitalize(new Intl.DateTimeFormat(locale, { weekday: 'long', timeZone }).format(instant))
  } else {
    const sameYear = dayjs(iso).tz(timeZone).year() === now.tz(timeZone).year()
    day = new Intl.DateTimeFormat(locale, {
      day: 'numeric',
      month: 'short',
      year: sameYear ? undefined : 'numeric',
      timeZone,
    }).format(instant)
  }
  if (!hasTime) return day
  return `${day} ${new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone }).format(instant)}`
}

/** Formats an ISO instant as a time only, in the account's time zone. */
export function formatTime(iso: string | null | undefined, timeZone: string): string {
  if (!iso) return '—'
  return dayjs(iso).tz(timeZone).format('HH:mm')
}
