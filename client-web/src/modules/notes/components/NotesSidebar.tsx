import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { App, Button, Dropdown, Flex, Switch, Tree, Typography } from 'antd'
import type { MenuProps } from 'antd'
import type { DataNode } from 'antd/es/tree'
import {
  AppstoreOutlined,
  CheckOutlined,
  DownOutlined,
  EllipsisOutlined,
  FileTextOutlined,
  PartitionOutlined,
  PictureOutlined,
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
import { useSpaces } from '../hooks/useSpaces'
import { computeReorder, isSelfOrDescendant } from '../lib/moveMath'
import { SpaceIcon } from './SpaceIcon'
import { TagFilter } from './TagFilter'

interface NotesSidebarProps {
  /** The space whose tree is shown; null shows every space's pages (the graph's "all spaces"). */
  spaceId: string | null
  onSpaceChange: (spaceId: string) => void
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
  spaceId,
  onSpaceChange,
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
  const { tree, isLoading } = usePageTree(includeArchived, tagIds, spaceId)
  const { data: spaces = [] } = useSpaces()
  const space = spaces.find((candidate) => candidate.id === spaceId)
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
      const page = await createMutation.mutateAsync({ title: t('notes.untitled'), parentId, spaceId })
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

  // To the top of another space; the subpages come along.
  async function handleMoveToSpace(node: PageTreeNode, target: string) {
    try {
      await reorderMutation.mutateAsync([{ id: node.id, body: { parentId: null, orderIndex: 0, spaceId: target } }])
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function nodeMenu(node: PageTreeNode): MenuProps['items'] {
    const otherSpaces = spaces.filter((candidate) => candidate.id !== node.spaceId)
    return [
      { key: 'new-child', label: t('notes.newChild'), onClick: () => handleCreate(node.id) },
      ...(otherSpaces.length > 0
        ? [
            {
              key: 'move',
              label: t('notes.spaces.moveTo'),
              children: otherSpaces.map((candidate) => ({
                key: `move-${candidate.id}`,
                label: (
                  <span className="flex items-center gap-2">
                    <SpaceIcon space={candidate} size={18} />
                    {candidate.name}
                  </span>
                ),
                onClick: () => handleMoveToSpace(node, candidate.id),
              })),
            },
          ]
        : []),
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
        {/* Shown on hover; a touch screen has no hover, so there they stay visible. */}
        <Button
          type="text"
          size="small"
          icon={<PlusOutlined />}
          title={t('notes.newChild')}
          aria-label={t('notes.newChildOf', { title: node.title || t('notes.untitled') })}
          onClick={(e) => {
            e.stopPropagation()
            void handleCreate(node.id)
          }}
          className="opacity-0 group-hover:opacity-100 [@media(hover:none)]:opacity-100"
          style={{ flexShrink: 0 }}
        />
        <Dropdown menu={{ items: nodeMenu(node) }} trigger={['click']}>
          <Button
            type="text"
            size="small"
            icon={<EllipsisOutlined />}
            aria-label={t('notes.pageActions', { title: node.title || t('notes.untitled') })}
            onClick={(e) => e.stopPropagation()}
            className="opacity-0 group-hover:opacity-100 [@media(hover:none)]:opacity-100"
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
      <Dropdown
        trigger={['click']}
        menu={{
          items: [
            ...spaces.map((candidate) => ({
              key: candidate.id,
              label: (
                <span className="flex items-center gap-2">
                  <SpaceIcon space={candidate} size={20} />
                  <span className="flex-1 truncate">{candidate.name}</span>
                  {candidate.id === spaceId && <CheckOutlined />}
                </span>
              ),
              onClick: () => onSpaceChange(candidate.id),
            })),
            { type: 'divider' as const },
            { key: 'all', icon: <AppstoreOutlined />, label: t('notes.spaces.all'), onClick: () => navigate('/notes') },
          ],
        }}
      >
        <button
          type="button"
          className="mb-2 flex w-full cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left hover:bg-[var(--ant-color-fill-tertiary)]"
          aria-label={t('notes.spaces.switch')}
        >
          {space ? <SpaceIcon space={space} size={28} /> : <AppstoreOutlined className="text-lg" />}
          <Typography.Text strong className="flex-1 truncate">
            {space?.name ?? t('notes.spaces.all')}
          </Typography.Text>
          <DownOutlined className="text-xs opacity-60" />
        </button>
      </Dropdown>

      <Flex align="center" justify="space-between" className="mb-2 px-2">
        <Typography.Text type="secondary" className="text-xs font-semibold uppercase tracking-wider">
          {t('notes.pagesTitle')}
        </Typography.Text>
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
            icon={<PictureOutlined />}
            title={t('notes.library.title')}
            aria-label={t('notes.library.title')}
            onClick={() => navigate('/notes/library')}
          />
          <Button
            type="text"
            size="small"
            icon={<PlusOutlined />}
            title={t('notes.newPage')}
            aria-label={t('notes.newPage')}
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
        <Switch size="small" checked={includeArchived} onChange={onToggleArchived} aria-label={t('notes.showArchived')} />
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('notes.showArchived')}
        </Typography.Text>
      </Flex>
    </Flex>
  )
}
