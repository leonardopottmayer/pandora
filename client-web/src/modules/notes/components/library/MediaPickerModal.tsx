import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Empty, Input, Modal, Skeleton, theme } from 'antd'
import { CloudUploadOutlined, SearchOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { MediaItemDto } from '../../models'
import { useMediaFolders, useMediaList, useUploadMedia } from '../../hooks/useMedia'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { MediaCard } from './MediaCard'
import { MediaFolderPanel, type LibraryPlace } from './MediaFolderPanel'

interface MediaPickerModalProps {
  open: boolean
  /** `image` or `file` narrows the grid to that kind; null shows everything. */
  kind: 'image' | 'file' | null
  onPick: (item: MediaItemDto) => void
  onClose: () => void
}

/**
 * Picks an item of the library from inside the editor — or uploads one first, into the folder on
 * screen, and picks it straight away.
 */
export function MediaPickerModal({ open, kind, onPick, onClose }: MediaPickerModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const fileInput = useRef<HTMLInputElement>(null)
  const [place, setPlace] = useState<LibraryPlace>('all')
  const [search, setSearch] = useState('')
  const q = useDebouncedValue(search)
  const searching = q.trim().length > 0
  const folderId = !searching && place !== 'all' && place !== 'unsorted' ? place : null

  const { data: folders, error: folderError } = useMediaFolders()
  const { data: items, isLoading, error: mediaError } = useMediaList({
    folderId,
    unsorted: !searching && place === 'unsorted',
    kind,
    q,
  })
  const loadError = mediaError ?? folderError
  const upload = useUploadMedia()

  async function uploadAndPick(file: File) {
    try {
      onPick(await upload.mutateAsync({ file, folderId }))
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.uploadError')))
    }
  }

  return (
    <Modal
      open={open}
      onCancel={onClose}
      footer={null}
      width={960}
      title={t(kind === 'image' ? 'notes.media.pickImage' : kind === 'file' ? 'notes.media.pickFile' : 'notes.media.pick')}
      destroyOnHidden
    >
      <div className="grid grid-cols-[minmax(0,1fr)] gap-4 md:grid-cols-[200px_minmax(0,1fr)]">
        <div className="hidden md:block">
          <MediaFolderPanel data={folders} place={place} onPlaceChange={setPlace} />
        </div>

        <div className="flex min-w-0 flex-col gap-3">
          <div className="flex gap-2">
            <Input
              allowClear
              autoFocus
              prefix={<SearchOutlined style={{ color: token.colorTextTertiary }} />}
              placeholder={t('notes.library.search')}
              aria-label={t('notes.library.search')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <Button icon={<CloudUploadOutlined />} loading={upload.isPending} onClick={() => fileInput.current?.click()}>
              {t('notes.media.uploadAndInsert')}
            </Button>
            <input
              ref={fileInput}
              type="file"
              hidden
              aria-label={t('notes.media.uploadAndInsert')}
              accept={kind === 'image' ? 'image/*' : undefined}
              onChange={(e) => {
                const file = e.target.files?.[0]
                e.target.value = ''
                if (file) void uploadAndPick(file)
              }}
            />
          </div>

          <div className="max-h-[60vh] overflow-auto">
            {loadError ? (
              <Alert type="error" showIcon title={loadError.message} />
            ) : isLoading ? (
              <Skeleton active />
            ) : !items?.length ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t(searching ? 'notes.library.searchEmpty' : 'notes.library.empty')} />
            ) : (
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
                {items.map((item) => (
                  <MediaCard key={item.id} item={item} selected={false} onOpen={() => onPick(item)} />
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </Modal>
  )
}
