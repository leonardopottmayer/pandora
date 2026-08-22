import dayjs from 'dayjs'

/** The browser's IANA time zone, sent when creating agenda entities. */
export function browserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone
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
