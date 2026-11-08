import { describe, expect, it } from 'vitest'
import { dateSuggestions, formatChipDate } from './chipDates'
import { filterEmojis } from './emojis'

const now = new Date(2026, 10, 7, 10, 30) // 7 Nov 2026
const values = (query: string) => dateSuggestions(query, now).map((date) => date.value)

describe('dateSuggestions', () => {
  it('offers today and tomorrow when nothing is typed', () => {
    expect(values('')).toEqual(['2026-11-07', '2026-11-08'])
  })

  it('reads words in both languages, and a time after them', () => {
    expect(values('amanhã')).toEqual(['2026-11-08'])
    expect(values('yesterday')).toEqual(['2026-11-06'])
    expect(values('hoje 14:30')).toEqual(['2026-11-07T14:30'])
    expect(values('tomorrow 9h')).toEqual(['2026-11-08T09:00'])
  })

  it('reads typed dates, and refuses one that does not exist', () => {
    expect(values('25/12')).toEqual(['2026-12-25'])
    expect(values('01/02/27')).toEqual(['2027-02-01'])
    expect(values('2027-03-04')).toEqual(['2027-03-04'])
    expect(values('31/02')).toEqual([])
    expect(values('reunião')).toEqual([])
  })

  it('makes a reminder when asked to remind', () => {
    expect(dateSuggestions('lembrar amanhã 8:00', now)).toEqual([{ value: '2026-11-08T08:00', word: 'tomorrow', reminder: true }])
    expect(dateSuggestions('remind me', now)[0]).toMatchObject({ value: '2026-11-08', reminder: true })
  })
})

describe('formatChipDate', () => {
  const words = (word: string) => word.toUpperCase()

  it('names the days next to today, and dates the rest', () => {
    expect(formatChipDate('2026-11-07', 'en', words, now)).toBe('TODAY')
    expect(formatChipDate('2026-11-08T14:00', 'en', words, now)).toBe('TOMORROW 14:00')
    expect(formatChipDate('2026-12-25', 'en', words, now)).toBe('Dec 25')
    expect(formatChipDate('2027-01-02', 'en', words, now)).toBe('Jan 2, 2027')
  })
})

describe('filterEmojis', () => {
  it('finds by a word start in either language, and waits for a letter', () => {
    expect(filterEmojis('fogo').map((emoji) => emoji.char)).toEqual(['🔥'])
    expect(filterEmojis('rocket').map((emoji) => emoji.char)).toEqual(['🚀'])
    expect(filterEmojis('')).toEqual([])
  })
})
