import { describe, expect, it } from 'vitest'
import { familyBounds, familyForce, familyForNode, type GraphFamily, type PositionedNode } from './graphGroups'

const work: GraphFamily = { id: 'work', name: 'Work', color: '#1677ff', tagIds: ['work', 'project'] }
const travel: GraphFamily = { id: 'travel', name: 'Travel', color: '#52c41a', tagIds: ['travel'] }
const node = (id: string, tagIds: string[]): PositionedNode => ({ id, tagIds, title: id, icon: null, degree: 0, isArchived: false })

describe('graph families', () => {
  it('uses any tag and the first matching family, leaving unmatched pages without a family', () => {
    expect(familyForNode(node('a', ['project']), [work, travel])).toBe(work)
    const both = node('b', ['work', 'travel'])
    expect(familyForNode(both, [work, travel])).toBe(work)
    expect(familyForNode(both, [travel, work])).toBe(travel)
    expect(familyForNode(node('c', []), [work, travel])).toBeUndefined()
  })

  it('encloses a singleton and multiple positioned nodes, ignoring unpositioned ones', () => {
    expect(familyBounds([node('a', [])])).toBeNull()
    expect(familyBounds([{ ...node('a', []), x: 10, y: 20 }], 5)).toEqual({ x: 5, y: 15, width: 10, height: 10 })
    expect(familyBounds([{ ...node('a', []), x: -10, y: 20 }, { ...node('b', []), x: 30, y: -20 }], 5))
      .toEqual({ x: -15, y: -25, width: 50, height: 50 })
  })

  it('pulls families toward different stable anchors without moving the cached input', () => {
    const nodes = [node('a', ['work']), node('b', ['travel']), node('c', [])].map((item) => ({ ...item, x: 0, y: 0 }))
    const force = familyForce([work, travel])
    force.initialize(nodes)
    force(1)
    expect(nodes[0].x).toBe(0)
    expect(nodes[0].vy).not.toBe(nodes[1].vy)
    expect(nodes[1].vx).not.toBe(nodes[2].vx)
    expect(nodes.every((item) => Number.isFinite(item.vx) && Number.isFinite(item.vy))).toBe(true)
  })
})
