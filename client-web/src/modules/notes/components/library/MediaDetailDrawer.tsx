import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Drawer, Grid, Input, TreeSelect, Typography, theme } from 'antd'
import { CopyOutlined, DeleteOutlined, DownloadOutlined } from '@ant-design/icons'
import { formatBytes } from '@/lib/formatBytes'
import { toErrorMessage } from '@/lib/api/envelope'
import type { MediaFolderDto, MediaItemDto } from '../../models'
import { useDeleteMedia, useMoveMedia, useRenameMedia } from '../../hooks/useMedia'
import { embedMarkdown } from '../../lib/media'
import { folderOptions, fromFolderOption, toFolderOption } from '../../lib/mediaFolders'
import { downloadMedia } from '../../services/media.service'
import { MediaPicture } from './MediaCard'

interface MediaDetailDrawerProps {
  item: MediaItemDto | null
  folders: MediaFolderDto[]
  onClose: () => void
}

/** One item up close: the picture, what it is, and what can be done with it. */
export function MediaDetailDrawer({ item, folders, onClose }: MediaDetailDrawerProps) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const isMobile = Grid.useBreakpoint().md === false
  const rename = useRenameMedia()
  const move = useMoveMedia()
  const remove = useDeleteMedia()
  const [name, setName] = useState('')
  const [shownId, setShownId] = useState<string | null>(null)

  // A different item opened: start its name field from its name (render-phase adjustment).
  if (item && item.id !== shownId) {
    setShownId(item.id)
    setName(item.name)
  }

  async function run(action: () => Promise<unknown>) {
    try {
      await action()
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function saveName() {
    const trimmed = name.trim()
    if (!item || !trimmed || trimmed === item.name) return setName(item?.name ?? '')
    void run(() => rename.mutateAsync({ id: item.id, name: trimmed }))
  }

  function confirmDelete() {
    if (!item) return
    modal.confirm({
      title: t('notes.library.deleteConfirm'),
      content: t('notes.library.deleteHint'),
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => run(async () => {
        await remove.mutateAsync(item.id)
        onClose()
      }),
    })
  }

  const row = (label: string, value: string | null) =>
    value && (
      <div className="flex justify-between gap-4 py-1.5 text-sm" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>
        <Typography.Text type="secondary">{label}</Typography.Text>
        <Typography.Text className="truncate text-right">{value}</Typography.Text>
      </div>
    )

  return (
    <Drawer
      open={!!item}
      onClose={onClose}
      title={t('notes.library.details')}
      placement={isMobile ? 'bottom' : 'right'}
      size={isMobile ? '85%' : 420}
    >
      {item && (
        <div className="flex flex-col gap-4">
          <div
            className="flex h-64 items-center justify-center overflow-hidden rounded-lg"
            style={{ background: token.colorFillQuaternary }}
          >
            <MediaPicture item={item} original />
          </div>

          <Input
            value={name}
            maxLength={255}
            aria-label={t('notes.library.name')}
            onChange={(e) => setName(e.target.value)}
            onBlur={saveName}
            onPressEnter={(e) => (e.target as HTMLInputElement).blur()}
          />

          <div className="flex flex-col gap-1.5">
            <Typography.Text type="secondary" className="text-xs">
              {t('notes.library.folder')}
            </Typography.Text>
            <TreeSelect
              value={toFolderOption(item.folderId)}
              treeData={folderOptions(folders, t('notes.library.unsorted'))}
              treeDefaultExpandAll
              aria-label={t('notes.library.folder')}
              onChange={(value: string) => run(() => move.mutateAsync({ id: item.id, folderId: fromFolderOption(value) }))}
            />
          </div>

          <div className="flex flex-wrap gap-2">
            <Button icon={<DownloadOutlined />} onClick={() => run(() => downloadMedia(item.url, item.originalFileName))}>
              {t('notes.library.download')}
            </Button>
            <Button
              icon={<CopyOutlined />}
              onClick={() =>
                run(async () => {
                  await navigator.clipboard.writeText(embedMarkdown(item))
                  message.success(t('notes.library.copied'))
                })
              }
            >
              {t('notes.library.copyMarkdown')}
            </Button>
            <Button danger icon={<DeleteOutlined />} onClick={confirmDelete}>
              {t('common.delete')}
            </Button>
          </div>

          <div className="flex flex-col">
            {row(t('notes.library.kind'), t(`notes.library.kinds.${item.kind}`))}
            {row(t('notes.library.size'), formatBytes(item.sizeBytes))}
            {row(t('notes.library.dimensions'), item.width && item.height ? `${item.width} × ${item.height}` : null)}
            {row(t('notes.library.originalName'), item.originalFileName)}
            {row(t('notes.library.uploadedAt'), new Date(item.createdAt).toLocaleString(i18n.language))}
          </div>
        </div>
      )}
    </Drawer>
  )
}
