import dayjs from 'dayjs'

// Dates typed after `@`: words (today, tomorrow, yesterday) or a date (25/12, 25/12/2026, 2026-12-25),
// with an optional time (14:00, 14h). A date chip stores `YYYY-MM-DD` or `YYYY-MM-DDTHH:mm`, local.

export interface DateSuggestion {
  value: string
  /** The word typed, as an i18n key (`today`, `tomorrow`, `yesterday`), or null for a typed date. */
  word: 'today' | 'tomorrow' | 'yesterday' | null
  /** Asked with "remind" ("lembrar"): the chip is made with its Agenda reminder. */
  reminder: boolean
}

const WORDS: Record<string, DateSuggestion['word']> = {
  hoje: 'today', today: 'today',
  amanha: 'tomorrow', 'amanhã': 'tomorrow', tomorrow: 'tomorrow',
  ontem: 'yesterday', yesterday: 'yesterday',
}
const OFFSET = { today: 0, tomorrow: 1, yesterday: -1 }

const pad = (n: number) => String(n).padStart(2, '0')
const day = (date: Date) => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`

/** The dates a query after `@` may mean; with nothing typed, today and tomorrow. */
export function dateSuggestions(query: string, now: Date = new Date()): DateSuggestion[] {
  let text = query.trim().toLowerCase()
  const reminder = /^(lembr\w*|remind\w*)(\s+(me|de|em|on))?\s*/.exec(text)
  if (reminder) text = text.slice(reminder[0].length)
  const time = /\s+(\d{1,2})(?::(\d{2})|h(\d{2})?)$/.exec(text)
  if (time) text = text.slice(0, time.index)
  const clock = time ? `T${pad(Math.min(Number(time[1]), 23))}:${pad(Math.min(Number(time[2] ?? time[3] ?? 0), 59))}` : ''
  const make = (date: Date, word: DateSuggestion['word']): DateSuggestion => ({ value: day(date) + clock, word, reminder: !!reminder })
  const shifted = (days: number) => new Date(now.getFullYear(), now.getMonth(), now.getDate() + days)

  if (!text) return reminder || clock ? [make(shifted(1), 'tomorrow')] : [make(shifted(0), 'today'), make(shifted(1), 'tomorrow')]
  const word = Object.entries(WORDS).filter(([key]) => key.startsWith(text)).map(([, value]) => value)
  if (word.length) return [...new Set(word)].map((w) => make(shifted(OFFSET[w!]), w))

  const br = /^(\d{1,2})\/(\d{1,2})(?:\/(\d{2}|\d{4}))?$/.exec(text)
  const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(text)
  const [y, m, d] = br
    ? [br[3] ? Number(br[3].length === 2 ? `20${br[3]}` : br[3]) : now.getFullYear(), Number(br[2]), Number(br[1])]
    : iso ? [Number(iso[1]), Number(iso[2]), Number(iso[3])] : [0, 0, 0]
  if (!y) return []
  const date = new Date(y, m - 1, d)
  // 31/02 rolls over in Date: refuse it instead of landing in March.
  return date.getMonth() === m - 1 && date.getDate() === d ? [make(date, null)] : []
}

/** Whether a chip date carries a time. */
export const hasTime = (value: string) => value.includes('T')

/** The moment a reminder rings for a chip date: its time, or 9:00 for a whole day. */
export function reminderMoment(value: string): string {
  return dayjs(hasTime(value) ? value : `${value}T09:00`).toISOString()
}

/** A chip date as the reader sees it: today/tomorrow/yesterday by name, else the date in the locale. */
export function formatChipDate(value: string, locale: string, words: (word: 'today' | 'tomorrow' | 'yesterday') => string, now: Date = new Date()): string {
  const date = dayjs(value)
  if (!date.isValid()) return value
  const days = date.startOf('day').diff(dayjs(now).startOf('day'), 'day')
  const name = days === 0 ? words('today') : days === 1 ? words('tomorrow') : days === -1 ? words('yesterday') : null
  const shown = name ?? date.toDate().toLocaleDateString(locale, { day: 'numeric', month: 'short', year: date.year() === now.getFullYear() ? undefined : 'numeric' })
  return hasTime(value) ? `${shown} ${date.format('HH:mm')}` : shown
}
