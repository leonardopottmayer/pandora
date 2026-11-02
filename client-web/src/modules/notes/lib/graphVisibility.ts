import type { PageGraphDto } from '../models'

/** Apply visibility before grouping; degrees describe only the surviving real links. */
export function graphVisibility(graph: PageGraphDto | undefined, includeArchived: boolean, hideIsolated: boolean): PageGraphDto | undefined {
  if (!graph) return undefined
  const candidates = graph.nodes.filter((node) => includeArchived || !node.isArchived)
  const ids = new Set(candidates.map((node) => node.id))
  const edges = graph.edges.filter((edge) => ids.has(edge.sourceId) && ids.has(edge.targetId))
  const degrees = new Map(candidates.map((node) => [node.id, 0]))
  for (const edge of edges) {
    degrees.set(edge.sourceId, degrees.get(edge.sourceId)! + 1)
    degrees.set(edge.targetId, degrees.get(edge.targetId)! + 1)
  }
  const nodes = candidates.map((node) => ({ ...node, degree: degrees.get(node.id)! }))
    .filter((node) => !hideIsolated || node.degree > 0)
  return { nodes, edges }
}
