import { describe, expect, it } from 'vitest'
import { timeAgo } from './timeAgo'

const now = new Date('2026-11-02T12:00:00Z')

describe('timeAgo', () => {
  it('says it in the largest whole unit', () => {
    expect(timeAgo('2026-11-02T11:59:30Z', 'en', now)).toBe('now')
    expect(timeAgo('2026-11-02T09:00:00Z', 'en', now)).toBe('3 hours ago')
    expect(timeAgo('2026-11-01T12:00:00Z', 'en', now)).toBe('yesterday')
    expect(timeAgo('2026-10-20T12:00:00Z', 'pt-BR', now)).toBe('semana passada')
    expect(timeAgo('2026-08-01T12:00:00Z', 'pt-BR', now)).toBe('há 3 meses')
  })
})
