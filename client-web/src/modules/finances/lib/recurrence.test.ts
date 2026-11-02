import { describe, it, expect } from 'vitest'
import dayjs from 'dayjs'
import type { RecurringTransactionDto } from '../models'
import { monthlyAmount, upcomingDates } from './recurrence'

const base = {
  frequency: 'monthly',
  interval: 1,
  amount: 100,
  nextOccurrenceOn: '2026-11-05',
  endDate: null,
  maxOccurrences: null,
  occurrencesCount: 0,
} as RecurringTransactionDto

const today = dayjs('2026-11-02')

describe('monthlyAmount', () => {
  it('spreads each frequency over an average month', () => {
    expect(monthlyAmount({ ...base, frequency: 'weekly' })).toBeCloseTo((100 * 52) / 12)
    expect(monthlyAmount({ ...base, frequency: 'yearly' })).toBeCloseTo(100 / 12)
    expect(monthlyAmount({ ...base, interval: 2 })).toBe(50)
  })
})

describe('upcomingDates', () => {
  it('steps from the next occurrence within the window', () => {
    expect(upcomingDates({ ...base, frequency: 'weekly' }, 30, today)).toEqual([
      '2026-11-05',
      '2026-11-12',
      '2026-11-19',
      '2026-11-26',
    ])
  })

  it('stops at the end date and at the occurrences left', () => {
    expect(upcomingDates({ ...base, frequency: 'weekly', endDate: '2026-11-15' }, 30, today)).toHaveLength(2)
    expect(upcomingDates({ ...base, frequency: 'weekly', maxOccurrences: 5, occurrencesCount: 4 }, 30, today)).toHaveLength(1)
  })
})
