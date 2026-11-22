import { describe, expect, it } from 'vitest'
import dayjs from 'dayjs'
import { daysFromToday, formatDue, occurrenceDays } from './datetime'

const TZ = 'America/Sao_Paulo'
// Monday, Nov 2 2026, 22:00 in São Paulo — already Tuesday in UTC.
const now = dayjs('2026-11-03T01:00:00Z')

describe('daysFromToday', () => {
  it('counts calendar days in the account zone, not in UTC', () => {
    expect(daysFromToday('2026-11-03T02:00:00Z', TZ, now)).toBe(0) // 23:00 Monday there
    expect(daysFromToday('2026-11-03T03:00:00Z', TZ, now)).toBe(1) // 00:00 Tuesday there
    expect(daysFromToday('2026-11-01T12:00:00Z', TZ, now)).toBe(-1)
  })
})

describe('formatDue', () => {
  it('says today, tomorrow and yesterday, with the time only when the task has one', () => {
    expect(formatDue('2026-11-02T15:00:00Z', false, TZ, 'en', now)).toBe('Today')
    expect(formatDue('2026-11-03T17:30:00Z', true, TZ, 'en', now)).toBe('Tomorrow 14:30')
    expect(formatDue('2026-11-01T15:00:00Z', false, TZ, 'pt-BR', now)).toBe('Ontem')
  })

  it('names the weekday within the week, and the date beyond it', () => {
    expect(formatDue('2026-11-06T15:00:00Z', false, TZ, 'en', now)).toBe('Friday')
    expect(formatDue('2026-11-20T15:00:00Z', false, TZ, 'en', now)).toBe('Nov 20')
    expect(formatDue('2027-01-05T15:00:00Z', false, TZ, 'en', now)).toBe('Jan 5, 2027')
  })
})

describe('occurrenceDays', () => {
  it('lists every date an event touches in the account zone', () => {
    // Fri 22:00 to Sun 10:00 in São Paulo.
    expect(occurrenceDays('2026-11-07T01:00:00Z', '2026-11-08T13:00:00Z', TZ)).toEqual([
      '2026-11-06',
      '2026-11-07',
      '2026-11-08',
    ])
  })

  it('does not spill into the next day when ending at midnight', () => {
    expect(occurrenceDays('2026-11-06T03:00:00Z', '2026-11-07T03:00:00Z', TZ)).toEqual(['2026-11-06'])
  })
})
