import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { App, Button, Dropdown, Flex, Switch, Tree, Typography } from 'antd'
import type { MenuProps } from 'antd'
import type { DataNode } from 'antd/es/tree'
import {
  EllipsisOutlined,
  FileTextOutlined,
  PartitionOutlined,
  PlusOutlined,
  SearchOutlined,
  StarFilled,
} from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { PageTreeNode } from '../models'
import {
  useCreatePage,
  useDeletePage,
  usePageTree,
  useReorderPages,
  useSetPageArchived,
  useSetPageFavorite,
} from '../hooks/usePages'
import { computeReorder, isSelfOrDescendant } from '../lib/moveMath'
import { TagFilter } from './TagFilter'

interface NotesSidebarProps {
  selectedId: string | null
  onSelect: (id: string | null) => void
  includeArchived: boolean
  onToggleArchived: (value: boolean) => void
  /** Tags the list is narrowed by; with any picked, the tree flattens into the matching pages. */
  tagIds: string[]
  onTagIdsChange: (tagIds: string[]) => void
  /** Raises the search palette — the same one Ctrl+K opens. */
  onSearch: () => void
}

export function NotesSidebar({
  selectedId,
  onSelect,
  includeArchived,
  onToggleArchived,
  tagIds,
  onTagIdsChange,
  onSearch,
}: NotesSidebarProps) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const navigate = useNavigate()
  const { tree, isLoading } = usePageTree(includeArchived, tagIds)
  // Filtering by tag leaves matching children with no matching parent, so the backend answers with
  // the matches alone and buildTree lays them out flat — which is what "who carries this tag?" asks.
  const isFiltered = tagIds.length > 0

  const createMutation = useCreatePage()
  const favoriteMutation = useSetPageFavorite()
  const archiveMutation = useSetPageArchived()
  const deleteMutation = useDeletePage()
  const reorderMutation = useReorderPages()

  async function handleCreate(parentId: string | null) {
    try {
      const page = await createMutation.mutateAsync({ title: t('notes.untitled'), parentId })
      onSelect(page.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  async function handleToggleFavorite(node: PageTreeNode) {
    try {
      await favoriteMutation.mutateAsync({ id: node.id, favorite: !node.isFavorite })
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  async function handleToggleArchived(node: PageTreeNode) {
    try {
      await archiveMutation.mutateAsync({ id: node.id, archived: !node.isArchived })
      if (node.id === selectedId && !node.isArchived && !includeArchived) onSelect(null)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function handleDelete(node: PageTreeNode) {
    modal.confirm({
      title: t('notes.deleteConfirm'),
      okText: t('common.delete'),
      cancelText: t('common.cancel'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteMutation.mutateAsync(node.id)
          if (node.id === selectedId) onSelect(null)
        } catch (err) {
          message.error(toErrorMessage(err, t('notes.deleteError')))
        }
      },
    })
  }

  function nodeMenu(node: PageTreeNode): MenuProps['items'] {
    return [
      { key: 'new-child', label: t('notes.newChild'), onClick: () => handleCreate(node.id) },
      {
        key: 'favorite',
        label: node.isFavorite ? t('notes.unfavorite') : t('notes.favorite'),
        onClick: () => handleToggleFavorite(node),
      },
      {
        key: 'archive',
        label: node.isArchived ? t('notes.unarchive') : t('notes.archive'),
        onClick: () => handleToggleArchived(node),
      },
      { type: 'divider' },
      { key: 'delete', label: t('common.delete'), danger: true, onClick: () => handleDelete(node) },
    ]
  }

  function renderTitle(node: PageTreeNode): DataNode['title'] {
    return (
      <Flex align="center" gap={6} className="group" style={{ width: '100%' }}>
        <span style={{ flexShrink: 0 }}>
          {node.icon ? <span>{node.icon}</span> : <FileTextOutlined />}
        </span>
        <span
          style={{
            flex: 1,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            opacity: node.isArchived ? 0.5 : 1,
          }}
        >
          {node.title || t('notes.untitled')}
        </span>
        {node.isFavorite && <StarFilled style={{ color: '#fadb14', flexShrink: 0 }} />}
        <Dropdown menu={{ items: nodeMenu(node) }} trigger={['click']}>
          <Button
            type="text"
            size="small"
            icon={<EllipsisOutlined />}
            onClick={(e) => e.stopPropagation()}
            className="opacity-0 group-hover:opacity-100"
            style={{ flexShrink: 0 }}
          />
        </Dropdown>
      </Flex>
    )
  }

  function toDataNodes(nodes: PageTreeNode[]): DataNode[] {
    return nodes.map((node) => ({
      key: node.id,
      title: renderTitle(node),
      children: node.children.length > 0 ? toDataNodes(node.children) : undefined,
    }))
  }

  async function handleDrop(info: Parameters<NonNullable<React.ComponentProps<typeof Tree>['onDrop']>>[0]) {
    const dragKey = String(info.dragNode.key)
    const dropKey = String(info.node.key)
    const dropPos = info.node.pos.split('-')
    const relativeIndex = info.dropPosition - Number(dropPos[dropPos.length - 1])

    if (isSelfOrDescendant(tree, dragKey, dropKey)) {
      message.error(t('notes.moveCycle'))
      return
    }

    const moves = computeReorder(tree, dragKey, dropKey, relativeIndex, info.dropToGap)
    if (moves.length === 0) return

    try {
      await reorderMutation.mutateAsync(moves)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  return (
    <Flex vertical style={{ height: '100%' }}>
      <Flex align="center" justify="space-between" className="mb-2 px-2">
        <Typography.Text strong>{t('nav.notes')}</Typography.Text>
        <Flex align="center" gap={2}>
          <Button
            type="text"
            size="small"
            icon={<SearchOutlined />}
            title={t('notes.openSearch')}
            onClick={onSearch}
          />
          <Button
            type="text"
            size="small"
            icon={<PartitionOutlined />}
            title={t('notes.openGraph')}
            onClick={() => navigate('/notes/graph')}
          />
          <Button
            type="text"
            size="small"
            icon={<PlusOutlined />}
            title={t('notes.newPage')}
            onClick={() => handleCreate(null)}
          />
        </Flex>
      </Flex>

      <div className="mb-2 px-2">
        <TagFilter value={tagIds} onChange={onTagIdsChange} />
      </div>

      <div style={{ flex: 1, overflow: 'auto' }}>
        <Tree
          blockNode
          // Dragging is off while filtering: the list is no longer the tree, so a drop would be
          // reordering against siblings that are not on screen.
          draggable={!isFiltered}
          showIcon={false}
          selectedKeys={selectedId ? [selectedId] : []}
          onSelect={(keys) => onSelect(keys.length > 0 ? String(keys[0]) : null)}
          onDrop={handleDrop}
          treeData={toDataNodes(tree)}
          disabled={isLoading}
        />
        {!isLoading && tree.length === 0 && (
          <Typography.Text type="secondary" className="block px-2 py-4">
            {isFiltered ? t('notes.tagFilterEmpty') : t('notes.empty')}
          </Typography.Text>
        )}
      </div>

      <Flex align="center" gap={8} className="mt-2 px-2 pt-2" style={{ borderTop: '1px solid rgba(128,128,128,0.2)' }}>
        <Switch size="small" checked={includeArchived} onChange={onToggleArchived} />
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('notes.showArchived')}
        </Typography.Text>
      </Flex>
    </Flex>
  )
}
