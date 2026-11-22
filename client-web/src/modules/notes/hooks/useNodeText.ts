import { useTranslation } from 'react-i18next'
import { countBelow, lastEdited, type SpaceNode } from '../lib/spaceTree'
import { timeAgo } from '../lib/timeAgo'
import { usePreferences } from '@/modules/identity/context/preferences-context'

/** How a folder or page reads in lists: its name, what it holds, when it last changed. */
export function useNodeText() {
  const { timeZone } = usePreferences()
  const { t, i18n } = useTranslation()
  const name = (node: SpaceNode) => (node.kind === 'folder' ? node.folder.name : node.page.title || t('notes.untitled'))
  const counts = (node: SpaceNode) => {
    const { folders, pages } = countBelow(node)
    if (!folders && !pages) return node.kind === 'folder' ? t('notes.counts.empty') : ''
    return [
      folders ? t('notes.counts.folders', { count: folders }) : null,
      pages ? t(node.kind === 'page' ? 'notes.subpageCount' : 'notes.counts.pages', { count: pages }) : null,
    ].filter(Boolean).join(' · ')
  }
  const edited = (node: SpaceNode) => timeAgo(lastEdited(node), i18n.language)
  const date = (node: SpaceNode) => new Date(lastEdited(node)).toLocaleDateString(i18n.language, { timeZone })
  return { name, counts, edited, date }
}
