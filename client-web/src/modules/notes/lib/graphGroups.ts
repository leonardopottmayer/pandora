import type { ForceNode } from './graphData'

export interface GraphFamily {
  id: string
  name: string
  tagIds: string[]
  color: string
}

export interface GraphPreferences {
  showGroups: boolean
  families: GraphFamily[]
}

export interface PositionedNode extends ForceNode {
  x?: number
  y?: number
  vx?: number
  vy?: number
}

/** Priority is the configured order; any selected tag qualifies, but a page is drawn only once. */
export function familyForNode(node: ForceNode, families: GraphFamily[]): GraphFamily | undefined {
  if (node.familyId) return families.find((family) => family.id === node.familyId)
  return families.find((family) => family.tagIds.some((id) => node.tagIds.includes(id)))
}

export function familyBounds(nodes: PositionedNode[], padding = 24) {
  const placed = nodes.filter((node) => Number.isFinite(node.x) && Number.isFinite(node.y))
  if (placed.length === 0) return null
  const xs = placed.map((node) => node.x!)
  const ys = placed.map((node) => node.y!)
  const x = Math.min(...xs) - padding
  const y = Math.min(...ys) - padding
  return { x, y, width: Math.max(...xs) + padding - x, height: Math.max(...ys) + padding - y }
}

/** Deterministic anchors gather families while ordinary links and repulsion still shape each one. */
export function familyForce(families: GraphFamily[]) {
  let nodes: PositionedNode[] = []
  let anchors = new Map<string, { x: number; y: number }>()
  const ungrouped = ''
  function force(alpha: number) {
    for (const node of nodes) {
      const anchor = anchors.get(familyForNode(node, families)?.id ?? ungrouped)
      if (!anchor) continue
      node.vx = (node.vx ?? 0) + (anchor.x - (node.x ?? 0)) * 0.18 * alpha
      node.vy = (node.vy ?? 0) + (anchor.y - (node.y ?? 0)) * 0.18 * alpha
    }
  }
  force.initialize = (value: PositionedNode[]) => {
    nodes = value
    const counts = new Map<string, number>()
    for (const node of nodes) {
      const id = familyForNode(node, families)?.id ?? ungrouped
      counts.set(id, (counts.get(id) ?? 0) + 1)
    }
    const ids = [...families.map((family) => family.id), ungrouped].filter((id) => counts.has(id))
    const radius = ids.length < 2 ? 0 : Math.max(180, Math.sqrt(Math.max(...counts.values())) * 70, ids.length * 65)
    anchors = new Map(ids.map((id, index) => {
      const angle = (index / ids.length) * Math.PI * 2 - Math.PI / 2
      return [id, { x: Math.cos(angle) * radius, y: Math.sin(angle) * radius }]
    }))
  }
  return force
}
