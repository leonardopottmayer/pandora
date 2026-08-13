import type { MovePageRequest, PageTreeNode } from '../models'

export interface PageMove {
  id: string
  body: MovePageRequest
}

function findNode(nodes: PageTreeNode[], id: string): PageTreeNode | null {
  for (const node of nodes) {
    if (node.id === id) return node
    const found = findNode(node.children, id)
    if (found) return found
  }
  return null
}

/** The ordered children of `parentId` (root list when null). */
function siblingsOf(tree: PageTreeNode[], parentId: string | null): PageTreeNode[] {
  if (parentId === null) return tree
  return findNode(tree, parentId)?.children ?? []
}

/** True when `nodeId` is `ancestorId` itself or lives somewhere beneath it. */
export function isSelfOrDescendant(
  tree: PageTreeNode[],
  ancestorId: string,
  nodeId: string,
): boolean {
  if (ancestorId === nodeId) return true
  const ancestor = findNode(tree, ancestorId)
  return ancestor ? findNode(ancestor.children, nodeId) !== null : false
}

/**
 * Turns an antd Tree drop into the set of moves needed to realise it. `relativeIndex` is the
 * antd convention: -1 = above the drop node, 1 = below it, 0 = onto it. Dropping onto a node
 * makes the dragged page its last child; dropping into a gap places it among that parent's
 * siblings. The destination list is renumbered 0..n so the new order survives the id tie-break.
 */
export function computeReorder(
  tree: PageTreeNode[],
  dragKey: string,
  dropKey: string,
  relativeIndex: number,
  dropToGap: boolean,
): PageMove[] {
  const dropNode = findNode(tree, dropKey)
  if (!dropNode) return []

  // Dropped directly on a node → becomes its last child; dropped in a gap → joins that
  // node's sibling list. The dragged page is pulled out of the destination first, so the
  // drop index is measured against the list it will actually be spliced into (moving a
  // page down within one parent would otherwise overshoot by its own slot).
  const targetParentId = dropToGap ? dropNode.parentId : dropKey
  const destination = siblingsOf(tree, targetParentId).filter((s) => s.id !== dragKey)

  let insertIndex: number
  if (!dropToGap) {
    insertIndex = destination.length
  } else {
    const dropIndex = destination.findIndex((s) => s.id === dropKey)
    insertIndex = relativeIndex > 0 ? dropIndex + 1 : dropIndex
  }

  const clampedIndex = Math.max(0, Math.min(insertIndex, destination.length))
  const orderedIds = [
    ...destination.slice(0, clampedIndex).map((s) => s.id),
    dragKey,
    ...destination.slice(clampedIndex).map((s) => s.id),
  ]

  return orderedIds.map((id, orderIndex) => ({
    id,
    body: { parentId: targetParentId, orderIndex },
  }))
}
