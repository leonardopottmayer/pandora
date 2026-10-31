import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Button, Card, Empty, Flex, Input, Segmented, Spin } from 'antd'
import { EditOutlined, SettingOutlined } from '@ant-design/icons'
import { ACTIONS_THAT_REMOVE, type MessageAction, type MessageSummary } from '../models'
import { useMailboxes, useMessages } from '../hooks/useMail'
import { ComposeDrawer, type ComposeRequest } from '../components/ComposeDrawer'
import { MailActions } from '../components/MailActions'
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
  const [checkedIds, setCheckedIds] = useState<ReadonlySet<string>>(new Set())
  const [composing, setComposing] = useState<ComposeRequest | null>(null)

  const messages = useMessages({ ...folder, unread: unreadOnly || undefined, q: q || undefined })
  const items = messages.data?.pages.flatMap((p) => p.items) ?? []

  // The list has the row's current flags after an action; the state only remembers which one is open.
  const current = selected && (items.find((m) => m.id === selected.id) ?? selected)
  const checked = items.filter((m) => checkedIds.has(m.id))

  function onActionDone(action: MessageAction, ids: string[]) {
    setCheckedIds(new Set())
    if (ACTIONS_THAT_REMOVE.includes(action) && selected && ids.includes(selected.id)) setSelected(null)
  }

  function clearList() {
    setCheckedIds(new Set())
  }

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
        <Button
          type="primary"
          block
          icon={<EditOutlined />}
          className="mb-2"
          onClick={() => setComposing({ mode: 'new', mailboxId: folder.mailboxId })}
        >
          {t('mail.compose.new')}
        </Button>
        <div className="flex-1">
          <MailSidebar
            mailboxes={mailboxes}
            folder={folder}
            onSelect={(next) => {
              setFolder(next)
              setSelected(null)
              clearList()
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
          <Input.Search
            allowClear
            placeholder={t('mail.search')}
            onSearch={(value) => {
              setQ(value.trim())
              clearList()
            }}
          />
          <Segmented<'all' | 'unread'>
            size="small"
            value={unreadOnly ? 'unread' : 'all'}
            onChange={(value) => {
              setUnreadOnly(value === 'unread')
              clearList()
            }}
            options={[
              { value: 'all', label: t('mail.filterAll') },
              { value: 'unread', label: t('mail.filterUnread') },
            ]}
          />
          {checked.length > 0 && <MailActions messages={checked} mailboxes={mailboxes} onDone={onActionDone} />}
        </div>
        <div className="flex-1 overflow-auto">
          <MessageList
            messages={items}
            selectedId={selected?.id ?? null}
            onSelect={setSelected}
            checkedIds={checkedIds}
            onCheckedChange={setCheckedIds}
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
        {current ? (
          <MessageReader
            key={current.id}
            summary={current}
            mailboxes={mailboxes}
            onSelect={setSelected}
            onActionDone={onActionDone}
            onCompose={(mode) => setComposing({ mode, message: current })}
          />
        ) : (
          <Flex align="center" justify="center" style={{ height: '100%' }}>
            <Empty description={t('mail.noSelection')} />
          </Flex>
        )}
      </Card>

      {composing && <ComposeDrawer request={composing} mailboxes={mailboxes} onClose={() => setComposing(null)} />}
    </Flex>
  )
}
