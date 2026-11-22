import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Dropdown, Empty, Progress, Space, Spin, Tag, Typography, theme } from 'antd'
import { EllipsisOutlined, PlusOutlined, SyncOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingRow } from '@/components/settings/SettingRow'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import { useProviders, useStartConnection } from '@/modules/integrations/hooks/useIntegrations'
import { GMAIL_SCOPE, type Mailbox, type MailboxStatus, type MailProvider } from '../models'
import { useDeleteMailbox, useMailboxes, useSyncMailbox, useUpdateMailbox } from '../hooks/useMail'
import { ConnectMailboxModal } from '../components/ConnectMailboxModal'
import { FixConnectionModal } from '../components/FixConnectionModal'
import { usePreferences } from '@/modules/identity/context/preferences-context'

const STATUS_COLOR: Record<MailboxStatus, string> = {
  active: 'green',
  syncing: 'blue',
  'needs-reconnect': 'gold',
  error: 'red',
}

const PROVIDER_LABEL: Record<MailProvider, string> = { gmail: 'Gmail', titan: 'Titan', imap: 'IMAP' }

export function MailAccountsPage() {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [searchParams, setSearchParams] = useSearchParams()

  const { data: mailboxes, isLoading, error: mailboxesError } = useMailboxes()
  const { data: providers, error: providersError } = useProviders()
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
    // Gmail is fixed by a new consent; the others by editing the connection, which is always offered.
    const canFix = mailbox.status === 'needs-reconnect' || mailbox.provider !== 'gmail'
    const fixLabel = mailbox.status === 'needs-reconnect' ? t('mail.accounts.reconnect') : t('mail.accounts.fix')
    const name = mailbox.displayName ?? mailbox.address

    const summary = [
      mailbox.displayName && mailbox.address,
      t('mail.accounts.messages', { count: mailbox.messageCount }),
      mailbox.lastSyncedAt && t('mail.accounts.lastSynced', { when: new Date(mailbox.lastSyncedAt).toLocaleString(undefined, { timeZone }) }),
    ]
      .filter(Boolean)
      .join(' · ')

    return (
      // The row's own divider is dropped as the wrapper's last child, so the wrapper draws it.
      <div
        key={mailbox.id}
        className="border-b border-solid last:border-b-0"
        style={{ borderColor: token.colorBorderSecondary }}
      >
        <SettingRow
          label={
            <Space size={8} wrap>
              <Typography.Text strong editable={{ onChange: (value) => rename(mailbox, value) }}>
                {name}
              </Typography.Text>
              <Tag>{PROVIDER_LABEL[mailbox.provider]}</Tag>
              <Tag color={STATUS_COLOR[mailbox.status]}>{t(`mail.status.${mailbox.status}`)}</Tag>
            </Space>
          }
          description={summary}
          control={
            <Space size={4}>
              {broken && canFix && (
                <Button
                  type="primary"
                  size="small"
                  loading={mailbox.provider === 'gmail' && startConnection.isPending}
                  onClick={() => reconnect(mailbox)}
                >
                  {fixLabel}
                </Button>
              )}
              <Dropdown
                trigger={['click']}
                placement="bottomRight"
                menu={{
                  items: [
                    {
                      key: 'sync',
                      icon: <SyncOutlined spin={sync.isPending && sync.variables === mailbox.id} />,
                      label: t('mail.accounts.syncNow'),
                      onClick: () => void syncNow(mailbox.id),
                    },
                    ...(canFix && !broken ? [{ key: 'fix', label: fixLabel, onClick: () => void reconnect(mailbox) }] : []),
                    { type: 'divider' as const },
                    { key: 'delete', danger: true, label: t('mail.accounts.delete'), onClick: () => confirmDelete(mailbox) },
                  ],
                }}
              >
                <Button
                  type="text"
                  size="small"
                  icon={<EllipsisOutlined />}
                  aria-label={t('mail.accounts.moreActions', { name })}
                />
              </Dropdown>
            </Space>
          }
        />

        {backfilling || broken ? (
          <div className="flex flex-col gap-1 px-4 pb-4">
            {broken ? (
              <Alert
                type={mailbox.status === 'error' ? 'error' : 'warning'}
                showIcon
                title={mailbox.status === 'error' ? t('mail.accounts.errorTitle') : t('mail.accounts.reconnectTitle')}
                description={mailbox.lastError ?? undefined}
              />
            ) : (
              <>
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
              </>
            )}
          </div>
        ) : null}
      </div>
    )
  }

  const loadError = mailboxesError ?? providersError
  if (loadError) return <Alert type="error" showIcon title={loadError.message} />

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('mail.accounts.title')} description={t('mail.accounts.description')} />
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => setAdding(providers?.some((p) => p.provider === 'google') ? 'gmail' : 'titan')}
        >
          {t('mail.accounts.add')}
        </Button>
      </div>

      {isLoading ? (
        <Spin />
      ) : (mailboxes ?? []).length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('mail.accounts.none')} />
      ) : (
        <SettingsSection>{mailboxes!.map(renderMailbox)}</SettingsSection>
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
