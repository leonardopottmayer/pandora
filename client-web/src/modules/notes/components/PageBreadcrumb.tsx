import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Typography } from 'antd'
import { LeftOutlined } from '@ant-design/icons'
import type { PageSummaryDto, SpaceDto } from '../models'
import { SpaceIcon } from './SpaceIcon'

interface PageBreadcrumbProps {
  space: SpaceDto | undefined
  /** The open page, or null on the space itself. */
  page: Pick<PageSummaryDto, 'id' | 'parentId' | 'title' | 'icon'> | null
  /** Every page, to walk up from the open one. */
  pages: PageSummaryDto[]
  /** A phone shows only the way back up, not the whole path. */
  compact: boolean
  onOpenSpace: () => void
  onOpenPage: (id: string) => void
}

/** Where the open page sits: its space, then each page above it. */
export function PageBreadcrumb({ space, page, pages, compact, onOpenSpace, onOpenPage }: PageBreadcrumbProps) {
  const { t } = useTranslation()

  const byId = new Map(pages.map((p) => [p.id, p]))
  const ancestors: PageSummaryDto[] = []
  // A missing or cyclic parent ends the walk; the backend rejects cycles, this only guards the loop.
  for (let id = page?.parentId; id && ancestors.length < 20; id = byId.get(id)?.parentId) {
    const parent = byId.get(id)
    if (!parent) break
    ancestors.unshift(parent)
  }

  if (compact) {
    const parent = ancestors[ancestors.length - 1]
    if (!page) return null
    return (
      <Button
        type="text"
        size="small"
        icon={<LeftOutlined />}
        className="min-w-0 max-w-full"
        onClick={() => (parent ? onOpenPage(parent.id) : onOpenSpace())}
      >
        <span className="truncate">{parent ? parent.title || t('notes.untitled') : space?.name}</span>
      </Button>
    )
  }

  const crumb = (key: string, content: ReactNode, onClick?: () => void) => (
    <li key={key} className="flex min-w-0 items-center gap-1">
      {onClick ? (
        <button
          type="button"
          className="flex min-w-0 cursor-pointer items-center gap-1.5 rounded px-1.5 py-0.5 hover:bg-[var(--ant-color-fill-tertiary)]"
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
        page ? onOpenSpace : undefined,
      ),
    )
  }
  for (const ancestor of ancestors) {
    items.push(separator(`sep-${ancestor.id}`))
    items.push(
      crumb(
        ancestor.id,
        <>
          {ancestor.icon && <span>{ancestor.icon}</span>}
          <span className="truncate">{ancestor.title || t('notes.untitled')}</span>
        </>,
        () => onOpenPage(ancestor.id),
      ),
    )
  }
  if (page) {
    if (items.length) items.push(separator('sep-current'))
    items.push(
      crumb(
        'current',
        <>
          {page.icon && <span>{page.icon}</span>}
          <Typography.Text strong className="truncate">
            {page.title || t('notes.untitled')}
          </Typography.Text>
        </>,
      ),
    )
  }

  return (
    <nav aria-label={t('notes.breadcrumb')} className="min-w-0">
      <ol className="flex min-w-0 items-center gap-0.5 overflow-hidden text-sm">{items}</ol>
    </nav>
  )
}
