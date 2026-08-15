// DTOs mirroring the Notes backend module (Phase 01/02). See
// Pottmayer.Pandora.Modules.Notes.Application.Dtos.

/** Lightweight node for the sidebar tree — no markdown body. */
export interface PageSummaryDto {
  id: string
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

/** A tag as the filters list it; `pageCount` counts the live pages carrying it. */
export interface TagDto extends PageTagDto {
  pageCount: number
}

export interface CreatePageRequest {
  title: string
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
}

/** Metadata returned after an upload; `url` is the authenticated download path. */
export interface AttachmentDto {
  id: string
  pageId: string | null
  fileName: string
  contentType: string
  sizeBytes: number
  url: string
  createdAt: string
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

/** One hit of the full-text search; `excerpt` is a plain slice of the body, no highlighting. */
export interface PageSearchResultDto {
  id: string
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
