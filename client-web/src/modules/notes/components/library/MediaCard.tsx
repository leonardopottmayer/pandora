import { theme, Typography } from 'antd'
import {
  FileExcelOutlined,
  FileImageOutlined,
  FileOutlined,
  FilePdfOutlined,
  FileTextOutlined,
  FileWordOutlined,
  FileZipOutlined,
} from '@ant-design/icons'
import { formatBytes } from '@/lib/formatBytes'
import type { MediaItemDto } from '../../models'
import { useMediaBlobUrl } from '../../hooks/useMedia'
import { MEDIA_DRAG_TYPE } from '../../lib/media'

/** A file's icon by what it is, for items that have no picture to show. */
export function FileIcon({ contentType, className }: { contentType: string; className?: string }) {
  const type = contentType.toLowerCase()
  const Icon = type.includes('pdf')
    ? FilePdfOutlined
    : type.includes('zip') || type.includes('compressed')
      ? FileZipOutlined
      : type.includes('word') || type.includes('document')
        ? FileWordOutlined
        : type.includes('sheet') || type.includes('excel') || type.includes('csv')
          ? FileExcelOutlined
          : type.startsWith('text/')
            ? FileTextOutlined
            : type.startsWith('image/')
              ? FileImageOutlined
              : FileOutlined
  return <Icon className={className} />
}

/** The picture that stands for an item: its thumbnail, the image itself when small, or an icon. */
export function MediaPicture({ item, original = false }: { item: MediaItemDto; original?: boolean }) {
  const { token } = theme.useToken()
  const path = item.kind !== 'image' ? null : original ? item.url : (item.thumbnailUrl ?? item.url)
  const { data: src } = useMediaBlobUrl(path)

  if (path) {
    return src ? (
      <img src={src} alt={item.name} className={original ? 'max-h-full max-w-full object-contain' : 'h-full w-full object-cover'} />
    ) : (
      <span className="h-full w-full animate-pulse" style={{ background: token.colorFillTertiary }} />
    )
  }
  return <FileIcon contentType={item.contentType} className="text-4xl opacity-60" />
}

interface MediaCardProps {
  item: MediaItemDto
  selected: boolean
  onOpen: () => void
}

/** One item of the grid. Drag it onto a folder to move it there. */
export function MediaCard({ item, selected, onOpen }: MediaCardProps) {
  const { token } = theme.useToken()
  const size = [formatBytes(item.sizeBytes), item.width && item.height ? `${item.width}×${item.height}` : null]
    .filter(Boolean)
    .join(' · ')

  return (
    <button
      type="button"
      draggable
      onDragStart={(e) => {
        e.dataTransfer.setData(MEDIA_DRAG_TYPE, item.id)
        e.dataTransfer.effectAllowed = 'move'
      }}
      onClick={onOpen}
      aria-label={item.name}
      className="flex min-w-0 cursor-pointer flex-col overflow-hidden rounded-lg text-left transition-shadow hover:shadow-md"
      style={{
        border: `1px solid ${selected ? token.colorPrimary : token.colorBorderSecondary}`,
        outline: selected ? `1px solid ${token.colorPrimary}` : undefined,
        background: token.colorBgContainer,
      }}
    >
      <span
        className="flex aspect-[4/3] w-full items-center justify-center overflow-hidden"
        style={{ background: token.colorFillQuaternary }}
      >
        <MediaPicture item={item} />
      </span>
      <span className="flex min-w-0 flex-col px-2.5 py-2">
        <Typography.Text className="truncate text-sm">{item.name}</Typography.Text>
        <Typography.Text type="secondary" className="truncate text-xs tabular-nums">
          {size}
        </Typography.Text>
      </span>
    </button>
  )
}
