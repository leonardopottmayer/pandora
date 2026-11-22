import type { FolderDto, MoveFolderRequest, MovePageRequest, PageSummaryDto } from '../models'

// A space as the sidebar, the overview and a folder show it: folders hold folders and pages, a page
// holds sub-pages, and a folder never sits inside a page.

export type SpaceNode =
  | { kind: 'folder'; id: string; folder: FolderDto; children: SpaceNode[] }
  | { kind: 'page'; id: string; page: PageSummaryDto; children: SpaceNode[] }

/** Folders first, then pages, each by its position. */
export function buildSpaceTree(folders: FolderDto[], pages: PageSummaryDto[]): SpaceNode[] {
  const folderNodes = new Map<string, SpaceNode>()
  const pageNodes = new Map<string, SpaceNode>()
  for (const folder of [...folders].sort((a, b) => a.orderIndex - b.orderIndex || a.name.localeCompare(b.name))) {
    folderNodes.set(folder.id, { kind: 'folder', id: folder.id, folder, children: [] })
  }
  for (const page of [...pages].sort((a, b) => a.orderIndex - b.orderIndex)) {
    pageNodes.set(page.id, { kind: 'page', id: page.id, page, children: [] })
  }

  const roots: SpaceNode[] = []
  for (const node of folderNodes.values()) {
    const parent = node.kind === 'folder' && node.folder.parentId ? folderNodes.get(node.folder.parentId) : undefined
    ;(parent ? parent.children : roots).push(node)
  }
  for (const node of pageNodes.values()) {
    if (node.kind !== 'page') continue
    // A page whose parent or folder is missing (archived, filtered out) surfaces at the top, still reachable.
    const parent = (node.page.parentId && pageNodes.get(node.page.parentId)) || (node.page.folderId && folderNodes.get(node.page.folderId)) || null
    ;(parent ? parent.children : roots).push(node)
  }
  return roots
}

export function walkSpace(nodes: SpaceNode[], visit: (node: SpaceNode, path: SpaceNode[]) => void, path: SpaceNode[] = []) {
  for (const node of nodes) {
    visit(node, path)
    walkSpace(node.children, visit, [...path, node])
  }
}

/** Templates have their own section; their descendants stay reachable beneath them. */
export function splitTemplateTree(nodes: SpaceNode[]): { pages: SpaceNode[]; templates: SpaceNode[] } {
  const templates: SpaceNode[] = []
  function ordinary(items: SpaceNode[]): SpaceNode[] {
    return items.flatMap((node) => {
      if (node.kind === 'page' && node.page.isTemplate) {
        templates.push(node)
        return []
      }
      return [{ ...node, children: ordinary(node.children) }]
    })
  }
  return { pages: ordinary(nodes), templates }
}

export function findNode(nodes: SpaceNode[], id: string): SpaceNode | null {
  let found: SpaceNode | null = null
  walkSpace(nodes, (node) => {
    if (!found && node.id === id) found = node
  })
  return found
}

/** The nodes above `id`, from the top. */
export function pathTo(nodes: SpaceNode[], id: string): SpaceNode[] {
  let found: SpaceNode[] = []
  walkSpace(nodes, (node, path) => {
    if (node.id === id) found = path
  })
  return found
}

/** How many folders and pages sit anywhere below. */
export function countBelow(node: SpaceNode): { folders: number; pages: number } {
  let folders = 0
  let pages = 0
  walkSpace(node.children, (child) => {
    if (child.kind === 'folder') folders++
    else pages++
  })
  return { folders, pages }
}

const editedAt = (page: PageSummaryDto) => page.updatedAt ?? page.createdAt

/** When the node or anything below it last changed; a folder with no page, when it was made. */
export function lastEdited(node: SpaceNode): string {
  let latest = node.kind === 'page' ? editedAt(node.page) : node.folder.updatedAt ?? node.folder.createdAt
  walkSpace(node.children, (child) => {
    if (child.kind === 'page' && editedAt(child.page) > latest) latest = editedAt(child.page)
  })
  return latest
}

// ---------- Drag and drop ----------

export type SpaceMove =
  | { kind: 'page'; id: string; body: MovePageRequest }
  | { kind: 'folder'; id: string; body: MoveFolderRequest }

export type DropProblem = 'cycle' | 'folderInPage'

/** Where a node's children go: a folder (or the top, null) holds folders and pages; a page holds pages. */
interface Container {
  folderId: string | null
  pageId: string | null
}

const containerOf = (nodes: SpaceNode[], node: SpaceNode): Container => {
  const parent = pathTo(nodes, node.id).at(-1)
  if (!parent) return { folderId: null, pageId: null }
  return parent.kind === 'folder' ? { folderId: parent.id, pageId: null } : { folderId: null, pageId: parent.id }
}

const childrenIn = (nodes: SpaceNode[], container: Container): SpaceNode[] => {
  const id = container.pageId ?? container.folderId
  return id ? (findNode(nodes, id)?.children ?? []) : nodes
}

/** The list renumbered with `dragged` placed at `index`, as moves. */
function renumber(list: SpaceNode[], dragged: SpaceNode, index: number, container: Container): SpaceMove[] {
  const others = list.filter((node) => node.id !== dragged.id)
  const at = Math.max(0, Math.min(index, others.length))
  const ordered = [...others.slice(0, at), dragged, ...others.slice(at)]
  return ordered.map((node, orderIndex): SpaceMove =>
    node.kind === 'folder'
      ? { kind: 'folder', id: node.id, body: { parentId: container.folderId, orderIndex } }
      : { kind: 'page', id: node.id, body: { parentId: container.pageId, folderId: container.pageId ? null : container.folderId, orderIndex } },
  )
}

/**
 * The moves an antd Tree drop asks for. `relativeIndex` is antd's: -1 above the target, 1 below,
 * 0 onto it. Onto a folder puts the node in it, onto a page makes it a sub-page; a gap puts it
 * beside the target. A folder never goes inside a page, nor inside itself.
 */
export function computeSpaceDrop(
  nodes: SpaceNode[],
  dragId: string,
  dropId: string,
  relativeIndex: number,
  dropToGap: boolean,
): SpaceMove[] | DropProblem {
  const dragged = findNode(nodes, dragId)
  const target = findNode(nodes, dropId)
  if (!dragged || !target || dragId === dropId) return []
  if (findNode(dragged.children, dropId)) return 'cycle'

  const container: Container = dropToGap
    ? containerOf(nodes, target)
    : target.kind === 'folder' ? { folderId: target.id, pageId: null } : { folderId: null, pageId: target.id }
  if (dragged.kind === 'folder' && container.pageId) return 'folderInPage'

  const siblings = childrenIn(nodes, container).filter((node) => node.kind === dragged.kind)
  if (!dropToGap) return renumber(siblings, dragged, siblings.length, container)

  // Beside a node of its own kind: before or after it. Beside one of the other kind: at the edge
  // where the two kinds meet (folders come first).
  if (target.kind === dragged.kind) {
    const index = siblings.filter((node) => node.id !== dragId).findIndex((node) => node.id === dropId)
    return renumber(siblings, dragged, relativeIndex > 0 ? index + 1 : index, container)
  }
  return renumber(siblings, dragged, dragged.kind === 'folder' ? siblings.length : 0, container)
}
