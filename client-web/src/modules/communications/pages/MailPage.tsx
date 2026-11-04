import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, Drawer, Empty, Grid, Input, Segmented, Spin, theme } from 'antd'
import { EditOutlined, FilterOutlined, MenuOutlined, SettingOutlined } from '@ant-design/icons'
import { ACTIONS_THAT_REMOVE, type MessageAction, type MessageSummary } from '../models'
import { useMailboxes, useMessages } from '../hooks/useMail'
import { ComposeDrawer, type ComposeRequest } from '../components/ComposeDrawer'
import { MailActions } from '../components/MailActions'
import { MailSidebar, type MailFolder } from '../components/MailSidebar'
import { MessageList } from '../components/MessageList'
import { MessageReader } from '../components/MessageReader'

export function MailPage() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  // Three panes need xl. Below it the folders slide in from the left, and below lg the list and the open
  // message take turns.
  const screens = Grid.useBreakpoint()
  const narrow = !screens.lg
  const foldersPane = !!screens.xl
  const { data: mailboxes, isLoading, error: mailboxesError } = useMailboxes()

  const [folder, setFolder] = useState<MailFolder>({})
  const [foldersOpen, setFoldersOpen] = useState(false)
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
  const allChecked = items.length > 0 && checked.length === items.length

  function onActionDone(action: MessageAction, ids: string[]) {
    setCheckedIds(new Set())
    if (ACTIONS_THAT_REMOVE.includes(action) && selected && ids.includes(selected.id)) setSelected(null)
  }

  function clearList() {
    setCheckedIds(new Set())
  }

  const compose = () => setComposing({ mode: 'new', mailboxId: folder.mailboxId })

  if (mailboxesError) return <Alert type="error" showIcon title={mailboxesError.message} />

  if (isLoading) return <Spin />
  if (!mailboxes?.length)
    return (
      <Empty className="mt-16" image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('mail.noMailboxes')}>
        <Link to="/mail/accounts">
          <Button type="primary">{t('mail.accounts.add')}</Button>
        </Link>
      </Empty>
    )

  const divider = `1px solid ${token.colorBorderSecondary}`
  const footerLink = 'flex items-center gap-2 rounded-md px-3 py-1.5 text-sm'

  const folders = (
    <div className="flex h-full flex-col">
      <div className="p-3">
        <Button type="primary" block icon={<EditOutlined />} onClick={compose}>
          {t('mail.compose.new')}
        </Button>
      </div>
      <div className="min-h-0 flex-1 overflow-auto px-1">
        <MailSidebar
          mailboxes={mailboxes}
          folder={folder}
          onSelect={(next) => {
            setFolder(next)
            setSelected(null)
            setFoldersOpen(false)
            clearList()
          }}
        />
      </div>
      <div className="flex flex-col p-2" style={{ borderTop: divider }}>
        <Link to="/mail/rules" className={footerLink} style={{ color: token.colorTextSecondary }}>
          <FilterOutlined /> {t('mail.rules.title')}
        </Link>
        <Link to="/mail/accounts" className={footerLink} style={{ color: token.colorTextSecondary }}>
          <SettingOutlined /> {t('mail.manageAccounts')}
        </Link>
      </div>
    </div>
  )

  const list = (
    <div className="flex h-full flex-col">
      <div className="flex flex-col gap-2 p-3" style={{ borderBottom: divider }}>
        <div className="flex gap-2">
          {!foldersPane && (
            <Button icon={<MenuOutlined />} aria-label={t('mail.folders')} onClick={() => setFoldersOpen(true)} />
          )}
          <Input.Search
            allowClear
            placeholder={t('mail.search')}
            onSearch={(value) => {
              setQ(value.trim())
              clearList()
            }}
          />
          {!foldersPane && (
            <Button type="primary" icon={<EditOutlined />} aria-label={t('mail.compose.new')} onClick={compose} />
          )}
        </div>
        {/* With a selection, the filter makes room for what can be done to it. */}
        <div className="flex min-h-7 flex-wrap items-center gap-2">
          <Checkbox
            aria-label={t('mail.actions.selectPage')}
            disabled={items.length === 0}
            checked={allChecked}
            indeterminate={!allChecked && checked.length > 0}
            onChange={() => setCheckedIds(new Set(allChecked ? [] : items.map((m) => m.id)))}
          />
          {checked.length > 0 ? (
            <>
              <span className="text-xs tabular-nums" style={{ color: token.colorTextSecondary }}>
                {t('mail.selected', { count: checked.length })}
              </span>
              <span className="ms-auto">
                <MailActions messages={checked} mailboxes={mailboxes} onDone={onActionDone} />
              </span>
            </>
          ) : (
            <Segmented<'all' | 'unread'>
              size="small"
              className="ms-auto"
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
          )}
        </div>
      </div>
      <div className="min-h-0 flex-1 overflow-auto">
        {messages.error ? (
          <Alert type="error" showIcon title={messages.error.message} />
        ) : (
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
        )}
      </div>
    </div>
  )

  const reader = current ? (
    <MessageReader
      key={current.id}
      summary={current}
      mailboxes={mailboxes}
      onSelect={setSelected}
      onActionDone={onActionDone}
      onCompose={(mode) => setComposing({ mode, message: current })}
      onBack={narrow ? () => setSelected(null) : undefined}
    />
  ) : (
    <div className="flex h-full items-center justify-center">
      <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('mail.noSelection')} />
    </div>
  )

  return (
    <>
      <div
        className="flex overflow-hidden rounded-xl"
        style={{ height: 'calc(100dvh - 112px)', border: divider, background: token.colorBgContainer }}
      >
        {narrow ? (
          <div className="min-w-0 flex-1">{current ? reader : list}</div>
        ) : (
          <>
            {foldersPane && (
              <div className="w-60 shrink-0" style={{ borderRight: divider }}>
                {folders}
              </div>
            )}
            <div className="w-[360px] shrink-0" style={{ borderRight: divider }}>
              {list}
            </div>
            <div className="min-w-0 flex-1">{reader}</div>
          </>
        )}
      </div>

      {!foldersPane && (
        <Drawer
          placement="left"
          size={280}
          open={foldersOpen}
          onClose={() => setFoldersOpen(false)}
          closable={false}
          styles={{ body: { padding: 0 } }}
        >
          {folders}
        </Drawer>
      )}

      {composing && <ComposeDrawer request={composing} mailboxes={mailboxes} onClose={() => setComposing(null)} />}
    </>
  )
}
