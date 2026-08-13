import { describe, it, expect } from 'vitest'
import { computeReorder, isSelfOrDescendant } from './moveMath'
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

// a, b, c at the root; a has children a1, a2.
const tree = buildTree([
  page('a', null, 0),
  page('b', null, 1),
  page('c', null, 2),
  page('a1', 'a', 0),
  page('a2', 'a', 1),
])

describe('isSelfOrDescendant', () => {
  it('flags the node itself', () => {
    expect(isSelfOrDescendant(tree, 'a', 'a')).toBe(true)
  })

  it('flags a direct child and a deeper descendant', () => {
    expect(isSelfOrDescendant(tree, 'a', 'a1')).toBe(true)
  })

  it('does not flag an unrelated node', () => {
    expect(isSelfOrDescendant(tree, 'a', 'b')).toBe(false)
  })
})

describe('computeReorder', () => {
  it('appends the page as the last child when dropped onto a node', () => {
    const moves = computeReorder(tree, 'c', 'a', 0, false)

    expect(moves).toEqual([
      { id: 'a1', body: { parentId: 'a', orderIndex: 0 } },
      { id: 'a2', body: { parentId: 'a', orderIndex: 1 } },
      { id: 'c', body: { parentId: 'a', orderIndex: 2 } },
    ])
  })

  it('inserts above the drop node when dropped into the gap before it', () => {
    const moves = computeReorder(tree, 'c', 'b', -1, true)

    expect(moves.map((m) => m.id)).toEqual(['a', 'c', 'b'])
    expect(moves.every((m) => m.body.parentId === null)).toBe(true)
  })

  it('inserts below the drop node when dropped into the gap after it', () => {
    const moves = computeReorder(tree, 'a', 'b', 1, true)
    expect(moves.map((m) => m.id)).toEqual(['b', 'a', 'c'])
  })

  it('renumbers the destination siblings sequentially from zero', () => {
    const moves = computeReorder(tree, 'c', 'a', -1, true)
    expect(moves.map((m) => m.body.orderIndex)).toEqual([0, 1, 2])
  })

  it('reparents a nested page to the root', () => {
    const moves = computeReorder(tree, 'a1', 'b', 1, true)

    expect(moves.map((m) => m.id)).toEqual(['a', 'b', 'a1', 'c'])
    expect(moves.every((m) => m.body.parentId === null)).toBe(true)
  })

  it('returns no moves when the drop target is unknown', () => {
    expect(computeReorder(tree, 'a', 'ghost', 1, true)).toEqual([])
  })
})
