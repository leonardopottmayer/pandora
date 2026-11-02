import { useRef, useState, type DragEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { App, Button, Empty, Input, Modal, Segmented, Skeleton, TreeSelect, Typography, theme } from 'antd'
import { CloudUploadOutlined, FolderAddOutlined, SearchOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { MediaFolderDto, MediaItemDto } from '../models'
import {
  useCreateMediaFolder,
  useDeleteMediaFolder,
  useMediaFolders,
  useMediaList,
  useMoveMedia,
  useMoveMediaFolder,
  useRenameMediaFolder,
  useUploadMedia,
} from '../hooks/useMedia'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { folderAndDescendants, folderOptions, folderPath, fromFolderOption, toFolderOption } from '../lib/mediaFolders'
import { MediaCard } from '../components/library/MediaCard'
import { MediaDetailDrawer } from '../components/library/MediaDetailDrawer'
import { MediaFolderPanel, type LibraryPlace } from '../components/library/MediaFolderPanel'

type KindFilter = 'all' | 'image' | 'file'

/** A folder dialog: naming a new one, renaming one, or picking where one goes. */
type FolderDialog =
  | { mode: 'create'; parentId: string | null }
  | { mode: 'rename'; folder: MediaFolderDto }
  | { mode: 'move'; folder: MediaFolderDto }

/** The media library: the images and files notes embed, in virtual folders shared by every space. */
export function LibraryPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [searchParams, setSearchParams] = useSearchParams()
  const fileInput = useRef<HTMLInputElement>(null)

  // Where the grid looks lives in the URL, so a reload or a link opens the same folder.
  const place: LibraryPlace = searchParams.get('folder') ?? 'all'
  const [kind, setKind] = useState<KindFilter>('all')
  const [search, setSearch] = useState('')
  const q = useDebouncedValue(search)
  const [selected, setSelected] = useState<MediaItemDto | null>(null)
  const [dialog, setDialog] = useState<FolderDialog | null>(null)
  const [dialogValue, setDialogValue] = useState('')
  const [draggingFiles, setDraggingFiles] = useState(false)

  const { data: folderData } = useMediaFolders()
  const folders = folderData?.folders ?? []
  // A search looks through the whole library, wherever the item is.
  const searching = q.trim().length > 0
  const { data: items, isLoading } = useMediaList({
    folderId: !searching && place !== 'all' && place !== 'unsorted' ? place : null,
    unsorted: !searching && place === 'unsorted',
    kind: kind === 'all' ? null : kind,
    q,
  })

  const upload = useUploadMedia()
  const moveItem = useMoveMedia()
  const createFolder = useCreateMediaFolder()
  const renameFolder = useRenameMediaFolder()
  const moveFolder = useMoveMediaFolder()
  const deleteFolder = useDeleteMediaFolder()

  const currentFolderId = place !== 'all' && place !== 'unsorted' ? place : null
  const placeName =
    place === 'all' ? t('notes.library.all') : place === 'unsorted' ? t('notes.library.unsorted') : folderPath(folders, place).map((f) => f.name).join(' / ')
  // The item on the drawer, kept current as the list refreshes after a rename or move.
  const shown = selected && (items?.find((i) => i.id === selected.id) ?? selected)

  function goTo(next: LibraryPlace) {
    setSearch('')
    setSearchParams(next === 'all' ? {} : { folder: next }, { replace: true })
  }

  async function run(action: () => Promise<unknown>) {
    try {
      await action()
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  // Into the folder on screen; "everything" and "unsorted" both mean no folder.
  async function uploadFiles(files: File[]) {
    if (!files.length) return
    const key = 'library-upload'
    let done = 0
    for (const file of files) {
      message.open({ key, type: 'loading', content: t('notes.library.uploading', { done, total: files.length }), duration: 0 })
      try {
        await upload.mutateAsync({ file, folderId: currentFolderId })
        done++
      } catch (err) {
        message.error(`${file.name}: ${toErrorMessage(err, t('notes.uploadError'))}`)
      }
    }
    if (done) message.open({ key, type: 'success', content: t('notes.library.uploaded', { count: done }) })
    else message.destroy(key)
  }

  function openDialog(next: FolderDialog) {
    setDialog(next)
    setDialogValue(next.mode === 'rename' ? next.folder.name : next.mode === 'move' ? toFolderOption(next.folder.parentId) : '')
  }

  async function submitDialog() {
    if (!dialog) return
    const value = dialogValue.trim()
    if (dialog.mode !== 'move' && !value) return
    await run(async () => {
      if (dialog.mode === 'create') {
        const created = await createFolder.mutateAsync({ name: value, parentId: dialog.parentId })
        goTo(created.id)
      } else if (dialog.mode === 'rename') {
        await renameFolder.mutateAsync({ id: dialog.folder.id, name: value })
      } else {
        await moveFolder.mutateAsync({ id: dialog.folder.id, parentId: fromFolderOption(dialogValue) })
      }
      setDialog(null)
    })
  }

  function confirmDeleteFolder(folder: MediaFolderDto) {
    const parent = folders.find((f) => f.id === folder.parentId)
    modal.confirm({
      title: t('notes.library.deleteFolderConfirm', { name: folder.name }),
      content: t('notes.library.deleteFolderHint', { place: parent?.name ?? t('notes.library.unsorted') }),
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () =>
        run(async () => {
          await deleteFolder.mutateAsync(folder.id)
          if (place === folder.id) goTo(folder.parentId ?? 'all')
        }),
    })
  }

  // Files from the computer can be dropped anywhere on the grid; items dragged inside the page cannot.
  const fileDrop = {
    onDragOver: (e: DragEvent) => {
      if (!e.dataTransfer.types.includes('Files')) return
      e.preventDefault()
      setDraggingFiles(true)
    },
    onDragLeave: (e: DragEvent) => {
      if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setDraggingFiles(false)
    },
    onDrop: (e: DragEvent) => {
      if (!e.dataTransfer.files.length) return
      e.preventDefault()
      setDraggingFiles(false)
      void uploadFiles([...e.dataTransfer.files])
    },
  }

  const border = `1px solid ${token.colorBorderSecondary}`

  return (
    <div className="mx-auto flex max-w-7xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('notes.library.title')} description={t('notes.library.subtitle')} />
        <div className="flex gap-2">
          <Button icon={<FolderAddOutlined />} onClick={() => openDialog({ mode: 'create', parentId: currentFolderId })}>
            {t('notes.library.newFolder')}
          </Button>
          <Button type="primary" icon={<CloudUploadOutlined />} onClick={() => fileInput.current?.click()}>
            {t('notes.library.upload')}
          </Button>
          <input
            ref={fileInput}
            type="file"
            multiple
            hidden
            aria-label={t('notes.library.upload')}
            onChange={(e) => {
              void uploadFiles([...(e.target.files ?? [])])
              e.target.value = ''
            }}
          />
        </div>
      </div>

      <div className="grid grid-cols-[minmax(0,1fr)] items-start gap-6 md:grid-cols-[240px_minmax(0,1fr)]">
        <aside className="rounded-xl p-2" style={{ border, background: token.colorBgContainer }}>
          <MediaFolderPanel
            data={folderData}
            place={place}
            onPlaceChange={goTo}
            onDropItem={(id, folderId) => run(() => moveItem.mutateAsync({ id, folderId }))}
            onCreateFolder={(parentId) => openDialog({ mode: 'create', parentId })}
            onRenameFolder={(folder) => openDialog({ mode: 'rename', folder })}
            onMoveFolder={(folder) => openDialog({ mode: 'move', folder })}
            onDeleteFolder={confirmDeleteFolder}
          />
        </aside>

        <section className="relative flex min-w-0 flex-col gap-4" {...fileDrop}>
          <div className="flex flex-wrap items-center gap-3">
            <Typography.Title level={5} className="!mb-0 min-w-0 flex-1 truncate">
              {searching ? t('notes.library.searchResults') : placeName}
              {items && (
                <Typography.Text type="secondary" className="ms-2 text-sm font-normal">
                  {items.length}
                </Typography.Text>
              )}
            </Typography.Title>
            <Input
              allowClear
              prefix={<SearchOutlined style={{ color: token.colorTextTertiary }} />}
              placeholder={t('notes.library.search')}
              aria-label={t('notes.library.search')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="!w-56"
            />
            <Segmented<KindFilter>
              value={kind}
              onChange={setKind}
              options={[
                { value: 'all', label: t('notes.library.kindAll') },
                { value: 'image', label: t('notes.library.kindImages') },
                { value: 'file', label: t('notes.library.kindFiles') },
              ]}
            />
          </div>

          {isLoading ? (
            <Skeleton active />
          ) : !items?.length ? (
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              className="rounded-xl py-12"
              style={{ border: `1px dashed ${token.colorBorder}` }}
              description={searching ? t('notes.library.searchEmpty') : t('notes.library.empty')}
            />
          ) : (
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
              {items.map((item) => (
                <MediaCard key={item.id} item={item} selected={shown?.id === item.id} onOpen={() => setSelected(item)} />
              ))}
            </div>
          )}

          {draggingFiles && (
            <div
              className="pointer-events-none absolute inset-0 z-10 flex items-center justify-center rounded-xl"
              style={{ background: token.colorPrimaryBg, border: `2px dashed ${token.colorPrimary}`, opacity: 0.95 }}
            >
              <Typography.Text strong style={{ color: token.colorPrimary }}>
                <CloudUploadOutlined /> {t('notes.library.dropHere', { place: currentFolderId ? placeName : t('notes.library.unsorted') })}
              </Typography.Text>
            </div>
          )}
        </section>
      </div>

      <MediaDetailDrawer item={shown} folders={folders} onClose={() => setSelected(null)} />

      <Modal
        open={!!dialog}
        title={
          dialog?.mode === 'create'
            ? t('notes.library.newFolder')
            : dialog?.mode === 'rename'
              ? t('notes.library.renameFolder')
              : t('notes.library.moveFolder', { name: dialog?.folder.name })
        }
        okText={t('common.save')}
        cancelText={t('common.cancel')}
        onOk={submitDialog}
        onCancel={() => setDialog(null)}
        destroyOnHidden
      >
        {dialog?.mode === 'move' ? (
          <TreeSelect
            className="w-full"
            value={dialogValue}
            onChange={setDialogValue}
            treeDefaultExpandAll
            aria-label={t('notes.library.folder')}
            // "Unsorted" here means the top: a folder that sits in no other folder.
            treeData={folderOptions(folders, t('notes.library.top'), folderAndDescendants(folders, dialog.folder.id))}
          />
        ) : (
          <Input
            autoFocus
            maxLength={100}
            value={dialogValue}
            aria-label={t('notes.library.name')}
            onChange={(e) => setDialogValue(e.target.value)}
            onPressEnter={submitDialog}
          />
        )}
      </Modal>
    </div>
  )
}
