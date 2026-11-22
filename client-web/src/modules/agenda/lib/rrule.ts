import dayjs from 'dayjs'

// Small RFC 5545 RRULE helper shared by events, tasks and reminders.
// The backend expands recurrence; here we only build/parse the supported subset
// (FREQ, INTERVAL, BYDAY, COUNT, UNTIL). We deliberately do not offer
// BYSETPOS/BYWEEKNO/BYYEARDAY/BYHOUR/BYMINUTE/BYSECOND (the backend rejects them).

export const WEEKDAYS = ['MO', 'TU', 'WE', 'TH', 'FR', 'SA', 'SU'] as const
export type Weekday = (typeof WEEKDAYS)[number]

export type Frequency = 'none' | 'daily' | 'weekly' | 'monthly' | 'yearly'
export type EndMode = 'never' | 'until' | 'count'

const FREQ_TO_RFC: Record<Exclude<Frequency, 'none'>, string> = {
  daily: 'DAILY',
  weekly: 'WEEKLY',
  monthly: 'MONTHLY',
  yearly: 'YEARLY',
}

const RFC_TO_FREQ: Record<string, Exclude<Frequency, 'none'>> = {
  DAILY: 'daily',
  WEEKLY: 'weekly',
  MONTHLY: 'monthly',
  YEARLY: 'yearly',
}

export interface RecurrenceValue {
  frequency: Frequency
  interval: number
  /** Selected weekdays (only meaningful for WEEKLY). */
  byDay: Weekday[]
  endMode: EndMode
  /** ISO instant for UNTIL (only when endMode = 'until'). */
  until?: string | null
  /** Occurrence count for COUNT (only when endMode = 'count'). */
  count?: number | null
}

export const NO_RECURRENCE: RecurrenceValue = {
  frequency: 'none',
  interval: 1,
  byDay: [],
  endMode: 'never',
}

/** Formats an ISO instant into RFC 5545 UTC basic form: YYYYMMDDTHHmmssZ. */
function toRfcUntil(iso: string): string {
  // Derive UTC basic form from the canonical ISO string (no dayjs utc plugin needed).
  return new Date(iso).toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '')
}

/** Parses an RFC 5545 UNTIL token (date or date-time, with/without Z). */
function fromRfcUntil(token: string): string | null {
  const m = token.match(/^(\d{4})(\d{2})(\d{2})(?:T(\d{2})(\d{2})(\d{2})Z?)?$/)
  if (!m) return null
  const [, y, mo, d, h = '00', mi = '00', s = '00'] = m
  const iso = `${y}-${mo}-${d}T${h}:${mi}:${s}Z`
  const parsed = dayjs(iso)
  return parsed.isValid() ? parsed.toISOString() : null
}

/** Builds an RRULE string, or null when there is no recurrence. */
export function buildRrule(value: RecurrenceValue): string | null {
  if (value.frequency === 'none') return null

  const parts = [`FREQ=${FREQ_TO_RFC[value.frequency]}`]

  const interval = Math.max(1, Math.trunc(value.interval || 1))
  if (interval > 1) parts.push(`INTERVAL=${interval}`)

  if (value.frequency === 'weekly' && value.byDay.length > 0) {
    parts.push(`BYDAY=${value.byDay.join(',')}`)
  }

  if (value.endMode === 'count' && value.count && value.count > 0) {
    parts.push(`COUNT=${Math.trunc(value.count)}`)
  } else if (value.endMode === 'until' && value.until) {
    parts.push(`UNTIL=${toRfcUntil(value.until)}`)
  }

  return parts.join(';')
}

/** Parses an RRULE string into the editable value (falls back to NO_RECURRENCE). */
export function parseRrule(rrule: string | null | undefined): RecurrenceValue {
  if (!rrule || rrule.trim() === '') return { ...NO_RECURRENCE }

  const map = new Map<string, string>()
  for (const part of rrule.split(';')) {
    const [key, val] = part.split('=')
    if (key && val) map.set(key.trim().toUpperCase(), val.trim())
  }

  const freqToken = (map.get('FREQ') ?? '').toUpperCase()
  const frequency = RFC_TO_FREQ[freqToken]
  if (!frequency) return { ...NO_RECURRENCE }

  const interval = Number.parseInt(map.get('INTERVAL') ?? '1', 10) || 1

  const byDay = (map.get('BYDAY') ?? '')
    .split(',')
    .map((d) => d.trim().toUpperCase())
    .filter((d): d is Weekday => (WEEKDAYS as readonly string[]).includes(d))

  let endMode: EndMode = 'never'
  let until: string | null = null
  let count: number | null = null
  if (map.has('COUNT')) {
    endMode = 'count'
    count = Number.parseInt(map.get('COUNT') ?? '', 10) || null
  } else if (map.has('UNTIL')) {
    const parsed = fromRfcUntil(map.get('UNTIL') ?? '')
    if (parsed) {
      endMode = 'until'
      until = parsed
    }
  }

  return { frequency, interval, byDay, endMode, until, count }
}
