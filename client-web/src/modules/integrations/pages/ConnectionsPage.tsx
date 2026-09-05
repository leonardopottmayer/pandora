import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Card, Divider, Input, List, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  providerLabel,
  type ExternalAccount,
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

const REDIRECT_AFTER = '/settings/connections'

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

  return (
    <div className="mx-auto max-w-2xl">
      <Card title={t('connections.title')}>
        <Typography.Paragraph type="secondary">{t('connections.description')}</Typography.Paragraph>

        {providersLoading ? (
          <Spin />
        ) : (
          <Space direction="vertical" size="large" className="w-full">
            {(providers ?? []).map((provider, index) => {
              const account = accountByProvider.get(provider.provider)
              const revoked = account?.status === 'revoked'
              const label = provider.displayName ?? providerLabel(provider.provider)

              return (
                <div key={provider.provider} className="flex flex-col gap-2">
                  {index > 0 && <Divider className="my-0" />}
                  <Space>
                    <Typography.Text strong>{label}</Typography.Text>
                    {account && statusTag(account.status, t)}
                  </Space>

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
                      <Typography.Text type="secondary">
                        {t('connections.providerDesc')}
                      </Typography.Text>
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

            {(providers ?? []).length === 0 && (
              <Typography.Text type="secondary">{t('connections.noneAvailable')}</Typography.Text>
            )}
          </Space>
        )}
      </Card>

      {events && events.length > 0 && (
        <Card title={t('connections.activity.title')} className="mt-4">
          <Typography.Paragraph type="secondary">
            {t('connections.activity.description')}
          </Typography.Paragraph>
          <List
            size="small"
            dataSource={events}
            renderItem={(event) => (
              <List.Item>
                <Space direction="vertical" size={0} className="w-full">
                  <Space wrap>
                    <Tag color={EVENT_TAG_COLOR[event.eventType]}>
                      {t(`connections.activity.event.${event.eventType}`)}
                    </Tag>
                    <Typography.Text strong>{providerLabel(event.provider)}</Typography.Text>
                    <Typography.Text type="secondary">
                      {new Date(event.occurredAt).toLocaleString()}
                    </Typography.Text>
                  </Space>
                  {event.detail && (
                    <Typography.Text type="secondary" className="text-xs">
                      {event.detail}
                    </Typography.Text>
                  )}
                </Space>
              </List.Item>
            )}
          />
        </Card>
      )}
    </div>
  )
}
