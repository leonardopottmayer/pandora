import type { PageSummaryDto, PageTreeNode } from '../models'

/**
 * Nests a flat, ordered page list (from `GET /notes/pages`) into a tree by `parentId`.
 * Siblings keep the backend order (already sorted by `orderIndex`); orphaned pages whose
 * parent is missing (e.g. an archived parent) surface at the root so they stay reachable.
 */
export function buildTree(pages: PageSummaryDto[]): PageTreeNode[] {
  const byId = new Map<string, PageTreeNode>()
  for (const p of pages) byId.set(p.id, { ...p, children: [] })

  const roots: PageTreeNode[] = []
  for (const node of byId.values()) {
    const parent = node.parentId ? byId.get(node.parentId) : undefined
    if (parent) parent.children.push(node)
    else roots.push(node)
  }
  return roots
}
