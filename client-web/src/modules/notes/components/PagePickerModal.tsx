import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Empty, Input, Modal, Tree, theme } from 'antd'
import type { DataNode } from 'antd/es/tree'
import { FileTextOutlined, SearchOutlined } from '@ant-design/icons'
import { useSpaces } from '../hooks/useSpaces'
import { buildTree } from '../lib/buildTree'
import { filterPages } from '../lib/filterPages'
import type { PageSummaryDto, PageTreeNode } from '../models'
import { SpaceIcon } from './SpaceIcon'

interface PagePickerModalProps {
  open: boolean
  pages: PageSummaryDto[]
  onPick: (page: PageSummaryDto) => void
  onClose: () => void
}

function PageLabel({ page }: { page: PageSummaryDto }) {
  const { t } = useTranslation()
  return (
    <span className="flex min-w-0 items-center gap-2" style={{ opacity: page.isArchived ? 0.5 : 1 }}>
      <span className="shrink-0">{page.icon ?? <FileTextOutlined />}</span>
      <span className="truncate">{page.title || t('notes.untitled')}</span>
    </span>
  )
}

/**
 * Picks the page a link points at: every space's tree, the way the sidebar shows it, and a search
 * that lists matches with the path to each — two pages can share a title.
 */
export function PagePickerModal({ open, pages, onPick, onClose }: PagePickerModalProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const { data: spaces = [] } = useSpaces()
  const [search, setSearch] = useState('')
  const term = search.trim()

  const byId = useMemo(() => new Map(pages.map((page) => [page.id, page])), [pages])

  const treeData = useMemo<DataNode[]>(() => {
    const toNodes = (nodes: PageTreeNode[]): DataNode[] =>
      nodes.map((node) => ({
        key: node.id,
        title: <PageLabel page={node} />,
        children: node.children.length > 0 ? toNodes(node.children) : undefined,
      }))
    return spaces
      .map((space) => ({
        key: `space:${space.id}`,
        selectable: false,
        title: (
          <span className="flex items-center gap-2 font-medium">
            <SpaceIcon space={space} size={18} />
            {space.name}
          </span>
        ),
        children: toNodes(buildTree(pages.filter((page) => page.spaceId === space.id))),
      }))
      .filter((node) => node.children.length > 0)
  }, [pages, spaces])

  const results = term ? filterPages(pages, term, 50) : []

  /** Where a page sits: its space, then its ancestors. */
  function pathOf(page: PageSummaryDto): string {
    const parts: string[] = []
    for (let parent = page.parentId ? byId.get(page.parentId) : undefined; parent; parent = parent.parentId ? byId.get(parent.parentId) : undefined) {
      parts.unshift(parent.title)
    }
    const space = spaces.find((candidate) => candidate.id === page.spaceId)
    return [space?.name, ...parts].filter(Boolean).join(' › ')
  }

  return (
    <Modal
      open={open}
      onCancel={onClose}
      afterOpenChange={(visible) => !visible && setSearch('')}
      footer={null}
      width={560}
      title={t('notes.pagePicker.title')}
      destroyOnHidden
    >
      <Input
        allowClear
        autoFocus
        prefix={<SearchOutlined style={{ color: token.colorTextTertiary }} />}
        placeholder={t('notes.pagePicker.search')}
        aria-label={t('notes.pagePicker.search')}
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        onPressEnter={() => results[0] && onPick(results[0])}
      />

      <div className="mt-3 max-h-[60vh] overflow-auto">
        {term ? (
          results.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('notes.pagePicker.empty')} />
          ) : (
            <div className="flex flex-col">
              {results.map((page) => (
                <button
                  key={page.id}
                  type="button"
                  className="flex cursor-pointer flex-col items-start rounded-md px-2 py-1.5 text-left hover:bg-[var(--ant-color-fill-tertiary)]"
                  onClick={() => onPick(page)}
                >
                  <PageLabel page={page} />
                  <span className="truncate pl-6 text-xs" style={{ color: token.colorTextTertiary }}>
                    {pathOf(page)}
                  </span>
                </button>
              ))}
            </div>
          )
        ) : (
          <Tree
            // Expanding everything happens on mount, so it mounts again once the spaces arrive.
            key={spaces.length}
            blockNode
            defaultExpandAll
            showIcon={false}
            treeData={treeData}
            onSelect={(keys) => {
              const page = byId.get(String(keys[0]))
              if (page) onPick(page)
            }}
          />
        )}
      </div>
    </Modal>
  )
}
