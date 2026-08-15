import { describe, expect, it } from 'vitest'
import { buildTagIndex, filterTags, parseTags, renderTags, tagSlug, tagTriggerAt } from './tags'
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

describe('parseTags', () => {
  it('finds tags in reading order, one per slug', () => {
    expect(parseTags('Sobre #ideias, #pandora e #Ideias')).toEqual(['ideias', 'pandora'])
  })

  it('ignores headings, glued hashes and pure numbers', () => {
    expect(parseTags('# Título')).toEqual([])
    expect(parseTags('https://x.com/a#frag e src/lib#2')).toEqual([])
    expect(parseTags('issue #123')).toEqual([])
  })

  it('does not read inside code', () => {
    expect(parseTags('```bash\n#naovale\n```\n\n#vale')).toEqual(['vale'])
    expect(parseTags('use `#naovale` mas #vale')).toEqual(['vale'])
  })

  // The preview and the save have to agree on what a tag is, so this mirrors TagParserTests.
  it('matches the backend on a tag at the start of a line', () => {
    expect(parseTags('linha um\n#inicio da linha dois')).toEqual(['inicio'])
  })
})

describe('renderTags', () => {
  const index = buildTagIndex([tag({ slug: 'cafe', name: 'Café', color: '#7c3aed' })])

  it('wraps a tag in a chip carrying its slug', () => {
    const html = renderTags('tomando #Café agora', index)

    expect(html).toContain('class="notes-tag"')
    expect(html).toContain('data-tag-slug="cafe"')
    expect(html).toContain('#Café')
  })

  it('paints a tag that has a color', () => {
    expect(renderTags('#Café', index)).toContain('color: #7c3aed')
  })

  it('leaves an unknown tag unpainted but still a chip', () => {
    const html = renderTags('#semcor', index)

    expect(html).toContain('data-tag-slug="semcor"')
    expect(html).not.toContain('style=')
  })

  it('leaves code untouched', () => {
    expect(renderTags('`#naovale`', index)).toBe('`#naovale`')
  })
})

describe('tagTriggerAt', () => {
  it('opens after a hash that starts the line or follows a space', () => {
    expect(tagTriggerAt('#ide', 4)).toEqual({ from: 1, query: 'ide' })
    expect(tagTriggerAt('sobre #ide', 10)).toEqual({ from: 7, query: 'ide' })
  })

  it('declines when the hash is glued to something else', () => {
    expect(tagTriggerAt('src/lib#2', 9)).toBeNull()
  })

  it('closes once a space is typed', () => {
    expect(tagTriggerAt('#ideias e ', 10)).toBeNull()
  })

  it('declines with no hash before the cursor', () => {
    expect(tagTriggerAt('nada aqui', 9)).toBeNull()
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
