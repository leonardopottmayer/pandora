import { describe, expect, it } from 'vitest'
import type { CatalogModule } from '../models'
import { coveredKeys, matches, missingDependencies, toPatterns } from './patterns'

const action = (mod: string, resource: string, name: string) => ({
  key: `${mod}.${resource}.${name}`,
  action: name,
  requires: name === 'read' ? [] : [`${mod}.${resource}.read`],
})

const catalog: CatalogModule[] = [
  {
    key: 'finances',
    resources: [
      { key: 'cards', feature: 'finances', actions: ['read', 'delete'].map((a) => action('finances', 'cards', a)) },
      { key: 'tags', feature: 'finances', actions: ['read', 'assign'].map((a) => action('finances', 'tags', a)) },
    ],
  },
  { key: 'notes', resources: [{ key: 'pages', feature: 'notes', actions: ['read'].map((a) => action('notes', 'pages', a)) }] },
]

describe('patterns', () => {
  it('matches like the backend', () => {
    expect(matches('*', 'notes.pages.read')).toBe(true)
    expect(matches('finances.*', 'finances.cards.read')).toBe(true)
    expect(matches('finances.cards.*', 'finances.tags.read')).toBe(false)
    expect(matches('finances.cards.read', 'finances.cards.read')).toBe(true)
  })

  it('expands patterns and folds keys back into the shortest patterns', () => {
    const keys = coveredKeys(['notes.*', 'finances.cards.*', 'finances.tags.read'], catalog)
    expect([...keys].sort()).toEqual(['finances.cards.delete', 'finances.cards.read', 'finances.tags.read', 'notes.pages.read'])
    expect(toPatterns(keys, catalog)).toEqual(['finances.cards.*', 'finances.tags.read', 'notes.*'])
    expect(toPatterns(coveredKeys(['*'], catalog), catalog)).toEqual(['finances.*', 'notes.*'])
  })

  it('reports an action held without its read', () => {
    expect(missingDependencies(new Set(['finances.cards.delete', 'finances.tags.read']), catalog)).toEqual(['finances.cards.delete'])
  })
})
