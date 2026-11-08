import { createContext, useContext } from 'react'
import type { FolderDto, MediaItemDto, PageSummaryDto } from '../../models'

/** What the node views need from the page around the editor: the pages a mention names, and where a click goes. */
export interface VisualEditorContextValue {
  /** The page being written (or read). */
  pageId: string
  /** The page's editing state, including locks and the phone's read mode. */
  editable: boolean
  pagesById: ReadonlyMap<string, PageSummaryDto>
  foldersById: ReadonlyMap<string, FolderDto>
  onOpenPage: (pageId: string) => void
  onOpenFolder?: (folderId: string) => void
  /** A page read through its public link: a page it names is private, not gone. */
  publicView?: boolean
  /** Opens the media library and answers the item picked, or null when it was closed. */
  pickMedia: (kind: 'image' | 'file') => Promise<MediaItemDto | null>
}

export const VisualEditorContext = createContext<VisualEditorContextValue | null>(null)

export function useVisualEditorContext(): VisualEditorContextValue {
  const value = useContext(VisualEditorContext)
  if (!value) throw new Error('A visual editor node view rendered outside its editor.')
  return value
}
