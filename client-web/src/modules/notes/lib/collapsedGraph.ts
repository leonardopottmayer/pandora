import { toForceData, type ForceData, type ForceLink, type ForceNode } from './graphData'
import { familyForNode, type GraphFamily } from './graphGroups'
import type { PageGraphDto } from '../models'

/** A display projection: real graph/query data remains intact for search and details. */
export function collapsedGraph(graph: PageGraphDto | undefined, families: GraphFamily[], collapsedIds: string[]): ForceData {
  const data = toForceData(graph)
  if (collapsedIds.length === 0) return data
  const summaries = new Map<string, ForceNode>()
  const replacements = new Map<string, string>()
  const nodes: ForceNode[] = []
  for (const node of data.nodes) {
    const family = familyForNode(node, families)
    if (!family || !collapsedIds.includes(family.id)) {
      nodes.push(node)
      continue
    }
    let summary = summaries.get(family.id)
    if (!summary) {
      summary = {
        id: `family:${family.id}`, title: family.name, icon: null, isArchived: false,
        degree: 0, tagIds: [], familyId: family.id, memberCount: 0,
      }
      summaries.set(family.id, summary)
    }
    summary.memberCount = (summary.memberCount ?? 0) + 1
    replacements.set(node.id, summary.id)
  }
  const links: ForceLink[] = []
  const aggregated = new Map<string, ForceLink>()
  for (const link of data.links) {
    const source = replacements.get(link.source) ?? link.source
    const target = replacements.get(link.target) ?? link.target
    if (source === link.source && target === link.target) {
      links.push(link)
      continue
    }
    if (source === target) continue
    // Preserve direction and kind while combining parallel links between displayed endpoints.
    const key = JSON.stringify([source, target, link.kind])
    const existing = aggregated.get(key)
    if (existing) existing.count = (existing.count ?? 0) + 1
    else aggregated.set(key, { source, target, kind: link.kind, count: 1 })
  }
  return { nodes: [...nodes, ...summaries.values()], links: [...links, ...aggregated.values()] }
}
