import { describe, expect, it } from 'vitest'
import { collapsedGraph } from './collapsedGraph'
import { familyForNode, type GraphFamily } from './graphGroups'
import { toForceData } from './graphData'
import type { PageGraphDto } from '../models'

const families: GraphFamily[] = [
  { id: 'work', name: 'Work', color: '#1677ff', tagIds: ['work'] },
  { id: 'home', name: 'Home', color: '#52c41a', tagIds: ['home'] },
]
const graph: PageGraphDto = {
  nodes: [
    ['a', ['work']], ['b', ['work', 'home']], ['c', ['home']], ['d', ['home']], ['e', []],
  ].map(([id, tagIds]) => ({ id: id as string, title: id as string, slug: id as string, tagIds: tagIds as string[], icon: null, isArchived: false, degree: 1 })),
  edges: [
    { sourceId: 'a', targetId: 'b', kind: 'wikilink' },
    { sourceId: 'a', targetId: 'c', kind: 'wikilink' },
    { sourceId: 'b', targetId: 'c', kind: 'wikilink' },
    { sourceId: 'b', targetId: 'e', kind: 'wikilink' },
    { sourceId: 'c', targetId: 'a', kind: 'wikilink' },
    { sourceId: 'a', targetId: 'c', kind: 'embed' },
    { sourceId: 'e', targetId: 'e', kind: 'wikilink' },
  ],
}

describe('collapsedGraph', () => {
  it('replaces each collapsed family once using priority and leaves unmatched pages visible', () => {
    const data = collapsedGraph(graph, families, ['work', 'home'])
    expect(data.nodes.map((node) => node.id)).toEqual(['e', 'family:work', 'family:home'])
    expect(data.nodes.find((node) => node.familyId === 'work')?.memberCount).toBe(2)
    expect(data.nodes.find((node) => node.familyId === 'home')?.memberCount).toBe(2)
    expect(familyForNode(data.nodes[1], families)?.id).toBe('work')
    expect(collapsedGraph(graph, [...families].reverse(), ['home']).nodes.find((node) => node.familyId === 'home')?.memberCount).toBe(3)
  })

  it('hides internal edges and combines external links without losing direction or kind', () => {
    const { links } = collapsedGraph(graph, families, ['work', 'home'])
    expect(links).toEqual([
      { source: 'e', target: 'e', kind: 'wikilink' },
      { source: 'family:work', target: 'family:home', kind: 'wikilink', count: 2 },
      { source: 'family:work', target: 'e', kind: 'wikilink', count: 1 },
      { source: 'family:home', target: 'family:work', kind: 'wikilink', count: 1 },
      { source: 'family:work', target: 'family:home', kind: 'embed', count: 1 },
    ])
  })

  it('keeps expanded pages and maps links between them and a collapsed family', () => {
    const { nodes, links } = collapsedGraph(graph, families, ['work'])
    expect(nodes.map((node) => node.id)).toEqual(['c', 'd', 'e', 'family:work'])
    expect(links).toContainEqual({ source: 'family:work', target: 'c', kind: 'wikilink', count: 2 })
    expect(links).toContainEqual({ source: 'c', target: 'family:work', kind: 'wikilink', count: 1 })
  })

  it('uses only filtered members and draws no empty family nodes', () => {
    const data = collapsedGraph({ nodes: [graph.nodes[0]], edges: [] }, families, ['work', 'home', 'deleted'])
    expect(data.nodes).toHaveLength(1)
    expect(data.nodes[0].memberCount).toBe(1)
    expect(collapsedGraph(undefined, families, ['work'])).toEqual({ nodes: [], links: [] })
  })

  it('restores the original view when expanded or grouping is off and never mutates cached data', () => {
    const before = structuredClone(graph)
    const data = collapsedGraph(graph, families, ['work'])
    data.nodes[0].tagIds.push('mutation')
    expect(graph).toEqual(before)
    expect(collapsedGraph(graph, families, [])).toEqual(toForceData(graph))
    expect(collapsedGraph(graph, [], ['work'])).toEqual(toForceData(graph))
  })
})
