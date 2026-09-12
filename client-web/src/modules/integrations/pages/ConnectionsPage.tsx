import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Input, Popconfirm, Space, Spin, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  providerLabel,
  type ExternalAccount,
  type IntegrationEvent,
  type IntegrationEventType,
  type IntegrationStatus,
} from '../models'
import {
  useAccounts,
  useDisconnectAccount,
  useIntegrationEvents,
  useProviders,
  useSaveApiKey,
  useStartConnection,
} from '../hooks/useIntegrations'

const REDIRECT_AFTER = '/integrations/connections'

const EVENT_TAG_COLOR: Record<IntegrationEventType, string> = {
  connected: 'green',
  reconnected: 'green',
  'refresh-failed': 'orange',
  expired: 'orange',
  revoked: 'red',
  disconnected: 'default',
}

function statusTag(status: IntegrationStatus, t: (k: string) => string) {
  switch (status) {
    case 'connected':
      return <Tag color="green">{t('connections.status.connected')}</Tag>
    case 'revoked':
      return <Tag color="red">{t('connections.status.revoked')}</Tag>
    case 'expired':
      return <Tag color="orange">{t('connections.status.expired')}</Tag>
    case 'needs-consent':
      return <Tag color="gold">{t('connections.status.needsConsent')}</Tag>
  }
}

