import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Typography } from 'antd'
import { LeftOutlined } from '@ant-design/icons'
import type { SpaceNode } from '../lib/spaceTree'
import type { SpaceDto } from '../models'
import { useNodeText } from '../hooks/useNodeText'
import { NodeIcon } from './SpaceItems'
import { SpaceIcon } from './SpaceIcon'

interface PageBreadcrumbProps {
  space: SpaceDto | undefined
  /** The folders and pages above the open one, from the top. */
  path: SpaceNode[]
  /** The open page or folder, or null on the space itself. */
  current: { icon: string | null; name: string } | null
  /** A phone shows only the way back up, not the whole path. */
  compact: boolean
  onOpenSpace: () => void
  onOpenNode: (node: SpaceNode) => void
}

/** Where the open page or folder sits: its space, then each folder and page above it. */
export function PageBreadcrumb({ space, path, current, compact, onOpenSpace, onOpenNode }: PageBreadcrumbProps) {
  const { t } = useTranslation()
  const text = useNodeText()

  if (compact) {
    const parent = path.at(-1)
    if (!current) return null
    return (
      <Button
        type="text"
        size="small"
        icon={<LeftOutlined />}
        className="min-w-0 max-w-full"
        onClick={() => (parent ? onOpenNode(parent) : onOpenSpace())}
      >
        <span className="truncate">{parent ? text.name(parent) : space?.name}</span>
      </Button>
    )
  }

  const crumb = (key: string, content: ReactNode, onClick?: () => void) => (
    <li key={key} className="flex min-w-0 items-center gap-1">
      {onClick ? (
        <button
          type="button"
          className="flex min-w-0 cursor-pointer items-center gap-1.5 rounded px-1.5 py-0.5 hover:bg-[var(--ant-color-fill-tertiary)]"
          style={{ border: 0, background: 'transparent' }}
          onClick={onClick}
        >
          {content}
        </button>
      ) : (
        <span className="flex min-w-0 items-center gap-1.5 px-1.5 py-0.5">{content}</span>
      )}
    </li>
  )

  const separator = (key: string) => (
    <li key={key} aria-hidden>
      <Typography.Text type="secondary">/</Typography.Text>
    </li>
  )

  const items: ReactNode[] = []
  if (space) {
    items.push(
      crumb(
        'space',
        <>
          <SpaceIcon space={space} size={18} />
          <span className="truncate">{space.name}</span>
        </>,
        current ? onOpenSpace : undefined,
      ),
    )
  }
  for (const node of path) {
    items.push(separator(`sep-${node.id}`))
    items.push(
      crumb(
        node.id,
        <>
          <NodeIcon node={node} />
          <span className="truncate">{text.name(node)}</span>
        </>,
        () => onOpenNode(node),
      ),
    )
  }
  if (current) {
    if (items.length) items.push(separator('sep-current'))
    items.push(
      crumb(
        'current',
        <>
          {current.icon && <span>{current.icon}</span>}
          <Typography.Text strong className="truncate">
            {current.name || t('notes.untitled')}
          </Typography.Text>
        </>,
      ),
    )
  }

  return (
    <nav aria-label={t('notes.breadcrumb')} className="min-w-0">
      <ol className="m-0 flex min-w-0 list-none items-center gap-0.5 overflow-hidden p-0 text-sm">{items}</ol>
    </nav>
  )
}
