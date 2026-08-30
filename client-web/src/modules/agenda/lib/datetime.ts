import dayjs, { type Dayjs } from 'dayjs'
import type { WeekStartsOn } from '@/modules/identity/models'

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

/** Formats an ISO instant as a localised date + time (e.g. "Aug 21, 2026 14:30"). */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  return dayjs(iso).format('MMM D, YYYY HH:mm')
}

/** Formats an ISO instant as a localised date only. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  return dayjs(iso).format('MMM D, YYYY')
}

/** Formats an ISO instant as a localised time only. */
export function formatTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  return dayjs(iso).format('HH:mm')
}
