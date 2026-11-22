import dayjs, { type Dayjs, type ManipulateType } from 'dayjs'
import type { RecurrenceFrequency, RecurringTransactionDto } from '../models'

const STEP_UNIT: Record<RecurrenceFrequency, ManipulateType> = {
  daily: 'day',
  weekly: 'week',
  monthly: 'month',
  yearly: 'year',
}

/** How many times a period fits in a month, on average. */
const PER_MONTH: Record<RecurrenceFrequency, number> = { daily: 365 / 12, weekly: 52 / 12, monthly: 1, yearly: 1 / 12 }

/** What a recurrence's amount comes to in an average month: weekly ×52/12, yearly ÷12, every 2 months ÷2. */
export function monthlyAmount(r: Pick<RecurringTransactionDto, 'amount' | 'frequency' | 'interval'>): number {
  return ((r.amount ?? 0) * PER_MONTH[r.frequency]) / r.interval
}

/** The first `count` dates of a schedule from `start`, stopping at its end date or after `left` of them. */
export function scheduleDates(
  start: Dayjs,
  frequency: RecurrenceFrequency,
  interval: number,
  count: number,
  end?: Dayjs | null,
  left = Infinity,
): string[] {
  const dates: string[] = []
  for (let date = start; dates.length < Math.min(count, left); date = date.add(interval, STEP_UNIT[frequency])) {
    if (end && date.isAfter(end, 'day')) break
    dates.push(date.format('YYYY-MM-DD'))
  }
  return dates
}

/** The dates a recurrence still has in the next `days`, from its next one, minding its end date and limit. */
export function upcomingDates(r: RecurringTransactionDto, days: number, today: Dayjs = dayjs().startOf('day')): string[] {
  const until = today.add(days, 'day')
  const left = r.maxOccurrences == null ? Infinity : r.maxOccurrences - r.occurrencesCount
  const end = r.endDate ? dayjs(r.endDate) : null
  const dates: string[] = []
  for (let date = dayjs(r.nextOccurrenceOn); dates.length < left; date = date.add(r.interval, STEP_UNIT[r.frequency])) {
    if (date.isAfter(until) || (end && date.isAfter(end))) break
    dates.push(date.format('YYYY-MM-DD'))
  }
  return dates
}
