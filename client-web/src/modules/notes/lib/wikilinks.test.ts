import { describe, it, expect } from 'vitest'
import {
  buildPageIndex,
  filterPages,
  renderWikilinks,
  resolveWikilink,
  slugify,
  wikilinkTriggerAt,
} from './wikilinks'
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

describe('wikilinkTriggerAt', () => {
  it('opens on the bare brackets with an empty query', () => {
    expect(wikilinkTriggerAt('see [[', 6)).toEqual({ from: 6, query: '' })
  })

  it('collects what was typed after the brackets, spaces included', () => {
    expect(wikilinkTriggerAt('see [[meeting no', 16)).toEqual({ from: 6, query: 'meeting no' })
  })

  it('triggers on an embed too', () => {
    expect(wikilinkTriggerAt('![[meet', 7)?.query).toBe('meet')
  })

  it('stays quiet outside a link', () => {
    expect(wikilinkTriggerAt('just a paragraph', 6)).toBeNull()
  })

  it('stays quiet once the link is closed', () => {
    expect(wikilinkTriggerAt('[[Meeting Notes]] and more', 26)).toBeNull()
  })

  it('stays quiet on the alias half', () => {
    expect(wikilinkTriggerAt('[[Meeting Notes|yester', 22)).toBeNull()
  })

  it('reads the innermost brackets when the cursor sits after a finished link', () => {
    const line = '[[Meeting Notes]] then [[caf'
    expect(wikilinkTriggerAt(line, line.length)?.query).toBe('caf')
  })
})

describe('filterPages', () => {
  const pages = [page('id-notes', 'Meeting Notes', 'meeting-notes'), page('id-cafe', 'Café com Pão', 'cafe-com-pao')]

  it('offers everything on an empty query', () => {
    expect(filterPages(pages, '')).toHaveLength(2)
  })

  it('matches part of a title, case-insensitively', () => {
    expect(filterPages(pages, 'notes').map((p) => p.id)).toEqual(['id-notes'])
  })

  it('matches by slug, so the accent-free spelling finds the page', () => {
    expect(filterPages(pages, 'cafe').map((p) => p.id)).toEqual(['id-cafe'])
  })

  it('caps the list', () => {
    expect(filterPages(pages, '', 1)).toHaveLength(1)
  })

  it('returns nothing when no page matches', () => {
    expect(filterPages(pages, 'zzz')).toEqual([])
  })
})
