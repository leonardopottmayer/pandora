import { useTranslation } from 'react-i18next'
import { Button, Checkbox, Empty, Spin, theme } from 'antd'
import { PaperClipOutlined, StarFilled } from '@ant-design/icons'
import { addressLabel, shortDate, type MessageSummary } from '../models'
import { usePreferences } from '@/modules/identity/context/preferences-context'

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
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const { token } = theme.useToken()

  if (loading) return <Spin className="m-6" />
  if (messages.length === 0)
    return <Empty className="mt-10" image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('mail.noMessages')} />

  // Once something is checked, every row shows its box; before that, the sender's initial, and the box on hover.
  const selecting = checkedIds.size > 0

  function toggle(id: string) {
    const next = new Set(checkedIds)
    if (!next.delete(id)) next.add(id)
    onCheckedChange(next)
  }

  return (
    <div className="flex flex-col">
      {messages.map((m) => {
        const sender = addressLabel(m.from) || t('mail.unknownSender')
        const active = m.id === selectedId
        return (
          <div
            key={m.id}
            className="group flex items-start gap-3 px-3 py-2.5"
            style={{
              background: active ? token.controlItemBgActive : checkedIds.has(m.id) ? token.colorFillQuaternary : undefined,
              borderBottom: `1px solid ${token.colorBorderSecondary}`,
            }}
          >
            <span className="relative mt-0.5 flex size-8 shrink-0 items-center justify-center">
              <span
                aria-hidden
                className={`flex size-8 items-center justify-center rounded-full text-sm font-semibold uppercase transition-opacity ${
                  selecting ? 'opacity-0' : 'group-hover:opacity-0'
                }`}
                style={{ background: token.colorFillSecondary, color: token.colorTextSecondary }}
              >
                {sender.trim().charAt(0)}
              </span>
              <Checkbox
                className={`absolute transition-opacity ${selecting ? '' : 'opacity-0 focus-within:opacity-100 group-hover:opacity-100'}`}
                aria-label={t('mail.actions.select', { subject: m.subject || t('mail.noSubject') })}
                checked={checkedIds.has(m.id)}
                onChange={() => toggle(m.id)}
              />
            </span>
            <button
              type="button"
              onClick={() => onSelect(m)}
              className="flex w-full min-w-0 cursor-pointer flex-col gap-0.5 border-0 bg-transparent p-0 text-left"
              style={{ color: token.colorText }}
            >
              <span className="flex items-center justify-between gap-2">
                <span className="flex min-w-0 items-center gap-1.5">
                  {m.isUnread && (
                    <span
                      role="img"
                      aria-label={t('mail.unread')}
                      className="size-2 shrink-0 rounded-full"
                      style={{ background: token.colorPrimary }}
                    />
                  )}
                  <span className="truncate" style={{ fontWeight: m.isUnread ? 600 : 400 }}>
                    {sender}
                  </span>
                </span>
                <span
                  className="flex shrink-0 items-center gap-1.5 text-xs tabular-nums"
                  style={{ color: m.isUnread ? token.colorText : token.colorTextTertiary }}
                >
                  {m.hasAttachments && <PaperClipOutlined />}
                  {m.isStarred && <StarFilled style={{ color: token.colorWarning }} />}
                  {shortDate(m.sentAt, timeZone)}
                </span>
              </span>
              <span className="truncate text-sm" style={{ fontWeight: m.isUnread ? 600 : 400 }}>
                {m.subject || t('mail.noSubject')}
              </span>
              <span className="truncate text-xs" style={{ color: token.colorTextTertiary }}>
                {m.snippet}
              </span>
            </button>
          </div>
        )
      })}
      {hasMore && (
        <Button type="link" className="my-2" loading={loadingMore} onClick={onLoadMore}>
          {t('mail.loadMore')}
        </Button>
      )}
    </div>
  )
}
