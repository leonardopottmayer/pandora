import { describe, it, expect } from 'vitest'
import { buildPageIndex, renderWikilinks, resolveWikilink, slugify } from './wikilinks'
import type { PageSummaryDto } from '../models'

function page(id: string, title: string, slug: string): PageSummaryDto {
  return {
    id,
    parentId: null,
    title,
    slug,
    icon: null,
    orderIndex: 0,
    isFavorite: false,
    isArchived: false,
  }
}

const index = buildPageIndex([
  page('id-notes', 'Meeting Notes', 'meeting-notes'),
  page('id-cafe', 'Café com Pão', 'cafe-com-pao'),
])

describe('slugify', () => {
  it.each([
    ['My Note', 'my-note'],
    ['  Trimmed  ', 'trimmed'],
    ['Café com Pão', 'cafe-com-pao'],
    ['Multiple   spaces & symbols!!!', 'multiple-spaces-symbols'],
    ['---leading and trailing---', 'leading-and-trailing'],
  ])('%s → %s', (title, expected) => {
    expect(slugify(title)).toBe(expected)
  })

  it.each(['', '   ', '!!!'])('falls back to untitled for %j', (title) => {
    expect(slugify(title)).toBe('untitled')
  })
})

describe('resolveWikilink', () => {
  it('matches by title, case-insensitively', () => {
    expect(resolveWikilink('meeting notes', index)?.id).toBe('id-notes')
  })

  it('matches by slug', () => {
    expect(resolveWikilink('cafe-com-pao', index)?.id).toBe('id-cafe')
  })

  it('matches a title whose slug differs from what was typed', () => {
    expect(resolveWikilink('Café  com  Pão!', index)?.id).toBe('id-cafe')
  })

  it('returns nothing for an unknown page', () => {
    expect(resolveWikilink('Nowhere', index)).toBeUndefined()
  })
})

describe('renderWikilinks', () => {
  it('links a resolved target by id and shows the page title', () => {
    const html = renderWikilinks('see [[meeting-notes]] please', index)
    expect(html).toContain('data-page-id="id-notes"')
    expect(html).toContain('>Meeting Notes<')
  })

  it('marks a broken link with the text that was typed', () => {
    const html = renderWikilinks('see [[Nowhere]]', index)
    expect(html).toContain('notes-wikilink-missing')
    expect(html).toContain('data-page-title="Nowhere"')
  })

  it('uses the alias as the label', () => {
    expect(renderWikilinks('[[Meeting Notes|yesterday]]', index)).toContain('>yesterday<')
  })

  it('treats an embed like a link (inline embedding is not part of this phase)', () => {
    expect(renderWikilinks('![[Meeting Notes]]', index)).toContain('data-page-id="id-notes"')
  })

  it('escapes the label and the typed title', () => {
    const html = renderWikilinks('[[<img src=x onerror=alert(1)>]]', index)
    expect(html).not.toContain('<img')
    expect(html).toContain('&lt;img')
  })

  it('leaves empty and non-wikilink brackets alone', () => {
    expect(renderWikilinks('[[]] and [a](b)', index)).toBe('[[]] and [a](b)')
  })
})
