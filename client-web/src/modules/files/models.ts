// Mirrors the Files backend DTOs (docs/modules/files/en/product-plan.md §6) and the agent's bridge (§4.5).

export type SelectionMode = 'include' | 'exclude'

/** A selection mark: the deepest marked ancestor of a folder decides whether it is cataloged. `/` is the root. */
export interface Mark {
  path: string
  mode: SelectionMode
}

export interface FilesPreferences {
  isEnabled: boolean
}

export interface Root {
  id: string
  deviceId: string
  name: string
  localPath: string
  caseSensitive: boolean
  includeHidden: boolean
  /** Daily, in the device's local time (`"03:00:00"`); null scans only on demand. */
  scanTime: string | null
  status: 'active' | 'removed'
  lastCompletedScanAt: string | null
  entryCount: number
  marks: Mark[]
}

export interface RootSettings {
  name: string
  scanTime: string | null
  includeHidden: boolean
  /** Null on a new root takes the device platform's default. */
  caseSensitive: boolean | null
}

export interface NewRoot extends RootSettings {
  deviceId: string
  localPath: string
}

export type FilterAction = 'include' | 'exclude'
export type FilterTarget = 'file' | 'folder'
export type FilterMatcher = 'extension' | 'glob' | 'starts-with' | 'ends-with' | 'contains' | 'regex'

/** The scope is the most specific of scopePath (needs rootId) → rootId → deviceId → the whole account. */
export interface FilterInput {
  deviceId: string | null
  rootId: string | null
  scopePath: string | null
  name: string
  action: FilterAction
  appliesTo: FilterTarget
  matcher: FilterMatcher
  pattern: string
  caseSensitive: boolean | null
  isEnabled: boolean
}

export interface Filter extends FilterInput {
  id: string
  isBuiltin: boolean
}

export interface FilterPreview {
  count: number
  sample: { id: string; rootId: string; relativePath: string }[]
}

export type EntryStatus = 'present' | 'missing' | 'excluded'
export type FileCategory = 'video' | 'audio' | 'image' | 'document' | 'ebook' | 'archive' | 'code' | 'other'

export interface Entry {
  id: string
  rootId: string
  kind: 'file' | 'directory'
  relativePath: string
  parentPath: string
  name: string
  extension: string | null
  category: FileCategory | null
  sizeBytes: number
  modifiedAt: string | null
  status: EntryStatus
  missingSince: string | null
  keptAt: string | null
  firstSeenAt: string
}

/** A page without a total. */
export interface Page<T> {
  items: T[]
  hasMore: boolean
}

export interface SearchCriteria {
  q?: string
  rootId?: string
  category?: FileCategory
  status?: EntryStatus | 'all'
}

export type ScanStatus = 'running' | 'completed' | 'aborted' | 'held'

export interface Scan {
  id: string
  rootId: string
  status: ScanStatus
  startedAt: string
  lastBatchAt: string | null
  finishedAt: string | null
  seen: number
  created: number
  changed: number
  moved: number
  missing: number
  excluded: number
  error: string | null
}

/** A node of the review tree: a root at the top (path `/`), then folders and entries counting what waits below. */
export interface ReviewNode {
  rootId: string
  name: string
  path: string
  count: number
  hasChildren: boolean
  entryId: string | null
  status: 'missing' | 'excluded' | null
  kind: 'file' | 'directory' | null
}

export interface ReviewDecision {
  entryIds: string[]
  folders: { rootId: string; path: string }[]
}

// ── The agent, through the desktop bridge ──

/** A folder from `files.listFolders`; `catalogPath` is set when listing inside a root. */
export interface AgentFolder {
  name: string
  path: string
  catalogPath: string | null
}

export interface ScanProgress {
  rootId: string
  scanId: string | null
  state: 'running' | 'completed' | 'held' | 'aborted' | 'failed'
  walked: number
  fingerprinted: number
  error: string | null
}

/** `files.status`: what this PC's agent knows. */
export interface AgentStatus {
  /** False when the server rejected the device key (not paired, or revoked). */
  paired: boolean
  enabled: boolean | null
  roots: { id: string }[]
  running: ScanProgress | null
  lastError: string | null
}
