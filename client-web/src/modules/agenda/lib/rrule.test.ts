import { describe, it, expect } from 'vitest'
import { buildRrule, parseRrule, NO_RECURRENCE, type RecurrenceValue } from './rrule'

describe('buildRrule', () => {
  it('returns null when there is no recurrence', () => {
    expect(buildRrule(NO_RECURRENCE)).toBeNull()
  })

  it('builds a weekly rule with selected days', () => {
    const value: RecurrenceValue = {
      frequency: 'weekly',
      interval: 1,
      byDay: ['MO', 'WE', 'FR'],
      endMode: 'never',
    }
    expect(buildRrule(value)).toBe('FREQ=WEEKLY;BYDAY=MO,WE,FR')
  })

  it('includes INTERVAL only when greater than 1', () => {
    expect(buildRrule({ frequency: 'daily', interval: 2, byDay: [], endMode: 'never' })).toBe(
      'FREQ=DAILY;INTERVAL=2',
    )
    expect(buildRrule({ frequency: 'daily', interval: 1, byDay: [], endMode: 'never' })).toBe(
      'FREQ=DAILY',
    )
  })

  it('emits COUNT for the count end mode', () => {
    expect(
      buildRrule({ frequency: 'daily', interval: 1, byDay: [], endMode: 'count', count: 5 }),
    ).toBe('FREQ=DAILY;COUNT=5')
  })

  it('emits UNTIL in RFC basic UTC form', () => {
    const value: RecurrenceValue = {
      frequency: 'monthly',
      interval: 1,
      byDay: [],
      endMode: 'until',
      until: '2026-12-31T23:59:59.000Z',
    }
    expect(buildRrule(value)).toBe('FREQ=MONTHLY;UNTIL=20261231T235959Z')
  })

  it('omits BYDAY for non-weekly frequencies', () => {
    expect(
      buildRrule({ frequency: 'monthly', interval: 1, byDay: ['MO'], endMode: 'never' }),
    ).toBe('FREQ=MONTHLY')
  })
})

describe('parseRrule', () => {
  it('returns NO_RECURRENCE for empty input', () => {
    expect(parseRrule(null)).toEqual(NO_RECURRENCE)
    expect(parseRrule('')).toEqual(NO_RECURRENCE)
  })

  it('parses a weekly rule with days', () => {
    expect(parseRrule('FREQ=WEEKLY;BYDAY=MO,WE,FR;INTERVAL=1')).toEqual({
      frequency: 'weekly',
      interval: 1,
      byDay: ['MO', 'WE', 'FR'],
      endMode: 'never',
      until: null,
      count: null,
    })
  })

  it('parses COUNT', () => {
    const parsed = parseRrule('FREQ=DAILY;COUNT=10')
    expect(parsed.endMode).toBe('count')
    expect(parsed.count).toBe(10)
  })

  it('parses UNTIL back to an ISO instant', () => {
    const parsed = parseRrule('FREQ=MONTHLY;UNTIL=20261231T235959Z')
    expect(parsed.endMode).toBe('until')
    expect(parsed.until).toBe('2026-12-31T23:59:59.000Z')
  })

  it('round-trips through build and parse', () => {
    const value: RecurrenceValue = {
      frequency: 'weekly',
      interval: 3,
      byDay: ['TU', 'TH'],
      endMode: 'count',
      count: 8,
      until: null,
    }
    const rrule = buildRrule(value)
    expect(rrule).not.toBeNull()
    expect(parseRrule(rrule)).toEqual({ ...value, until: null })
  })
})