export function ConnectionsPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const [searchParams, setSearchParams] = useSearchParams()

  const { data: providers, isLoading: providersLoading } = useProviders()
  const { data: accounts } = useAccounts()
  const { data: events } = useIntegrationEvents()

  const startConnection = useStartConnection()
  const disconnect = useDisconnectAccount()
  const saveApiKey = useSaveApiKey()

  const [apiKeyDrafts, setApiKeyDrafts] = useState<Record<string, string>>({})

  // Surface the callback outcome, then drop the query param so a refresh does not repeat it.
  const outcome = searchParams.get('integration')
  useEffect(() => {
    if (outcome) {
      if (outcome === 'error') message.error(t('connections.connectError'))
      const next = new URLSearchParams(searchParams)
      next.delete('integration')
      setSearchParams(next, { replace: true })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [outcome])

  const accountByProvider = new Map<string, ExternalAccount>(
    (accounts ?? []).map((a) => [a.provider, a]),
  )

  async function handleConnect(provider: string) {
    try {
      const url = await startConnection.mutateAsync({ provider, redirectAfter: REDIRECT_AFTER })
      window.location.href = url
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.connectError')))
    }
  }

  async function handleDisconnect(id: string) {
    try {
      await disconnect.mutateAsync(id)
      message.success(t('connections.disconnected'))
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.disconnectError')))
    }
  }

  async function handleSaveApiKey(provider: string) {
    const apiKey = (apiKeyDrafts[provider] ?? '').trim()
    if (!apiKey) return
    try {
      await saveApiKey.mutateAsync({ provider, apiKey })
      setApiKeyDrafts((drafts) => ({ ...drafts, [provider]: '' }))
      message.success(t('connections.apiKey.saved'))
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.apiKey.saveError')))
    }
  }

  const cardStyle = {
    border: `1px solid ${token.colorBorderSecondary}`,
    background: token.colorBgContainer,
  }

  const activityColumns: ColumnsType<IntegrationEvent> = [
    {
      title: t('connections.activity.colEvent'),
      dataIndex: 'eventType',
      render: (type: IntegrationEventType) => (
        <Tag color={EVENT_TAG_COLOR[type]}>{t(`connections.activity.event.${type}`)}</Tag>
      ),
    },
    {
      title: t('connections.activity.colProvider'),
      dataIndex: 'provider',
      render: (provider: string) => providerLabel(provider),
    },
    {
      title: t('connections.activity.colWhen'),
      dataIndex: 'occurredAt',
      render: (value: string) => new Date(value).toLocaleString(),
    },
    {
      title: t('connections.activity.colDetail'),
      dataIndex: 'detail',
      ellipsis: true,
      render: (detail: string | null) =>
        detail ? detail : <Typography.Text type="secondary">—</Typography.Text>,
    },
  ]

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <PageHeading title={t('connections.title')} description={t('connections.description')} />

      <section className="flex flex-col gap-2.5">
          <div className="px-0.5">
            <Typography.Text strong type="secondary" className="text-xs uppercase tracking-wider">
              {t('connections.providersTitle')}
            </Typography.Text>
          </div>

          {providersLoading ? (
            <div className="rounded-xl p-6" style={cardStyle}>
              <Spin />
            </div>
          ) : (providers ?? []).length === 0 ? (
            <div className="rounded-xl p-4" style={cardStyle}>
              <Typography.Text type="secondary">{t('connections.noneAvailable')}</Typography.Text>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {(providers ?? []).map((provider) => {
                const account = accountByProvider.get(provider.provider)
                const revoked = account?.status === 'revoked'
                const label = provider.displayName ?? providerLabel(provider.provider)

                return (
                  <div key={provider.provider} className="flex flex-col gap-3 rounded-xl p-4" style={cardStyle}>
                    <div className="flex items-center justify-between gap-2">
                      <Typography.Text strong>{label}</Typography.Text>
                      {account && statusTag(account.status, t)}
                    </div>

                    {provider.authKind === 'api-key' ? (
                      <>
                        {account?.displayName && (
                          <Typography.Text type="secondary">{account.displayName}</Typography.Text>
                        )}
                        {account && (
                          <Typography.Text type="secondary" className="text-xs">
                            {t('connections.apiKey.connectedHint')}
                          </Typography.Text>
                        )}
                        <Space.Compact className="w-full">
                          <Input.Password
                            placeholder={t('connections.apiKey.placeholder')}
                            value={apiKeyDrafts[provider.provider] ?? ''}
                            onChange={(e) =>
                              setApiKeyDrafts((drafts) => ({ ...drafts, [provider.provider]: e.target.value }))
                            }
                            onPressEnter={() => handleSaveApiKey(provider.provider)}
                          />
                          <Button
                            type="primary"
                            loading={saveApiKey.isPending}
                            onClick={() => handleSaveApiKey(provider.provider)}
                          >
                            {t('connections.apiKey.save')}
                          </Button>
                        </Space.Compact>
                        {account && (
                          <Popconfirm
                            title={t('connections.disconnectConfirm')}
                            onConfirm={() => handleDisconnect(account.id)}
                          >
                            <Button danger className="w-fit">
                              {t('connections.disconnect')}
                            </Button>
                          </Popconfirm>
                        )}
                      </>
                    ) : account ? (
                      <>
                        {account.displayName && (
                          <Typography.Text type="secondary">{account.displayName}</Typography.Text>
                        )}
                        {revoked && (
                          <Alert
                            type="warning"
                            showIcon
                            message={t('connections.reconnectNeeded')}
                            description={account.lastError ?? undefined}
                          />
                        )}
                        <Space>
                          {revoked && (
                            <Button
                              type="primary"
                              onClick={() => handleConnect(provider.provider)}
                              loading={startConnection.isPending}
                            >
                              {t('connections.reconnect')}
                            </Button>
                          )}
                          <Popconfirm
                            title={t('connections.disconnectConfirm')}
                            onConfirm={() => handleDisconnect(account.id)}
                          >
                            <Button danger>{t('connections.disconnect')}</Button>
                          </Popconfirm>
                        </Space>
                      </>
                    ) : (
                      <>
                        <Typography.Text type="secondary">{t('connections.providerDesc')}</Typography.Text>
                        <Button
                          type="primary"
                          className="w-fit"
                          onClick={() => handleConnect(provider.provider)}
                          loading={startConnection.isPending}
                        >
                          {t('connections.connect', { provider: providerLabel(provider.provider) })}
                        </Button>
                      </>
                    )}
                  </div>
                )
              })}
            </div>
          )}
      </section>

      {/* Recent activity */}
      {events && events.length > 0 && (
        <SettingsSection title={t('connections.activity.title')}>
          <Table<IntegrationEvent>
            rowKey="id"
            size="small"
            columns={activityColumns}
            dataSource={events}
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            scroll={{ x: true }}
          />
        </SettingsSection>
      )}
    </div>
  )
}
