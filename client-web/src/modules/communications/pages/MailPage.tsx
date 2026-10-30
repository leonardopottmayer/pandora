import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Button, Card, Empty, Flex, Input, Segmented, Spin } from 'antd'
import { SettingOutlined } from '@ant-design/icons'
import type { MessageSummary } from '../models'
import { useMailboxes, useMessages } from '../hooks/useMail'
import { MailSidebar, type MailFolder } from '../components/MailSidebar'
import { MessageList } from '../components/MessageList'
import { MessageReader } from '../components/MessageReader'

export function MailPage() {
  const { t } = useTranslation()
  const { data: mailboxes, isLoading } = useMailboxes()

  const [folder, setFolder] = useState<MailFolder>({})
  const [unreadOnly, setUnreadOnly] = useState(false)
  const [q, setQ] = useState('')
  const [selected, setSelected] = useState<MessageSummary | null>(null)

  const messages = useMessages({ ...folder, unread: unreadOnly || undefined, q: q || undefined })
  const items = messages.data?.pages.flatMap((p) => p.items) ?? []

  if (isLoading) return <Spin />
  if (!mailboxes?.length)
    return (
      <Empty className="mt-16" description={t('mail.noMailboxes')}>
        <Link to="/mail/accounts">
          <Button type="primary">{t('mail.accounts.add')}</Button>
        </Link>
      </Empty>
    )

  return (
    <Flex gap={16} align="stretch" style={{ height: 'calc(100vh - 112px)' }}>
      <Card
        styles={{ body: { padding: 8, height: '100%', overflow: 'auto', display: 'flex', flexDirection: 'column' } }}
        style={{ width: 240, flexShrink: 0, height: '100%' }}
      >
        <div className="flex-1">
          <MailSidebar
            mailboxes={mailboxes}
            folder={folder}
            onSelect={(next) => {
              setFolder(next)
              setSelected(null)
            }}
          />
        </div>
        <Link to="/mail/accounts" className="px-3 py-2 text-sm">
          <SettingOutlined /> {t('mail.manageAccounts')}
        </Link>
      </Card>

      <Card
        styles={{ body: { padding: 0, height: '100%', display: 'flex', flexDirection: 'column' } }}
        style={{ width: 380, flexShrink: 0, height: '100%', overflow: 'hidden' }}
      >
        <div className="flex flex-col gap-2 p-3">
          <Input.Search allowClear placeholder={t('mail.search')} onSearch={(value) => setQ(value.trim())} />
          <Segmented<'all' | 'unread'>
            size="small"
            value={unreadOnly ? 'unread' : 'all'}
            onChange={(value) => setUnreadOnly(value === 'unread')}
            options={[
              { value: 'all', label: t('mail.filterAll') },
              { value: 'unread', label: t('mail.filterUnread') },
            ]}
          />
        </div>
        <div className="flex-1 overflow-auto">
          <MessageList
            messages={items}
            selectedId={selected?.id ?? null}
            onSelect={setSelected}
            loading={messages.isLoading}
            hasMore={messages.hasNextPage}
            loadingMore={messages.isFetchingNextPage}
            onLoadMore={() => void messages.fetchNextPage()}
          />
        </div>
      </Card>

      <Card
        styles={{ body: { padding: 0, height: '100%' } }}
        style={{ flex: 1, minWidth: 0, height: '100%', overflow: 'hidden' }}
      >
        {selected ? (
          <MessageReader key={selected.id} summary={selected} onSelect={setSelected} />
        ) : (
          <Flex align="center" justify="center" style={{ height: '100%' }}>
            <Empty description={t('mail.noSelection')} />
          </Flex>
        )}
      </Card>
    </Flex>
  )
}
