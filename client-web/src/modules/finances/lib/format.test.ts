import { describe, it, expect, beforeAll } from 'vitest'
import i18n from '@/i18n'
import { formatMoney, formatDate, formatDateTime, formatReferenceMonth } from './format'

// Fix the locale to make Intl output deterministic (pt-BR uses NBSP/comma).
beforeAll(async () => {
  await i18n.changeLanguage('pt-BR')
})

describe('formatMoney', () => {
  it('formats a known ISO currency', () => {
    const out = formatMoney(1234.5, 'BRL')
    expect(out).toContain('R$')
    expect(out).toContain('1.234,50')
  })

  it('falls back to decimals + code for unknown crypto tickers', () => {
    const out = formatMoney(0.5, 'XYZ')
    expect(out).toContain('XYZ')
    expect(out).toContain('0,5')
  })
})

describe('formatDate', () => {
  it('formats a DateOnly without timezone drift', () => {
    expect(formatDate('2026-06-13')).toBe('13/06/2026')
  })

  it('returns empty string for null', () => {
    expect(formatDate(null)).toBe('')
  })
})

describe('formatDateTime', () => {
  it('renders a value for a valid ISO instant', () => {
    expect(formatDateTime('2026-06-13T10:30:00Z')).not.toBe('')
  })

  it('returns empty string for null', () => {
    expect(formatDateTime(null)).toBe('')
  })
})

describe('formatReferenceMonth', () => {
  it('formats yyyy-MM as month/year', () => {
    const out = formatReferenceMonth('2026-06')
    expect(out.toLowerCase()).toContain('2026')
  })
})
