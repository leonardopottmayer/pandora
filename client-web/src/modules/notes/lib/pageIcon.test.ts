import { describe, expect, it } from 'vitest'
import { normalizeIcon } from './pageIcon'

describe('normalizeIcon', () => {
  it('keeps a single emoji', () => {
    expect(normalizeIcon('📚')).toBe('📚')
  })

  it('keeps a multi-codepoint emoji whole', () => {
    // Slicing by index would leave half of these behind.
    expect(normalizeIcon('👨‍👩‍👧')).toBe('👨‍👩‍👧')
    expect(normalizeIcon('🇧🇷')).toBe('🇧🇷')
    expect(normalizeIcon('👍🏽')).toBe('👍🏽')
  })

  it('takes only the first grapheme, so the field cannot become a second title', () => {
    expect(normalizeIcon('📚📝')).toBe('📚')
    expect(normalizeIcon('notas')).toBe('n')
  })

  it('trims, and reads blank as no icon', () => {
    expect(normalizeIcon('  🎯  ')).toBe('🎯')
    expect(normalizeIcon('')).toBeNull()
    expect(normalizeIcon('   ')).toBeNull()
  })
})
