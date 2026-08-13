import { describe, it, expect } from 'vitest'
import { buildTree } from './buildTree'
import type { PageSummaryDto } from '../models'

function page(id: string, parentId: string | null, orderIndex = 0): PageSummaryDto {
  return {
    id,
    parentId,
    title: id,
    slug: id,
    icon: null,
    orderIndex,
    isFavorite: false,
    isArchived: false,
  }
}

describe('buildTree', () => {
  it('nests children under their parent', () => {
    const tree = buildTree([page('a', null), page('b', 'a'), page('c', 'b')])

    expect(tree).toHaveLength(1)
    expect(tree[0].id).toBe('a')
    expect(tree[0].children[0].id).toBe('b')
    expect(tree[0].children[0].children[0].id).toBe('c')
  })

  it('keeps the backend order among siblings', () => {
    const tree = buildTree([page('x', null, 0), page('y', null, 1), page('z', null, 2)])
    expect(tree.map((n) => n.id)).toEqual(['x', 'y', 'z'])
  })

  it('surfaces orphans at the root so they stay reachable', () => {
    // 'b' points at a parent that was filtered out (e.g. archived).
    const tree = buildTree([page('b', 'missing')])
    expect(tree.map((n) => n.id)).toEqual(['b'])
  })

  it('returns an empty tree for an empty list', () => {
    expect(buildTree([])).toEqual([])
  })
})
