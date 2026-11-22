import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Empty, Segmented, Tag, Typography, theme } from 'antd'
import { AppstoreOutlined, ApartmentOutlined, FolderAddOutlined, PlusOutlined } from '@ant-design/icons'
import { useFolders } from '../hooks/useFolders'
import { usePageTree } from '../hooks/usePages'
import { buildSpaceTree, walkSpace, type SpaceNode } from '../lib/spaceTree'
import { timeAgo } from '../lib/timeAgo'
import type { PageSummaryDto, SpaceDto } from '../models'
import { ItemCards, ItemTree, NodeIcon } from './SpaceItems'
import { SpaceIcon } from './SpaceIcon'

interface SpaceOverviewProps {
  space: SpaceDto
  onOpenPage: (id: string) => void
  onOpenFolder: (id: string) => void
  onCreatePage: () => void
  onCreateFolder: () => void
}

const VIEW_KEY = 'pandora.notes_overview_view'

function readView(): 'cards' | 'tree' {
  try {
    return localStorage.getItem(VIEW_KEY) === 'tree' ? 'tree' : 'cards'
  } catch {
    return 'cards'
  }
}

/** The page every space has: what it holds, what changed lately, and its whole structure. */
export function SpaceOverview({ space, onOpenPage, onOpenFolder, onCreatePage, onCreateFolder }: SpaceOverviewProps) {
  const { t, i18n } = useTranslation()
  const { token } = theme.useToken()
  const { data: pages = [], isLoading: pagesLoading } = usePageTree(false, [], space.id)
  const { data: folders = [], isLoading: foldersLoading } = useFolders(space.id)
  const [view, setView] = useState(readView)
  const tree = useMemo(() => buildSpaceTree(folders, pages), [folders, pages])

  const recent = useMemo(() => {
    const all: { page: PageSummaryDto; path: SpaceNode[] }[] = []
    walkSpace(tree, (node, path) => {
      if (node.kind === 'page') all.push({ page: node.page, path })
    })
    const edited = (page: PageSummaryDto) => page.updatedAt ?? page.createdAt
    return all.sort((a, b) => edited(b.page).localeCompare(edited(a.page))).slice(0, 6)
  }, [tree])

  const open = (node: SpaceNode) => (node.kind === 'folder' ? onOpenFolder(node.id) : onOpenPage(node.id))
  const changeView = (value: 'cards' | 'tree') => {
    setView(value)
    try {
      localStorage.setItem(VIEW_KEY, value)
    } catch {
      // Private mode: the choice lasts until the reload.
    }
  }

  const fact = (label: string, value: string) => (
    <div className="min-w-32 rounded-xl px-4 py-2.5" style={{ border: `1px solid ${token.colorBorderSecondary}` }}>
      <Typography.Text type="secondary" className="block text-xs">{label}</Typography.Text>
      <Typography.Text className="text-xl font-semibold tabular-nums">{value}</Typography.Text>
    </div>
  )

  const loading = pagesLoading || foldersLoading
  const sectionTitle = (text: string) => (
    <Typography.Text type="secondary" className="text-xs font-semibold uppercase tracking-wider">{text}</Typography.Text>
  )

  return (
    <div className="h-full overflow-auto">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-5 py-8 md:px-8">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex min-w-0 items-start gap-4">
            <SpaceIcon space={space} size={48} />
            <div className="flex min-w-0 flex-col gap-1">
              <div className="flex items-center gap-2">
                <Typography.Title level={3} className="!mb-0 truncate">{space.name}</Typography.Title>
                {space.isDefault && <Tag>{t('notes.spaces.default')}</Tag>}
              </div>
              {space.description && <Typography.Text type="secondary">{space.description}</Typography.Text>}
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button icon={<FolderAddOutlined />} onClick={onCreateFolder}>{t('notes.folders.new')}</Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={onCreatePage}>{t('notes.newPage')}</Button>
          </div>
        </div>

        <div className="flex flex-wrap gap-3">
          {fact(t('notes.overview.folders'), String(folders.length))}
          {fact(t('notes.overview.pages'), String(pages.length))}
          {fact(t('notes.overview.lastEdit'), recent[0] ? timeAgo(recent[0].page.updatedAt ?? recent[0].page.createdAt, i18n.language) : '—')}
        </div>

        {recent.length > 0 && (
          <section className="flex flex-col gap-2.5">
            {sectionTitle(t('notes.overview.recent'))}
            <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2 lg:grid-cols-3">
              {recent.map(({ page, path }) => (
                <button
                  key={page.id}
                  type="button"
                  className="flex min-w-0 cursor-pointer items-center gap-2.5 rounded-xl px-3 py-2.5 text-left hover:bg-[var(--ant-color-fill-quaternary)]"
                  style={{ border: `1px solid ${token.colorBorderSecondary}`, background: 'transparent' }}
                  onClick={() => onOpenPage(page.id)}
                >
                  <NodeIcon node={{ kind: 'page', id: page.id, page, children: [] }} size={18} />
                  <span className="flex min-w-0 flex-col">
                    <Typography.Text className="truncate font-medium">{page.title || t('notes.untitled')}</Typography.Text>
                    <Typography.Text type="secondary" className="truncate text-xs">
                      {[path.map((node) => (node.kind === 'folder' ? node.folder.name : node.page.title)).join(' › ') || space.name,
                        timeAgo(page.updatedAt ?? page.createdAt, i18n.language)].join(' · ')}
                    </Typography.Text>
                  </span>
                </button>
              ))}
            </div>
          </section>
        )}

        <section className="flex flex-col gap-2.5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            {sectionTitle(t('notes.overview.structure'))}
            <Segmented
              size="small"
              value={view}
              onChange={(value) => changeView(value as 'cards' | 'tree')}
              options={[
                { value: 'cards', icon: <AppstoreOutlined />, label: t('notes.overview.cards') },
                { value: 'tree', icon: <ApartmentOutlined />, label: t('notes.overview.tree') },
              ]}
            />
          </div>
          {!loading && tree.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('notes.overview.empty')} />
          ) : view === 'cards' ? (
            <ItemCards nodes={tree} onOpen={open} />
          ) : (
            <ItemTree nodes={tree} onOpen={open} />
          )}
        </section>
      </div>
    </div>
  )
}
