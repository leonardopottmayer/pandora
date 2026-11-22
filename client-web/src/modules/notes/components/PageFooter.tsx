import { useTranslation } from 'react-i18next'
import { Typography, theme } from 'antd'
import { FileTextOutlined } from '@ant-design/icons'
import { useBacklinks } from '../hooks/usePages'
import { timeAgo } from '../lib/timeAgo'
import type { PageSummaryDto } from '../models'

interface PageFooterProps {
  pageId: string
  pages: PageSummaryDto[]
  onOpenPage: (id: string) => void
}

/** Under the page: its sub-pages, and the pages that link to it ("Mentioned in"). */
export function PageFooter({ pageId, pages, onOpenPage }: PageFooterProps) {
  const { t, i18n } = useTranslation()
  const { token } = theme.useToken()
  const { data: backlinks = [] } = useBacklinks(pageId)
  const children = pages.filter((page) => page.parentId === pageId && !page.isArchived).sort((a, b) => a.orderIndex - b.orderIndex)
  // A page linking twice (mention and card) is one entry here.
  const mentions = [...new Map(backlinks.map((link) => [link.pageId, link])).values()]

  const section = (title: string, rows: { id: string; icon: string | null; title: string; hint?: string }[]) => (
    <section className="flex flex-col gap-1.5">
      <Typography.Text type="secondary" className="text-xs font-semibold uppercase tracking-wider">
        {title} · {rows.length}
      </Typography.Text>
      <div className="flex flex-col">
        {rows.map((row) => (
          <button
            key={row.id}
            type="button"
            className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-left hover:bg-[var(--ant-color-fill-tertiary)]"
            style={{ border: 0, background: 'transparent' }}
            onClick={() => onOpenPage(row.id)}
          >
            <span className="w-5 shrink-0 text-center">{row.icon ?? <FileTextOutlined />}</span>
            <Typography.Text className="flex-1 truncate">{row.title || t('notes.untitled')}</Typography.Text>
            {row.hint && <Typography.Text type="secondary" className="shrink-0 text-xs">{row.hint}</Typography.Text>}
          </button>
        ))}
      </div>
    </section>
  )

  if (children.length === 0 && mentions.length === 0) return null

  return (
    <div className="mt-10 flex flex-col gap-6 pt-6" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>
      {children.length > 0 &&
        section(
          t('notes.subpages'),
          children.map((page) => ({ id: page.id, icon: page.icon, title: page.title, hint: timeAgo(page.updatedAt ?? page.createdAt, i18n.language) })),
        )}
      {mentions.length > 0 &&
        section(t('notes.mentionedIn'), mentions.map((link) => ({ id: link.pageId, icon: link.icon, title: link.title })))}
    </div>
  )
}
