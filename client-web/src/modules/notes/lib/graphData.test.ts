import { describe, it, expect } from 'vitest'
import { buildAdjacency, endpointId, nodeLabel, toForceData } from './graphData'
import type { GraphNodeDto, PageGraphDto } from '../models'

function node(id: string, degree = 0): GraphNodeDto {
  return { id, title: id.toUpperCase(), slug: id, icon: null, isArchived: false, degree }
}

const graph: PageGraphDto = {
  nodes: [node('a', 1), node('b', 2), node('c', 1)],
  edges: [
    { sourceId: 'a', targetId: 'b', kind: 'wikilink' },
    { sourceId: 'b', targetId: 'c', kind: 'embed' },
  ],
}

describe('toForceData', () => {
  it('renames the edge endpoints to what the simulation expects', () => {
    const { links } = toForceData(graph)

    expect(links).toEqual([
      { source: 'a', target: 'b', kind: 'wikilink' },
      { source: 'b', target: 'c', kind: 'embed' },
    ])
  })

  it('copies the nodes, so the simulation cannot write into the query cache', () => {
    const { nodes } = toForceData(graph)

    expect(nodes[0]).not.toBe(graph.nodes[0])
    expect(nodes[0].title).toBe('A')
  })

  it('handles a graph that has not loaded yet', () => {
    expect(toForceData(undefined)).toEqual({ nodes: [], links: [] })
  })
})

describe('buildAdjacency', () => {
  it('links both ways, so a hover highlights neighbors in either direction', () => {
    const adjacency = buildAdjacency(graph)

    expect(adjacency.get('a')).toEqual(new Set(['b']))
    expect(adjacency.get('b')).toEqual(new Set(['a', 'c']))
    expect(adjacency.get('c')).toEqual(new Set(['b']))
  })

  it('leaves a page with no edges out of the map', () => {
    const adjacency = buildAdjacency({ nodes: [node('lonely')], edges: [] })
    expect(adjacency.get('lonely')).toBeUndefined()
  })
})

describe('nodeLabel', () => {
  it('prefixes the emoji when the page has one', () => {
    expect(nodeLabel('📓', 'Journal')).toBe('📓 Journal')
    expect(nodeLabel(null, 'Journal')).toBe('Journal')
  })
})

describe('endpointId', () => {
  it('reads the id both before and after the simulation swaps it for the node', () => {
    expect(endpointId('a')).toBe('a')
    expect(endpointId({ id: 'a', x: 1, y: 2 })).toBe('a')
  })
})
