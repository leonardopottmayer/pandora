import type { GraphEdgeDto, PageGraphDto } from '../models'

/** A node as the force simulation wants it: `id` plus whatever we draw with. */
export interface ForceNode {
  id: string
  title: string
  icon: string | null
  isArchived: boolean
  degree: number
}

/** An edge as the force simulation wants it — endpoints named `source`/`target`. */
export interface ForceLink {
  source: string
  target: string
  kind: GraphEdgeDto['kind']
}

export interface ForceData {
  nodes: ForceNode[]
  links: ForceLink[]
}

/**
 * Reshapes the API payload for `react-force-graph`. The copy is not incidental: the simulation
 * mutates the objects it is given (writing `x`/`y`/velocities onto them), and the payload it would
 * otherwise mutate is the react-query cache.
 */
export function toForceData(graph: PageGraphDto | undefined): ForceData {
  if (!graph) return { nodes: [], links: [] }

  return {
    nodes: graph.nodes.map((node) => ({
      id: node.id,
      title: node.title,
      icon: node.icon,
      isArchived: node.isArchived,
      degree: node.degree,
    })),
    links: graph.edges.map((edge) => ({
      source: edge.sourceId,
      target: edge.targetId,
      kind: edge.kind,
    })),
  }
}

/**
 * Who touches whom, ignoring direction — what a hover highlights. Built from the DTO edges rather
 * than from the links handed to the simulation, which replaces the endpoint ids with node objects
 * once it starts running.
 */
export function buildAdjacency(graph: PageGraphDto | undefined): Map<string, Set<string>> {
  const adjacency = new Map<string, Set<string>>()
  if (!graph) return adjacency

  const connect = (from: string, to: string) => {
    const neighbors = adjacency.get(from) ?? new Set<string>()
    neighbors.add(to)
    adjacency.set(from, neighbors)
  }

  for (const edge of graph.edges) {
    connect(edge.sourceId, edge.targetId)
    connect(edge.targetId, edge.sourceId)
  }

  return adjacency
}

/** How a node reads on the canvas: the page's emoji, when it has one, then its title. */
export function nodeLabel(icon: unknown, title: unknown): string {
  return [icon, title].filter(Boolean).join(' ').trim()
}

/**
 * An edge endpoint is the page id we handed in until the simulation starts, and the node object
 * itself afterwards — link styling has to read it either way.
 */
export function endpointId(endpoint: unknown): string {
  if (endpoint && typeof endpoint === 'object' && 'id' in endpoint) return String(endpoint.id)
  return String(endpoint)
}
