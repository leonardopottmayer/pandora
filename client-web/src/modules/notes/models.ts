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

/** A sidebar node with its children nested (built on the frontend from the flat list). */
export interface PageTreeNode extends PageSummaryDto {
  children: PageTreeNode[]
}
