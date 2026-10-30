import { useTranslation } from 'react-i18next'
import { Button, Empty, Spin, Typography, theme } from 'antd'
import { PaperClipOutlined, StarFilled } from '@ant-design/icons'
import { addressLabel, shortDate, type MessageSummary } from '../models'

interface MessageListProps {
  messages: MessageSummary[]
  selectedId: string | null
  onSelect: (message: MessageSummary) => void
  loading: boolean
  hasMore: boolean
  loadingMore: boolean
  onLoadMore: () => void
}

// ponytail: a plain list, pages of 50 behind "load more"; virtualize if a long scroll gets slow.
export function MessageList({ messages, selectedId, onSelect, loading, hasMore, loadingMore, onLoadMore }: MessageListProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()

  if (loading) return <Spin className="m-6" />
  if (messages.length === 0) return <Empty className="mt-10" description={t('mail.noMessages')} />

  return (
    <div className="flex flex-col">
      {messages.map((m) => (
        <button
          key={m.id}
          type="button"
          onClick={() => onSelect(m)}
          className="flex w-full cursor-pointer flex-col gap-0.5 border-0 px-3 py-2 text-left"
          style={{
            background: m.id === selectedId ? token.controlItemBgActive : 'transparent',
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
            color: token.colorText,
            fontWeight: m.isUnread ? 600 : 400,
          }}
        >
          <span className="flex items-center justify-between gap-2">
            <span className="truncate">{addressLabel(m.from) || t('mail.unknownSender')}</span>
            <span className="flex shrink-0 items-center gap-1 text-xs" style={{ color: token.colorTextSecondary }}>
              {m.isStarred && <StarFilled style={{ color: token.colorWarning }} />}
              {m.hasAttachments && <PaperClipOutlined />}
              {shortDate(m.sentAt)}
            </span>
          </span>
          <span className="truncate text-sm">{m.subject || t('mail.noSubject')}</span>
          <Typography.Text type="secondary" className="truncate text-xs !font-normal">
            {m.snippet}
          </Typography.Text>
        </button>
      ))}
      {hasMore && (
        <Button type="link" className="my-2" loading={loadingMore} onClick={onLoadMore}>
          {t('mail.loadMore')}
        </Button>
      )}
    </div>
  )
}
