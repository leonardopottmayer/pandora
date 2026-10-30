import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Empty, Progress, Space, Spin, Tag, Typography, theme } from 'antd'
import { PlusOutlined, SyncOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import { useProviders, useStartConnection } from '@/modules/integrations/hooks/useIntegrations'
import { GMAIL_SCOPE, type Mailbox, type MailboxStatus, type MailProvider } from '../models'
import { useDeleteMailbox, useMailboxes, useSyncMailbox, useUpdateMailbox } from '../hooks/useMail'
import { ConnectMailboxModal } from '../components/ConnectMailboxModal'
import { FixConnectionModal } from '../components/FixConnectionModal'

const STATUS_COLOR: Record<MailboxStatus, string> = {
  active: 'green',
  syncing: 'blue',
  'needs-reconnect': 'gold',
  error: 'red',
}

const PROVIDER_LABEL: Record<MailProvider, string> = { gmail: 'Gmail', titan: 'Titan', imap: 'IMAP' }

export function MailAccountsPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [searchParams, setSearchParams] = useSearchParams()

  const { data: mailboxes, isLoading } = useMailboxes()
  const { data: providers } = useProviders()
  const startConnection = useStartConnection()
  const sync = useSyncMailbox()
  const update = useUpdateMailbox()
  const remove = useDeleteMailbox()

  // Back from a Gmail consent: reopen "add" on Gmail.
  const [adding, setAdding] = useState<MailProvider | null>(() =>
    searchParams.get('connect') === 'gmail' ? 'gmail' : null,
  )
  const [fixing, setFixing] = useState<Mailbox | null>(null)

  async function syncNow(id: string) {
    try {
      const mailbox = await sync.mutateAsync(id)
      if (mailbox.status === 'needs-reconnect' || mailbox.status === 'error')
        message.error(mailbox.lastError ?? t('mail.accounts.syncError'))
      else message.success(t('mail.accounts.synced'))
    } catch (err) {
      message.error(toErrorMessage(err, t('mail.accounts.syncError')))
    }
  }

  // Back from Google: `sync={id}` finishes a reconnect; then the params go, so a refresh does not repeat it.
  const syncParam = searchParams.get('sync')
  const outcome = searchParams.get('integration')
  useEffect(() => {
    if (outcome === 'error') message.error(t('connections.connectError'))
    else if (outcome === 'other-account') message.warning(t('connections.otherAccount'), 8)
    else if (syncParam) void syncNow(syncParam)
    if (searchParams.size > 0) setSearchParams({}, { replace: true })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [syncParam, outcome])

  async function reconnect(mailbox: Mailbox) {
    if (mailbox.provider !== 'gmail') {
      setFixing(mailbox)
      return
    }
    try {
      const google = providers?.find((p) => p.provider === 'google')
      const url = await startConnection.mutateAsync({
        provider: 'google',
        redirectAfter: `/mail/accounts?sync=${mailbox.id}`,
        accountId: mailbox.externalAccountId,
        scopes: [...(google?.defaultScopes ?? []), GMAIL_SCOPE],
      })
      window.location.assign(url)
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.connectError')))
    }
  }

  async function rename(mailbox: Mailbox, value: string) {
    const displayName = value.trim() || null
    if (displayName === (mailbox.displayName ?? mailbox.address)) return
    try {
      await update.mutateAsync({ id: mailbox.id, displayName })
    } catch (err) {
      message.error(toErrorMessage(err, t('mail.accounts.renameError')))
    }
  }

  function confirmDelete(mailbox: Mailbox) {
    modal.confirm({
      title: t('mail.accounts.deleteTitle', { address: mailbox.address }),
      content: t('mail.accounts.deleteDesc'),
      okText: t('mail.accounts.delete'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await remove.mutateAsync(mailbox.id)
        } catch (err) {
          message.error(toErrorMessage(err, t('mail.accounts.deleteError')))
        }
      },
    })
  }

  function handleConnected(mailbox: Mailbox) {
    setAdding(null)
    // Creating a mailbox does not start the backfill: the first cycle does.
    sync.mutate(mailbox.id)
  }

  function renderMailbox(mailbox: Mailbox) {
    const backfilling = mailbox.backfillCompletedAt === null
    const estimate = mailbox.messageTotalEstimate
    const broken = mailbox.status === 'needs-reconnect' || mailbox.status === 'error'

    return (
      <div
        key={mailbox.id}
        className="flex flex-col gap-2 rounded-xl p-4"
        style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
      >
        <div className="flex flex-wrap items-center justify-between gap-2">
          <Space size={8}>
            <Typography.Text strong editable={{ onChange: (value) => rename(mailbox, value) }}>
              {mailbox.displayName ?? mailbox.address}
            </Typography.Text>
            <Tag>{PROVIDER_LABEL[mailbox.provider]}</Tag>
          </Space>
          <Tag color={STATUS_COLOR[mailbox.status]}>{t(`mail.status.${mailbox.status}`)}</Tag>
        </div>
        {mailbox.displayName && <Typography.Text type="secondary">{mailbox.address}</Typography.Text>}

        {backfilling && !broken ? (
          <div className="flex flex-col">
            <Typography.Text type="secondary" className="text-xs">
              {estimate
                ? t('mail.accounts.backfill', { count: mailbox.messageCount, total: estimate })
                : t('mail.accounts.backfillStarting')}
            </Typography.Text>
            <Progress
              percent={estimate ? Math.min(99, Math.floor((mailbox.messageCount / estimate) * 100)) : 0}
              status="active"
              size="small"
            />
          </div>
        ) : (
          <Typography.Text type="secondary" className="text-xs">
            {t('mail.accounts.messages', { count: mailbox.messageCount })}
            {mailbox.lastSyncedAt &&
              ` · ${t('mail.accounts.lastSynced', { when: new Date(mailbox.lastSyncedAt).toLocaleString() })}`}
          </Typography.Text>
        )}

        {broken && (
          <Alert
            type={mailbox.status === 'error' ? 'error' : 'warning'}
            showIcon
            message={mailbox.status === 'error' ? t('mail.accounts.errorTitle') : t('mail.accounts.reconnectTitle')}
            description={mailbox.lastError ?? undefined}
          />
        )}

        <Space wrap>
          <Button
            size="small"
            icon={<SyncOutlined />}
            loading={sync.isPending && sync.variables === mailbox.id}
            onClick={() => syncNow(mailbox.id)}
          >
            {t('mail.accounts.syncNow')}
          </Button>
          {(mailbox.status === 'needs-reconnect' || mailbox.provider !== 'gmail') && (
            <Button
              size="small"
              type={mailbox.status === 'needs-reconnect' ? 'primary' : 'default'}
              loading={mailbox.provider === 'gmail' && startConnection.isPending}
              onClick={() => reconnect(mailbox)}
            >
              {mailbox.status === 'needs-reconnect' ? t('mail.accounts.reconnect') : t('mail.accounts.fix')}
            </Button>
          )}
          <Button size="small" danger onClick={() => confirmDelete(mailbox)}>
            {t('mail.accounts.delete')}
          </Button>
        </Space>
      </div>
    )
  }

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('mail.accounts.title')} description={t('mail.accounts.description')} />
        <Space>
          <Link to="/mail">{t('mail.accounts.openInbox')}</Link>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setAdding(providers?.some((p) => p.provider === 'google') ? 'gmail' : 'titan')}
          >
            {t('mail.accounts.add')}
          </Button>
        </Space>
      </div>

      {isLoading ? (
        <Spin />
      ) : (mailboxes ?? []).length === 0 ? (
        <Empty description={t('mail.accounts.none')} />
      ) : (
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">{mailboxes!.map(renderMailbox)}</div>
      )}

      <ConnectMailboxModal
        open={adding !== null}
        initialProvider={adding ?? 'gmail'}
        usedAccountIds={(mailboxes ?? []).map((m) => m.externalAccountId)}
        onClose={() => setAdding(null)}
        onConnected={handleConnected}
      />
      <FixConnectionModal mailbox={fixing} onClose={() => setFixing(null)} />
    </div>
  )
}
