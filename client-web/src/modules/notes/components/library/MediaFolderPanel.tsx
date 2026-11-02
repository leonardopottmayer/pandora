import { useState, type DragEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Dropdown, Tree, Typography, theme } from 'antd'
import type { DataNode } from 'antd/es/tree'
import { AppstoreOutlined, DisconnectOutlined, EllipsisOutlined, FolderOutlined, InboxOutlined, PlusOutlined } from '@ant-design/icons'
import type { MediaFolderDto, MediaFoldersDto } from '../../models'
import { buildFolderTree, type MediaFolderNode } from '../../lib/mediaFolders'
import { MEDIA_DRAG_TYPE } from '../../lib/media'

/** Where the grid looks: every item, the unsorted ones, the ones no note uses, or one folder's (by id). */
export type LibraryPlace = 'all' | 'unsorted' | 'unused' | string

interface MediaFolderPanelProps {
  data: MediaFoldersDto | undefined
  place: LibraryPlace
  onPlaceChange: (place: LibraryPlace) => void
  /** An item dropped on a folder, or on "Unsorted" (null). Left out, nothing takes drops. */
  onDropItem?: (mediaId: string, folderId: string | null) => void
  /** Left out, the panel only picks a place: no new folder, no folder menu (the editor's picker). */
  onCreateFolder?: (parentId: string | null) => void
  onRenameFolder?: (folder: MediaFolderDto) => void
  onMoveFolder?: (folder: MediaFolderDto) => void
  onDeleteFolder?: (folder: MediaFolderDto) => void
  /** Shows the "Unused" place, for the library's clean-up. */
  showUnused?: boolean
}

/** The library's places: everything, the unsorted items, and the folder tree. Items drop onto them. */
export function MediaFolderPanel({
  data,
  place,
  onPlaceChange,
  onDropItem,
  onCreateFolder,
  onRenameFolder,
  onMoveFolder,
  onDeleteFolder,
  showUnused = false,
}: MediaFolderPanelProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  // The place an item is being dragged over, to light it up.
  const [dropTarget, setDropTarget] = useState<string | null>(null)

  const tree = buildFolderTree(data?.folders ?? [])

  /** Native drop wiring for a place that takes items: only our own drag type, not files or text. */
  function dropZone(key: string, folderId: string | null) {
    if (!onDropItem) return {}
    return {
      onDragOver: (e: DragEvent) => {
        if (!e.dataTransfer.types.includes(MEDIA_DRAG_TYPE)) return
        e.preventDefault()
        e.dataTransfer.dropEffect = 'move'
        setDropTarget(key)
      },
      onDragLeave: () => setDropTarget((current) => (current === key ? null : current)),
      onDrop: (e: DragEvent) => {
        const id = e.dataTransfer.getData(MEDIA_DRAG_TYPE)
        setDropTarget(null)
        if (!id) return
        e.preventDefault()
        onDropItem(id, folderId)
      },
    }
  }

  const highlight = (key: string) =>
    dropTarget === key ? { background: token.colorPrimaryBg, outline: `1px dashed ${token.colorPrimary}` } : undefined

  const place_ = (key: LibraryPlace, icon: ReactNode, label: string, count: number | undefined, folderId?: string | null) => (
    <button
      type="button"
      className="flex w-full cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-left hover:bg-[var(--ant-color-fill-tertiary)]"
      style={{
        ...(place === key ? { background: token.colorPrimaryBg, color: token.colorPrimary } : undefined),
        ...highlight(key),
      }}
      onClick={() => onPlaceChange(key)}
      {...(folderId !== undefined ? dropZone(key, folderId) : {})}
    >
      {icon}
      <span className="flex-1 truncate">{label}</span>
      <Typography.Text type="secondary" className="text-xs tabular-nums">
        {count ?? ''}
      </Typography.Text>
    </button>
  )

  function toData(nodes: MediaFolderNode[]): DataNode[] {
    return nodes.map((node) => ({
      key: node.id,
      title: (
        <span className="group flex min-w-0 items-center gap-1.5 rounded" style={highlight(node.id)} {...dropZone(node.id, node.id)}>
          <FolderOutlined />
          <span className="flex-1 truncate">{node.name}</span>
          <Typography.Text type="secondary" className="text-xs tabular-nums group-hover:hidden">
            {node.itemCount || ''}
          </Typography.Text>
          {onCreateFolder && onRenameFolder && onMoveFolder && onDeleteFolder && (
          <span className="hidden items-center group-hover:flex [@media(hover:none)]:flex">
            <Button
              type="text"
              size="small"
              icon={<PlusOutlined />}
              aria-label={t('notes.library.newSubfolderOf', { name: node.name })}
              onClick={(e) => {
                e.stopPropagation()
                onCreateFolder?.(node.id)
              }}
            />
            <Dropdown
              trigger={['click']}
              menu={{
                items: [
                  { key: 'rename', label: t('notes.library.rename'), onClick: () => onRenameFolder?.(node) },
                  { key: 'move', label: t('notes.library.moveTo'), onClick: () => onMoveFolder?.(node) },
                  { type: 'divider' },
                  { key: 'delete', danger: true, label: t('common.delete'), onClick: () => onDeleteFolder?.(node) },
                ],
              }}
            >
              <Button
                type="text"
                size="small"
                icon={<EllipsisOutlined />}
                aria-label={t('notes.library.folderActions', { name: node.name })}
                onClick={(e) => e.stopPropagation()}
              />
            </Dropdown>
          </span>
          )}
        </span>
      ),
      children: node.children.length ? toData(node.children) : undefined,
    }))
  }

  return (
    <div className="flex flex-col gap-1">
      {place_('all', <AppstoreOutlined />, t('notes.library.all'), data?.totalCount)}
      {place_('unsorted', <InboxOutlined />, t('notes.library.unsorted'), data?.unsortedCount, null)}
      {showUnused && place_('unused', <DisconnectOutlined />, t('notes.library.unused'), data?.unusedCount)}

      <div className="mt-3 flex items-center justify-between px-2">
        <Typography.Text type="secondary" className="text-xs font-semibold uppercase tracking-wider">
          {t('notes.library.folders')}
        </Typography.Text>
        {onCreateFolder && (
          <Button
            type="text"
            size="small"
            icon={<PlusOutlined />}
            title={t('notes.library.newFolder')}
            aria-label={t('notes.library.newFolder')}
            onClick={() => onCreateFolder(null)}
          />
        )}
      </div>

      {tree.length === 0 ? (
        <Typography.Text type="secondary" className="px-2 text-xs">
          {t('notes.library.noFolders')}
        </Typography.Text>
      ) : (
        <Tree
          // Remounted when the folders first arrive, so they start expanded.
          key={data ? 'loaded' : 'loading'}
          blockNode
          defaultExpandAll
          selectedKeys={place !== 'all' && place !== 'unsorted' && place !== 'unused' ? [place] : []}
          onSelect={(keys) => keys.length && onPlaceChange(String(keys[0]))}
          treeData={toData(tree)}
          style={{ background: 'transparent' }}
        />
      )}
    </div>
  )
}
