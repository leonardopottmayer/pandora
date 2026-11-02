import { createContext, useContext } from 'react'
import type { PageIndex } from '../../lib/wikilinks'
import type { TagIndex } from '../../lib/tags'

/** What the node views need from the page around the editor: how links resolve, and where they go. */
export interface VisualEditorContextValue {
  pageIndex: PageIndex
  tagIndex: TagIndex
  onOpenPage: (pageId: string) => void
  onCreatePage: (title: string) => void
  onSelectTag: (slug: string) => void
}

export const VisualEditorContext = createContext<VisualEditorContextValue | null>(null)

export function useVisualEditorContext(): VisualEditorContextValue {
  const value = useContext(VisualEditorContext)
  if (!value) throw new Error('A visual editor node view rendered outside its editor.')
  return value
}
