import type { MediaFolderDto } from '../models'

const UNSORTED = '__unsorted__'

export interface MediaFolderNode extends MediaFolderDto {
  children: MediaFolderNode[]
}

/** Nests the flat folder list by parent, siblings by name. A folder whose parent is missing becomes a root. */
export function buildFolderTree(folders: MediaFolderDto[]): MediaFolderNode[] {
  const nodes = new Map(folders.map((f) => [f.id, { ...f, children: [] as MediaFolderNode[] }]))
  const roots: MediaFolderNode[] = []
  for (const node of nodes.values()) {
    const parent = node.parentId ? nodes.get(node.parentId) : undefined
    ;(parent ? parent.children : roots).push(node)
  }
  const sort = (list: MediaFolderNode[]) => {
    list.sort((a, b) => a.name.localeCompare(b.name))
    list.forEach((n) => sort(n.children))
  }
  sort(roots)
  return roots
}

/** The folder and every folder under it — where a folder cannot be moved to. */
export function folderAndDescendants(folders: MediaFolderDto[], id: string): Set<string> {
  const found = new Set([id])
  let grew = true
  while (grew) {
    grew = false
    for (const f of folders) {
      if (f.parentId && found.has(f.parentId) && !found.has(f.id)) {
        found.add(f.id)
        grew = true
      }
    }
  }
  return found
}

/** The folders from the top down to `id`, for a "Trips / Japan / Kyoto" path. */
export function folderPath(folders: MediaFolderDto[], id: string | null): MediaFolderDto[] {
  const byId = new Map(folders.map((f) => [f.id, f]))
  const path: MediaFolderDto[] = []
  for (let current = id ? byId.get(id) : undefined; current && path.length < 50; ) {
    path.unshift(current)
    current = current.parentId ? byId.get(current.parentId) : undefined
  }
  return path
}

/** The folder tree as TreeSelect options, "Unsorted" first; `disabled` greys out folders that cannot be picked. */
export function folderOptions(folders: MediaFolderDto[], unsortedLabel: string, disabled: Set<string> = new Set()) {
  type Option = { value: string; title: string; disabled?: boolean; children?: Option[] }
  const toOptions = (nodes: MediaFolderNode[]): Option[] =>
    nodes.map((n) => ({ value: n.id, title: n.name, disabled: disabled.has(n.id), children: toOptions(n.children) }))
  return [{ value: UNSORTED, title: unsortedLabel }, ...toOptions(buildFolderTree(folders))]
}

/** A TreeSelect value back to a folder id, "Unsorted" being null. */
export const fromFolderOption = (value: string) => (value === UNSORTED ? null : value)
export const toFolderOption = (folderId: string | null) => folderId ?? UNSORTED
