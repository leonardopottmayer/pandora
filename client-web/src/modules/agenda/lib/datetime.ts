import dayjs, { type Dayjs } from 'dayjs'
import utc from 'dayjs/plugin/utc'
import timezone from 'dayjs/plugin/timezone'
import type { WeekStartsOn } from '@/modules/identity/models'

dayjs.extend(utc)
dayjs.extend(timezone)

const WALL = 'YYYY-MM-DDTHH:mm:ss'

/**
 * A stored instant as a date-picker value. Pickers work in the device's zone, so the value holds the
 * account zone's wall clock as a plain Dayjs: a phone set to Lisbon still shows São Paulo's 10:00.
 */
export function toPicker(iso: string, timeZone: string): Dayjs {
  return dayjs(dayjs(iso).tz(timeZone).format(WALL))
}

/** Now in the account zone, as a picker value (see `toPicker`). */
export function nowPicker(timeZone: string): Dayjs {
  return dayjs(dayjs().tz(timeZone).format(WALL))
}

/**
 * A picker value back to an instant: the wall clock it shows, read in the account zone, as ISO-8601 with
 * that zone's offset ("2026-11-14T10:00:00-03:00"). The offset also carries the date the user picked,
 * which is what the backend keeps for a date-only due or an all-day event.
 */
export function fromPicker(value: Dayjs, timeZone: string): string {
  return dayjs.tz(value.format(WALL), timeZone).format()
}

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

/** The calendar date of an instant in the account's zone, as YYYY-MM-DD. */
export function dayKey(instant: string | Dayjs, timeZone: string): string {
  return dayjs(instant).tz(timeZone).format('YYYY-MM-DD')
}

/** Minutes since midnight of an instant, in the account's zone. */
export function minuteOfDay(instant: string, timeZone: string): number {
  const d = dayjs(instant).tz(timeZone)
  return d.hour() * 60 + d.minute()
}

/** Every date an event touches in the account's zone. Ending at midnight does not touch the next day. */
export function occurrenceDays(startsAt: string, endsAt: string, timeZone: string): string[] {
  const lastInstant = Math.max(dayjs(startsAt).valueOf(), dayjs(endsAt).valueOf() - 1)
  const last = dayKey(dayjs(lastInstant), timeZone)
  const days: string[] = []
  for (let d = dayjs.utc(dayKey(startsAt, timeZone)); d.format('YYYY-MM-DD') <= last; d = d.add(1, 'day')) {
    days.push(d.format('YYYY-MM-DD'))
  }
  return days
}

/** A date key at noon UTC: formatted with `timeZone: 'UTC'` it reads as that date anywhere. */
export const keyToDate = (key: string) => new Date(`${key}T12:00:00Z`)

/** "Mon, Nov 2, 14:00 – 15:00", or the dates alone for an all-day event; the year only when not this one. */
export function formatEventRange(
  startsAt: string,
  endsAt: string,
  isAllDay: boolean,
  timeZone: string,
  locale: string,
): string {
  const start = new Date(startsAt)
  const end = new Date(isAllDay ? Math.max(start.getTime(), new Date(endsAt).getTime() - 1) : endsAt)
  const thisYear = dayjs().tz(timeZone).year() === dayjs(startsAt).tz(timeZone).year()
  return new Intl.DateTimeFormat(locale, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: thisYear ? undefined : 'numeric',
    ...(isAllDay ? {} : { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }),
    timeZone,
  }).formatRange(start, end)
}

/** Formats an ISO instant as a time only, in the account's time zone. */
export function formatTime(iso: string | null | undefined, timeZone: string): string {
  if (!iso) return '—'
  return dayjs(iso).tz(timeZone).format('HH:mm')
}
