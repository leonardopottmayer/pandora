import { describe, expect, it } from 'vitest'
import { filterTags, tagSlug } from './tags'
import type { TagDto } from '../models'

function tag(overrides: Partial<TagDto> & { slug: string }): TagDto {
  return { id: overrides.slug, name: overrides.slug, color: null, pageCount: 0, ...overrides }
}

describe('tagSlug', () => {
  it('lower-cases and strips accents, keeping the separators', () => {
    expect(tagSlug('Café')).toBe('cafe')
    expect(tagSlug('#Café')).toBe('cafe')
    expect(tagSlug('PROJETO/Pandora')).toBe('projeto/pandora')
    expect(tagSlug('com_underline')).toBe('com_underline')
  })

  it('is empty when nothing with a letter is left', () => {
    expect(tagSlug('123')).toBe('')
    expect(tagSlug('---')).toBe('')
  })
})

describe('filterTags', () => {
  const tags = [tag({ slug: 'cafe', name: 'Café' }), tag({ slug: 'pandora', name: 'Pandora' })]

  it('matches by slug, so an accented name is reachable from plain text', () => {
    expect(filterTags(tags, 'cafe').map((found) => found.slug)).toEqual(['cafe'])
  })

  it('returns everything when nothing was typed yet', () => {
    expect(filterTags(tags, '')).toHaveLength(2)
  })

  it('caps the list', () => {
    const many = Array.from({ length: 30 }, (_, i) => tag({ slug: `t${i}a` }))
    expect(filterTags(many, '')).toHaveLength(10)
  })
})
