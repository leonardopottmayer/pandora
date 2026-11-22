import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Empty, Input, Segmented, Typography } from 'antd'
import { AppstoreOutlined, EllipsisOutlined, FolderAddOutlined, PlusOutlined, TableOutlined, UnorderedListOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { useDeleteFolder, useUpdateFolder } from '../hooks/useFolders'
import type { SpaceNode } from '../lib/spaceTree'
import type { FolderDto } from '../models'
import { useNodeText } from '../hooks/useNodeText'
import { ItemCards, ItemList } from './SpaceItems'
import { PageIconPicker } from './PageIconPicker'
import { FolderPropertiesTable } from './FolderPropertiesTable'

interface FolderViewProps {
  folder: FolderDto
  /** The folder's node in its space's tree, for what it holds. */
  node: SpaceNode | null
  onOpen: (node: SpaceNode) => void
  onCreatePage: (folderId: string) => void
  onCreateFolder: (parentId: string) => void
  onDeleted: () => void
  includeArchived?: boolean
  tagIds?: string[]
}

const VIEW_KEY = 'pandora.notes_folder_view'

function readView(): 'list' | 'cards' | 'table' {
  try {
    const stored = localStorage.getItem(VIEW_KEY)
    return stored === 'cards' || stored === 'table' ? stored : 'list'
  } catch {
    return 'list'
  }
}

/** A folder opened: its name and description, edited in place, and what it holds. */
export function FolderView({ folder, node, onOpen, onCreatePage, onCreateFolder, onDeleted, includeArchived = false, tagIds = [] }: FolderViewProps) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const update = useUpdateFolder()
  const remove = useDeleteFolder()
  const text = useNodeText()
  const [view, setView] = useState(readView)
  // Typed locally and saved on leaving the field; the list's copy comes back with the refetch.
  const [name, setName] = useState(folder.name)
  const [description, setDescription] = useState(folder.description ?? '')
  const [seen, setSeen] = useState(folder.id)
  if (seen !== folder.id) {
    setSeen(folder.id)
    setName(folder.name)
    setDescription(folder.description ?? '')
  }

  async function save(patch: Partial<{ name: string; icon: string | null; description: string }>) {
    const body = { name: name.trim() || folder.name, icon: folder.icon, description: description.trim() || null, ...patch }
    if (body.name === folder.name && body.icon === folder.icon && (body.description || null) === folder.description) return
    try {
      await update.mutateAsync({ id: folder.id, body })
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  function confirmDelete() {
    const what = node && node.children.length ? text.counts(node) : ''
    modal.confirm({
      title: t('notes.folders.deleteConfirm', { name: folder.name }),
      content: what ? t('notes.folders.deleteContent', { what }) : t('notes.folders.deleteEmpty'),
      okText: t('common.delete'),
      cancelText: t('common.cancel'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await remove.mutateAsync(folder.id)
          onDeleted()
        } catch (err) {
          message.error(toErrorMessage(err, t('notes.deleteError')))
        }
      },
    })
  }

  const changeView = (value: 'list' | 'cards' | 'table') => {
    setView(value)
    try {
      localStorage.setItem(VIEW_KEY, value)
    } catch {
      // Private mode: the choice lasts until the reload.
    }
  }

  const children = node?.children ?? []

  return (
    <div className="h-full overflow-auto">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 px-5 py-8 md:px-8">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex min-w-0 flex-1 items-start gap-3">
            <PageIconPicker value={folder.icon} onChange={(icon) => void save({ icon })} size="large" />
            <div className="flex min-w-0 flex-1 flex-col">
              <Input
                variant="borderless"
                value={name}
                aria-label={t('notes.folders.namePlaceholder')}
                placeholder={t('notes.folders.namePlaceholder')}
                className="!px-0 !text-2xl !font-semibold"
                onChange={(event) => setName(event.target.value)}
                onBlur={() => void save({})}
                onPressEnter={(event) => event.currentTarget.blur()}
              />
              <Input.TextArea
                variant="borderless"
                autoSize
                value={description}
                aria-label={t('notes.folders.descriptionPlaceholder')}
                placeholder={t('notes.folders.descriptionPlaceholder')}
                className="!px-0"
                style={{ resize: 'none' }}
                onChange={(event) => setDescription(event.target.value)}
                onBlur={() => void save({})}
              />
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button icon={<FolderAddOutlined />} onClick={() => onCreateFolder(folder.id)}>{t('notes.folders.new')}</Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => onCreatePage(folder.id)}>{t('notes.newPage')}</Button>
            <Dropdown
              trigger={['click']}
              placement="bottomRight"
              menu={{ items: [{ key: 'delete', danger: true, label: t('common.delete'), onClick: confirmDelete }] }}
            >
              <Button icon={<EllipsisOutlined />} aria-label={t('notes.folders.actions', { name: folder.name })} />
            </Dropdown>
          </div>
        </div>

        <section className="flex flex-col gap-2.5">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <Typography.Text type="secondary" className="text-xs font-semibold uppercase tracking-wider">
              {node && children.length ? `${text.counts(node)} · ${t('notes.edited', { when: text.edited(node) })}` : t('notes.folders.content')}
            </Typography.Text>
            {(children.length > 0 || view === 'table') && (
              <Segmented
                size="small"
                value={view}
                onChange={(value) => changeView(value as 'list' | 'cards' | 'table')}
                options={[
                  { value: 'list', icon: <UnorderedListOutlined />, label: t('notes.overview.list') },
                  { value: 'cards', icon: <AppstoreOutlined />, label: t('notes.overview.cards') },
                  { value: 'table', icon: <TableOutlined />, label: t('notes.folderTable.view') },
                ]}
              />
            )}
          </div>
          {view === 'table' ? (
            <FolderPropertiesTable folderId={folder.id} includeArchived={includeArchived} tagIds={tagIds} onOpen={onOpen} />
          ) : children.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('notes.folders.empty')} />
          ) : view === 'cards' ? (
            <ItemCards nodes={children} onOpen={onOpen} />
          ) : (
            <ItemList nodes={children} onOpen={onOpen} />
          )}
        </section>
      </div>
    </div>
  )
}
