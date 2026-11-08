// DTOs mirroring the Notes backend module (Phase 01/02). See
// Pottmayer.Pandora.Modules.Notes.Application.Dtos.

import type { BlockDocument } from './lib/blocks'

export interface NotesPreferencesDto {
  dailyTemplateId: string | null
}

export interface FolderPropertyPageDto {
  page: PageSummaryDto
  properties: Record<string, string>
}

export interface FolderPropertiesDto {
  columns: string[]
  pages: FolderPropertyPageDto[]
}

export interface MoveTreeItemsRequest {
  spaceId: string
  kind: 'page' | 'folder'
  parentId: string | null
  folderId: string | null
  /** The destination's visible siblings in their final order. */
  itemIds: string[]
}

/** Lightweight node for the sidebar tree — no body. */
export interface PageSummaryDto {
  isTemplate: boolean
  id: string
  spaceId: string
  parentId: string | null
  /** The folder a top-level page sits in; a sub-page has none. */
  folderId: string | null
  title: string
  slug: string
  icon: string | null
  orderIndex: number
  isFavorite: boolean
  isArchived: boolean
  createdAt: string
  updatedAt: string | null
}

/** A folder of a space: it holds folders and pages, never sits inside a page. */
export interface FolderDto {
  id: string
  spaceId: string
  parentId: string | null
  name: string
  icon: string | null
  description: string | null
  orderIndex: number
  createdAt: string
  updatedAt: string | null
}

export interface SaveFolderRequest {
  name: string
  icon?: string | null
  description?: string | null
  /** On create only: the folder it goes in, or none for the top of `spaceId`. */
  parentId?: string | null
  spaceId?: string | null
}

export interface MoveFolderRequest {
  parentId: string | null
  orderIndex: number
}

/** Full view of a page, including its blocks. */
export interface PageDto {
  isTemplate: boolean
  id: string
  spaceId: string
  parentId: string | null
  folderId: string | null
  title: string
  slug: string
  content: BlockDocument
  icon: string | null
  orderIndex: number
  isFavorite: boolean
  isArchived: boolean
  createdAt: string
  updatedAt: string | null
  /** The tags of the page's tags block, rewritten by the backend on every save. */
  tags: PageTagDto[]
  appearance: PageAppearance
  /** Locked, the page opens to read; its blocks are edited only once unlocked. */
  isLocked: boolean
  /** When someone said the page is right, and when that runs out (null: it does not). */
  verifiedAt: string | null
  verifiedUntil: string | null
  /** The secret of the page's public link; null while it is private. */
  publicToken: string | null
}

export const PAGE_WIDTHS = ['normal', 'wide'] as const
export const PAGE_FONTS = ['default', 'serif', 'mono'] as const
export const COVER_GRADIENTS = ['sunset', 'ocean', 'forest', 'dusk', 'sand', 'slate'] as const

/** A cover: a library image or a preset gradient, shown at `position` (0 top, 100 bottom). */
export interface PageCover {
  mediaId?: string | null
  gradient?: (typeof COVER_GRADIENTS)[number] | null
  position: number
}

export interface PageAppearance {
  cover?: PageCover | null
  width: (typeof PAGE_WIDTHS)[number]
  font: (typeof PAGE_FONTS)[number]
  smallText: boolean
}

/** A page in the trash, and how many sub-pages were deleted with it. */
export interface TrashItemDto {
  id: string
  spaceId: string
  title: string
  icon: string | null
  deletedAt: string
  subpageCount: number
}

/** A comment on a block; a thread has no `threadId`, its replies point at it. */
export interface CommentDto {
  id: string
  pageId: string
  blockId: string
  threadId: string | null
  /** The words a thread is about, when it is on part of the text. */
  quote: string | null
  body: string
  resolvedAt: string | null
  createdAt: string
  editedAt: string | null
}

/** When a block was written and last changed, and by what. */
export interface BlockStampDto {
  blockId: string
  createdAt: string
  updatedAt: string
  updatedSource: 'create' | 'edit' | 'assistant' | 'restore' | string
}

/** A page as its public link shows it. */
export interface PublicPageDto {
  title: string
  icon: string | null
  content: BlockDocument
  appearance: PageAppearance
  updatedAt: string | null
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
  /** The block document, as JSON text. */
  content: string
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
  content?: BlockDocument | null
  /** A top-level page's folder; ignored for a sub-page. */
  folderId?: string | null
}

export interface UpdatePageRequest {
  title: string
  icon?: string | null
  content: BlockDocument
  source?: 'edit' | 'assistant'
}

export type TextAction = 'summarize' | 'improve' | 'translate' | 'continue'

export interface TextTransformRequest {
  action: TextAction
  text: string
  language?: string
  context?: string
}

export interface TextTransformDto {
  text: string
}

export interface AiGenerationDto {
  text: string
  generatedAt: string
}

export interface AskNotesRequest {
  question: string
  spaceId?: string | null
}

export interface NoteSourceDto {
  pageId: string
  spaceId: string
  title: string
  icon: string | null
  excerpt: string
}

export interface NoteAnswerDto {
  answer: string | null
  sources: NoteSourceDto[]
}

export interface MovePageRequest {
  parentId: string | null
  orderIndex: number
  /** With no parent, the space the page goes to the top of. The subtree comes along. */
  spaceId?: string | null
  /** With no parent, the folder it goes in (and that folder's space). */
  folderId?: string | null
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

/** An oEmbed answer: `video`/`rich` bring the site's HTML (drawn sandboxed), `photo` an image. */
export interface LinkEmbedDto {
  type: 'video' | 'rich' | 'photo' | 'link'
  html: string | null
  url: string | null
  width: number | null
  height: number | null
}

/** What a web page says about itself, read by the server (the browser cannot, across sites). */
export interface LinkPreviewDto {
  url: string
  title: string | null
  description: string | null
  siteName: string | null
  imageUrl: string | null
  iconUrl: string | null
  embed: LinkEmbedDto | null
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
