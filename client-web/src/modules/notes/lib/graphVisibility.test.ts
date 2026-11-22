import { describe, expect, it } from 'vitest'
import { graphVisibility } from './graphVisibility'
import type { GraphNodeDto, PageGraphDto } from '../models'

function node(id: string, isArchived = false): GraphNodeDto {
  return { id, title: id, slug: id, icon: null, tagIds: [], degree: 99, isArchived }
}
const graph: PageGraphDto = {
  nodes: [node('a'), node('b'), node('old', true), node('leaf'), node('solo')],
  edges: [
    { sourceId: 'a', targetId: 'b', kind: 'wikilink' },
    { sourceId: 'a', targetId: 'b', kind: 'embed' },
    { sourceId: 'b', targetId: 'old', kind: 'wikilink' },
    { sourceId: 'old', targetId: 'leaf', kind: 'wikilink' },
  ],
}

describe('graphVisibility', () => {
  it('hides archived pages and their links and recomputes the remaining degrees', () => {
    const visible = graphVisibility(graph, false, false)!
    expect(visible.nodes.map((value) => value.id)).toEqual(['a', 'b', 'leaf', 'solo'])
    expect(visible.edges).toEqual(graph.edges.slice(0, 2))
    expect(visible.nodes.map((value) => value.degree)).toEqual([2, 2, 0, 0])
  })

  it('removes isolated nodes after archive filtering, including those whose only neighbor was archived', () => {
    expect(graphVisibility(graph, false, true)!.nodes.map((value) => value.id)).toEqual(['a', 'b'])
    expect(graphVisibility(graph, true, true)!.nodes.map((value) => value.id)).toEqual(['a', 'b', 'old', 'leaf'])
    expect(graphVisibility(graph, true, false)!.nodes).toHaveLength(5)
  })

  it('keeps self-links as connections, with the same degree rule as the backend', () => {
    const visible = graphVisibility({ nodes: [node('self')], edges: [{ sourceId: 'self', targetId: 'self', kind: 'wikilink' }] }, false, true)!
    expect(visible.nodes[0].degree).toBe(2)
    expect(visible.edges).toHaveLength(1)
  })

  it('does not mutate cached degrees or remove links from the original graph', () => {
    const before = structuredClone(graph)
    const visible = graphVisibility(graph, false, true)!
    visible.nodes[0].degree = 123
    expect(graph).toEqual(before)
    expect(graphVisibility(graph, true, false)!.edges).toEqual(graph.edges)
  })

  it('supports loading and an empty result after visibility filtering', () => {
    expect(graphVisibility(undefined, false, true)).toBeUndefined()
    expect(graphVisibility({ nodes: [node('old', true)], edges: [] }, false, false)).toEqual({ nodes: [], edges: [] })
    expect(graphVisibility({ nodes: [node('solo')], edges: [] }, true, true)).toEqual({ nodes: [], edges: [] })
  })
})
