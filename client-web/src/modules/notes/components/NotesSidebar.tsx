import { useId, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { App, Button, Dropdown, Flex, Switch, Tree, Typography } from 'antd'
import type { MenuProps } from 'antd'
import type { DataNode } from 'antd/es/tree'
import {
  AppstoreOutlined,
  CheckOutlined,
  DeleteOutlined,
  DownOutlined,
  ImportOutlined,
  EllipsisOutlined,
  FileTextOutlined,
  FolderOutlined,
  HomeOutlined,
  PartitionOutlined,
  PictureOutlined,
  PlusOutlined,
  SearchOutlined,
  SettingOutlined,
  StarFilled,
} from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { PageSummaryDto } from '../models'
import { useCreateFolder, useDeleteFolder, useFolders } from '../hooks/useFolders'
import {
  useCreatePage,
  useDeletePage,
  usePageTree,
  useMovePage,
  useSetPageArchived,
  useSetPageFavorite,
} from '../hooks/usePages'
import { useSpaces } from '../hooks/useSpaces'
import { useMoveTreeItems } from '../hooks/useMoveTreeItems'
import { buildSpaceTree, computeSpaceDrop, findNode, pathTo, splitTemplateTree, type SpaceNode } from '../lib/spaceTree'
import { useNodeText } from '../hooks/useNodeText'
import { SpaceIcon } from './SpaceIcon'
import { TagFilter } from './TagFilter'
import { RecentPages } from './RecentPages'
import { DailyNoteButton } from './DailyNoteButton'
import { DailyNotesSettingsModal } from './DailyNotesSettingsModal'
import { usePermission } from '@/modules/identity/hooks/useAccess'
import { TemplatePageModal } from './TemplatePageModal'
import { TrashDrawer } from './TrashDrawer'
import { markdownToBlocks } from '../lib/markdown'

interface NotesSidebarProps {
  /** The space whose tree is shown; null shows every space's pages (the graph's "all spaces"). */
  spaceId: string | null
  onSpaceChange: (spaceId: string) => void
  selectedId: string | null
  onSelect: (id: string | null) => void
  /** The open folder, when a folder (not a page) is open. */
  selectedFolderId: string | null
  onSelectFolder: (id: string) => void
  /** Opens the space's overview page. */
  onOpenOverview: () => void
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
  selectedFolderId,
  onSelectFolder,
  onOpenOverview,
  includeArchived,
  onToggleArchived,
  tagIds,
  onTagIdsChange,
  onSearch,
}: NotesSidebarProps) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const navigate = useNavigate()
  const canReadPreferences = usePermission('notes.preferences.read')
  const canUpdatePreferences = usePermission('notes.preferences.update')
  const canReadPages = usePermission('notes.pages.read')
  const canMovePages = usePermission('notes.pages.update')
  const canMoveFolders = usePermission('notes.folders.update')
  const [dailySettingsOpen, setDailySettingsOpen] = useState(false)
  const { data: pages = [], isLoading: pagesLoading } = usePageTree(includeArchived, tagIds, spaceId)
  const { data: folders = [], isLoading: foldersLoading } = useFolders(spaceId)
  const isLoading = pagesLoading || foldersLoading
  const text = useNodeText()
  const { data: spaces = [] } = useSpaces()
  const space = spaces.find((candidate) => candidate.id === spaceId)
  // Filtering by tag leaves matching children with no matching parent, so the backend answers with
  // the matches alone and buildSpaceTree lays them out flat — which is what "who carries this tag?" asks.
  const isFiltered = tagIds.length > 0
  // While filtering, the matching pages alone: their folders would only add empty branches.
  const tree = useMemo(() => buildSpaceTree(isFiltered ? [] : folders, pages), [isFiltered, folders, pages])
  const sections = useMemo(() => splitTemplateTree(tree), [tree])
  const [templateDestination, setTemplateDestination] = useState<{ folderId: string | null } | null>(null)

  // What is open stays visible: its folders and parent pages open with it.
  const [expanded, setExpanded] = useState<string[]>([])
  const openId = selectedFolderId ?? selectedId
  const [revealed, setRevealed] = useState<string | null>(null)
  if (openId && openId !== revealed && findNode(tree, openId)) {
    setRevealed(openId)
    const ancestors = pathTo(tree, openId).map((node) => node.id)
    setExpanded((current) => [...new Set([...current, ...ancestors, ...(selectedFolderId ? [selectedFolderId] : [])])])
  }

  const createMutation = useCreatePage()
  const favoriteMutation = useSetPageFavorite()
  const archiveMutation = useSetPageArchived()
  const deleteMutation = useDeletePage()
  const moveTree = useMoveTreeItems()
  const movePage = useMovePage()
  const deleteFolder = useDeleteFolder()
  const createFolder = useCreateFolder()

  // A new folder opens at once, its name ready to type over.
  async function handleCreateFolder(parentId: string | null) {
    try {
      const folder = await createFolder.mutateAsync({ name: t('notes.folders.untitled'), parentId, spaceId })
      onSelectFolder(folder.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  const [trashOpen, setTrashOpen] = useState(false)
  // Where an imported Markdown file lands: the folder whose menu asked for it, or the top.
  const importInputId = useId()
  const [importFolder, setImportFolder] = useState<string | null>(null)

  /** A Markdown file as a new page: its first heading (or the file name) is the title. */
  async function handleImport(file: File) {
    try {
      const text = await file.text()
      const byTitle = new Map(pages.map((page) => [page.title.trim().toLowerCase(), page.id]))
      const { title, blocks } = markdownToBlocks(text, (name) => byTitle.get(name.toLowerCase()))
      const page = await createMutation.mutateAsync({
        title: title || file.name.replace(/\.(md|markdown|txt)$/i, '') || t('notes.untitled'),
        folderId: importFolder,
        spaceId,
        content: { version: 1, blocks },
      })
      onSelect(page.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.importError')))
    }
  }

  function openImport(folderId: string | null) {
    setImportFolder(folderId)
    document.getElementById(importInputId)?.click()
  }

  async function handleCreate(parentId: string | null, folderId: string | null = null) {
    try {
      const page = await createMutation.mutateAsync({ title: t('notes.untitled'), parentId, folderId, spaceId })
      onSelect(page.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  async function handleToggleFavorite(node: PageSummaryDto) {
    try {
      await favoriteMutation.mutateAsync({ id: node.id, favorite: !node.isFavorite })
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  async function handleToggleArchived(node: PageSummaryDto) {
    try {
      await archiveMutation.mutateAsync({ id: node.id, archived: !node.isArchived })
      if (node.id === selectedId && !node.isArchived && !includeArchived) onSelect(null)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function handleDelete(node: PageSummaryDto) {
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
  async function handleMoveToSpace(node: PageSummaryDto, target: string) {
    try {
      await movePage.mutateAsync({ id: node.id, body: { parentId: null, orderIndex: 0, spaceId: target } })
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function handleDeleteFolder(node: SpaceNode & { kind: 'folder' }) {
    const what = node.children.length ? text.counts(node) : ''
    modal.confirm({
      title: t('notes.folders.deleteConfirm', { name: node.folder.name }),
      content: what ? t('notes.folders.deleteContent', { what }) : t('notes.folders.deleteEmpty'),
      okText: t('common.delete'),
      cancelText: t('common.cancel'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteFolder.mutateAsync(node.id)
          const inside = node.id === selectedFolderId || (openId && pathTo(tree, openId).some((above) => above.id === node.id))
          if (inside) onOpenOverview()
        } catch (err) {
          message.error(toErrorMessage(err, t('notes.deleteError')))
        }
      },
    })
  }

  function createMenu(folderId: string | null): MenuProps['items'] {
    return [
      { key: 'page', icon: <FileTextOutlined />, label: t('notes.newPage'), onClick: () => void handleCreate(null, folderId) },
      { key: 'template', icon: <FileTextOutlined />, label: t('notes.templates.create'), onClick: () => setTemplateDestination({ folderId }) },
      { key: 'folder', icon: <FolderOutlined />, label: t('notes.folders.new'), onClick: () => void handleCreateFolder(folderId) },
      { key: 'import', icon: <ImportOutlined />, label: t('notes.importMarkdown'), onClick: () => openImport(folderId) },
    ]
  }

  function folderMenu(node: SpaceNode & { kind: 'folder' }): MenuProps['items'] {
    return [
      { key: 'page', label: t('notes.folders.newPageHere'), onClick: () => void handleCreate(null, node.id) },
      { key: 'folder', label: t('notes.folders.newFolderHere'), onClick: () => void handleCreateFolder(node.id) },
      { key: 'open', label: t('notes.folders.rename'), onClick: () => onSelectFolder(node.id) },
      { type: 'divider' },
      { key: 'delete', label: t('common.delete'), danger: true, onClick: () => handleDeleteFolder(node) },
    ]
  }

  function nodeMenu(node: PageSummaryDto): MenuProps['items'] {
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

  function renderTitle(node: PageSummaryDto): DataNode['title'] {
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

  function renderFolderTitle(node: SpaceNode & { kind: 'folder' }): DataNode['title'] {
    const name = node.folder.name
    return (
      <Flex align="center" gap={6} className="group" style={{ width: '100%' }}>
        <span style={{ flexShrink: 0 }}>{node.folder.icon ? <span>{node.folder.icon}</span> : <FolderOutlined />}</span>
        <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{name}</span>
        {node.children.length > 0 && (
          <Typography.Text type="secondary" className="text-xs tabular-nums group-hover:hidden">{node.children.length}</Typography.Text>
        )}
        <Dropdown menu={{ items: createMenu(node.id) }} trigger={['click']}>
          <Button
            type="text"
            size="small"
            icon={<PlusOutlined />}
            title={t('notes.folders.create')}
            aria-label={t('notes.folders.createIn', { name })}
            onClick={(e) => e.stopPropagation()}
            className="opacity-0 group-hover:opacity-100 [@media(hover:none)]:opacity-100"
            style={{ flexShrink: 0 }}
          />
        </Dropdown>
        <Dropdown menu={{ items: folderMenu(node) }} trigger={['click']}>
          <Button
            type="text"
            size="small"
            icon={<EllipsisOutlined />}
            aria-label={t('notes.folders.actions', { name })}
            onClick={(e) => e.stopPropagation()}
            className="opacity-0 group-hover:opacity-100 [@media(hover:none)]:opacity-100"
            style={{ flexShrink: 0 }}
          />
        </Dropdown>
      </Flex>
    )
  }

  function toDataNodes(nodes: SpaceNode[]): DataNode[] {
    return nodes.map((node) => ({
      key: node.id,
      title: node.kind === 'folder' ? renderFolderTitle(node) : renderTitle(node.page),
      children: node.children.length > 0 ? toDataNodes(node.children) : undefined,
    }))
  }

  async function handleDrop(info: Parameters<NonNullable<React.ComponentProps<typeof Tree>['onDrop']>>[0]) {
    if (!spaceId || isFiltered || moveTree.isPending) return
    const dropPos = info.node.pos.split('-')
    const relativeIndex = info.dropPosition - Number(dropPos[dropPos.length - 1])
    const moves = computeSpaceDrop(sections.pages, String(info.dragNode.key), String(info.node.key), relativeIndex, info.dropToGap)
    if (moves === 'cycle') {
      message.error(t('notes.moveCycle'))
      return
    }
    if (moves === 'folderInPage') {
      message.error(t('notes.folders.folderInPage'))
      return
    }
    const first = moves[0]
    if (!first || (first.kind === 'page' ? !canMovePages : !canMoveFolders)) return
    try {
      await moveTree.mutateAsync({
        spaceId,
        kind: first.kind,
        parentId: first.body.parentId,
        folderId: first.kind === 'page' ? first.body.folderId ?? null : null,
        itemIds: moves.map((move) => move.id),
      })
      const destination = first.body.parentId ?? (first.kind === 'page' ? first.body.folderId : null)
      if (destination) setExpanded((current) => [...new Set([...current, destination])])
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function handleTreeSelect(keys: React.Key[]) {
    const node = keys.length > 0 ? findNode(tree, String(keys[0])) : null
    if (!node) return
    if (node.kind === 'folder') onSelectFolder(node.id)
    else onSelect(node.id)
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

      <nav className="notes-shortcuts" aria-label={t('notes.shortcuts')}>
        {spaceId && (
          <button type="button" className="notes-shortcut" aria-current={!selectedId && !selectedFolderId ? 'page' : undefined} onClick={onOpenOverview}>
            <HomeOutlined />
            <span>{t('notes.overview.title')}</span>
          </button>
        )}
        <div className="notes-shortcut-row">
          <DailyNoteButton onOpen={onSelect} />
          {canReadPreferences && canUpdatePreferences && canReadPages && (
            <Button type="text" size="small" className="notes-shortcut-extra" icon={<SettingOutlined />} aria-label={t('notes.daily.settings')} title={t('notes.daily.settings')} onClick={() => setDailySettingsOpen(true)} />
          )}
        </div>
        <RecentPages selectedId={selectedId} onSelect={onSelect} />
      </nav>

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
          <Dropdown menu={{ items: createMenu(null) }} trigger={['click']}>
            <Button type="text" size="small" icon={<PlusOutlined />} title={t('notes.folders.create')} aria-label={t('notes.folders.create')} />
          </Dropdown>
        </Flex>
      </Flex>

      <div className="mb-2 px-2">
        <TagFilter value={tagIds} onChange={onTagIdsChange} />
      </div>

      <div style={{ flex: 1, overflow: 'auto' }}>
        <Tree
          blockNode
          // A filtered/all-spaces view is not one complete hierarchy to reorder.
          draggable={!!spaceId && !isFiltered && !moveTree.isPending && {
            nodeDraggable: (node) => findNode(sections.pages, String(node.key))?.kind === 'folder' ? canMoveFolders : canMovePages,
          }}
          allowDrop={({ dragNode, dropNode, dropPosition }) => {
            const moves = computeSpaceDrop(sections.pages, String(dragNode.key), String(dropNode.key), dropPosition, dropPosition !== 0)
            return Array.isArray(moves) && moves.length > 0
          }}
          showIcon={false}
          selectedKeys={openId ? [openId] : []}
          onSelect={handleTreeSelect}
          expandedKeys={expanded}
          onExpand={(keys) => setExpanded(keys.map(String))}
          onDragEnter={({ expandedKeys }) => setExpanded(expandedKeys.map(String))}
          onDrop={handleDrop}
          treeData={toDataNodes(sections.pages)}
          disabled={isLoading || moveTree.isPending}
        />
        {sections.templates.length > 0 && (
          <>
            <Typography.Text type="secondary" className="mt-3 block px-2 text-xs font-semibold uppercase tracking-wider">
              {t('notes.templates.title')}
            </Typography.Text>
            <Tree
              blockNode
              showIcon={false}
              selectedKeys={openId ? [openId] : []}
              onSelect={handleTreeSelect}
              expandedKeys={expanded}
              onExpand={(keys) => setExpanded(keys.map(String))}
              treeData={toDataNodes(sections.templates)}
              disabled={isLoading}
            />
          </>
        )}
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
        <Button type="text" size="small" icon={<DeleteOutlined />} className="ms-auto" onClick={() => setTrashOpen(true)}>
          {t('notes.trash.title')}
        </Button>
      </Flex>
      <input
        id={importInputId}
        type="file"
        accept=".md,.markdown,.txt,text/markdown,text/plain"
        hidden
        aria-label={t('notes.importMarkdown')}
        onChange={(event) => {
          const file = event.target.files?.[0]
          event.target.value = ''
          if (file) void handleImport(file)
        }}
      />
      <TrashDrawer
        open={trashOpen}
        onClose={() => setTrashOpen(false)}
        onRestored={(id) => {
          setTrashOpen(false)
          onSelect(id)
        }}
      />
      {templateDestination && (
        <TemplatePageModal
          spaceId={spaceId}
          folderId={templateDestination.folderId}
          onClose={() => setTemplateDestination(null)}
          onCreated={(id) => {
            setTemplateDestination(null)
            onSelect(id)
          }}
        />
      )}
      {dailySettingsOpen && <DailyNotesSettingsModal onClose={() => setDailySettingsOpen(false)} />}
    </Flex>
  )
}
