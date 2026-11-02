// DTOs mirroring the Notes backend module (Phase 01/02). See
// Pottmayer.Pandora.Modules.Notes.Application.Dtos.

/** Lightweight node for the sidebar tree — no markdown body. */
export interface PageSummaryDto {
  id: string
  spaceId: string
  parentId: string | null
  title: string
  slug: string
  icon: string | null
  orderIndex: number
  isFavorite: boolean
  isArchived: boolean
}

/** Full view of a page, including its markdown body. */
export interface PageDto {
  id: string
  spaceId: string
  parentId: string | null
  title: string
  slug: string
  contentMarkdown: string
  icon: string | null
  orderIndex: number
  isFavorite: boolean
  isArchived: boolean
  createdAt: string
  updatedAt: string | null
  /** The tags the body mentions, rewritten by the backend on every save. */
  tags: PageTagDto[]
}

/** A tag as the page carries it — no usage count, this is one page's list. */
export interface PageTagDto {
  id: string
  slug: string
  name: string
  color: string | null
}

export interface PageSnapshot {
  title: string
  contentMarkdown: string
  icon: string | null
  spaceId: string
  parentId: string | null
  orderIndex: number
  isFavorite: boolean
  archivedAt: string | null
  deletedAt: string | null
}

export interface PageVersionSummaryDto {
  number: number
  createdAt: string
  source: string
  restoredFrom: number | null
}

export interface PageVersionDto extends PageVersionSummaryDto {
  snapshot: PageSnapshot
}

/** A tag as the filters list it; `pageCount` counts the live pages carrying it. */
export interface TagDto extends PageTagDto {
  pageCount: number
}

export interface CreatePageRequest {
  title: string
  /** Ignored for a child, which lives in its parent's space; without it, the default space. */
  spaceId?: string | null
  parentId?: string | null
  icon?: string | null
  contentMarkdown?: string | null
}

export interface UpdatePageRequest {
  title: string
  icon?: string | null
  contentMarkdown: string
}

export interface MovePageRequest {
  parentId: string | null
  orderIndex: number
  /** With no parent, the space the page goes to the top of. The subtree comes along. */
  spaceId?: string | null
}

/** A group of pages, like a Confluence space. The default one takes pages nobody placed. */
export interface SpaceDto {
  id: string
  name: string
  icon: string | null
  color: string | null
  description: string | null
  isDefault: boolean
  isArchived: boolean
  pageCount: number
  lastEditedAt: string | null
  createdAt: string
}

export interface SaveSpaceRequest {
  name: string
  icon: string | null
  color: string | null
  description: string | null
}

/** What a library item is: how it shows. `video` exists in the schema but is not accepted yet. */
export type MediaKind = 'image' | 'file' | 'video'

/**
 * An item of the media library. `url` is the authenticated path a note embeds; `thumbnailUrl` is null
 * for a file, or for an image already small enough to show as it is.
 */
export interface MediaItemDto {
  id: string
  folderId: string | null
  kind: MediaKind
  name: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  width: number | null
  height: number | null
  url: string
  thumbnailUrl: string | null
  /** How many pages show it. */
  usageCount: number
  /** Out of the library; notes that show it still do. */
  isDeleted: boolean
  createdAt: string
}

/** A page that shows a library item. */
export interface MediaUsageDto {
  pageId: string
  spaceId: string
  title: string
  icon: string | null
  isArchived: boolean
}

/** A virtual folder, with how many items it holds directly. */
export interface MediaFolderDto {
  id: string
  parentId: string | null
  name: string
  itemCount: number
}

export interface MediaFoldersDto {
  folders: MediaFolderDto[]
  unsortedCount: number
  unusedCount: number
  totalCount: number
  /** The space the items take (thumbnails not counted), and the part no page shows. */
  totalBytes: number
  unusedBytes: number
}

/** A page that mentions the one being read ("linked mention"), and how it mentions it. */
export interface BacklinkDto {
  pageId: string
  title: string
  slug: string
  icon: string | null
  isArchived: boolean
  kind: 'wikilink' | 'embed'
}

/** One ranked search hit; `excerpt` is plain text, highlighted safely by the palette. */
export interface PageSearchResultDto {
  id: string
  spaceId: string
  title: string
  slug: string
  icon: string | null
  isArchived: boolean
  excerpt: string
}

/** One page as a node of the wiki graph; `degree` counts the edges touching it in that same graph. */
export interface GraphNodeDto {
  id: string
  title: string
  slug: string
  icon: string | null
  isArchived: boolean
  degree: number
  tagIds: string[]
}

/** One link between two pages. A page that both links and embeds another yields two edges. */
export interface GraphEdgeDto {
  sourceId: string
  targetId: string
  kind: 'wikilink' | 'embed'
}

/** Nodes plus edges for the graph view; both endpoints of every edge are always in `nodes`. */
export interface PageGraphDto {
  nodes: GraphNodeDto[]
  edges: GraphEdgeDto[]
}

/** A sidebar node with its children nested (built on the frontend from the flat list). */
export interface PageTreeNode extends PageSummaryDto {
  children: PageTreeNode[]
}
