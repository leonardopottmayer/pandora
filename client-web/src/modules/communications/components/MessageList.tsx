import { useTranslation } from 'react-i18next'
import { Button, Checkbox, Empty, Spin, Typography, theme } from 'antd'
import { PaperClipOutlined, StarFilled } from '@ant-design/icons'
import { addressLabel, shortDate, type MessageSummary } from '../models'

interface MessageListProps {
  messages: MessageSummary[]
  selectedId: string | null
  onSelect: (message: MessageSummary) => void
  checkedIds: ReadonlySet<string>
  onCheckedChange: (ids: Set<string>) => void
  loading: boolean
  hasMore: boolean
  loadingMore: boolean
  onLoadMore: () => void
}

// ponytail: a plain list, pages of 50 behind "load more"; virtualize if a long scroll gets slow.
export function MessageList({
  messages,
  selectedId,
  onSelect,
  checkedIds,
  onCheckedChange,
  loading,
  hasMore,
  loadingMore,
  onLoadMore,
}: MessageListProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()

  if (loading) return <Spin className="m-6" />
  if (messages.length === 0) return <Empty className="mt-10" description={t('mail.noMessages')} />

  const allChecked = messages.every((m) => checkedIds.has(m.id))

  function toggle(id: string) {
    const next = new Set(checkedIds)
    if (!next.delete(id)) next.add(id)
    onCheckedChange(next)
  }

  return (
    <div className="flex flex-col">
      <div className="px-3 py-1" style={{ borderBottom: `1px solid ${token.colorBorderSecondary}` }}>
        <Checkbox
          checked={allChecked}
          indeterminate={!allChecked && checkedIds.size > 0}
          onChange={() => onCheckedChange(new Set(allChecked ? [] : messages.map((m) => m.id)))}
        >
          <span className="text-xs">{t('mail.actions.selectPage')}</span>
        </Checkbox>
      </div>
      {messages.map((m) => (
        <div
          key={m.id}
          className="flex items-start gap-2 pl-3"
          style={{
            background: m.id === selectedId ? token.controlItemBgActive : 'transparent',
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
          }}
        >
          <Checkbox
            className="mt-2"
            aria-label={t('mail.actions.select', { subject: m.subject || t('mail.noSubject') })}
            checked={checkedIds.has(m.id)}
            onChange={() => toggle(m.id)}
          />
          <button
            type="button"
            onClick={() => onSelect(m)}
            className="flex w-full min-w-0 cursor-pointer flex-col gap-0.5 border-0 bg-transparent py-2 pr-3 text-left"
            style={{ color: token.colorText, fontWeight: m.isUnread ? 600 : 400 }}
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
        </div>
      ))}
      {hasMore && (
        <Button type="link" className="my-2" loading={loadingMore} onClick={onLoadMore}>
          {t('mail.loadMore')}
        </Button>
      )}
    </div>
  )
}
