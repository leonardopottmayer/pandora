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

/** Formats an ISO instant as a time only, in the account's time zone. */
export function formatTime(iso: string | null | undefined, timeZone: string): string {
  if (!iso) return '—'
  return dayjs(iso).tz(timeZone).format('HH:mm')
}
