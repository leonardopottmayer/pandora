import { useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Card, Divider, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { providerLabel, type ExternalAccount, type IntegrationStatus } from '../models'
import { useAccounts, useDisconnectAccount, useProviders, useStartConnection } from '../hooks/useIntegrations'

const REDIRECT_AFTER = '/settings/connections'

function statusTag(status: IntegrationStatus, t: (k: string) => string) {
  switch (status) {
    case 'connected':
      return <Tag color="green">{t('connections.status.connected')}</Tag>
    case 'revoked':
      return <Tag color="red">{t('connections.status.revoked')}</Tag>
    case 'expired':
      return <Tag color="orange">{t('connections.status.expired')}</Tag>
    case 'needs_consent':
      return <Tag color="gold">{t('connections.status.needsConsent')}</Tag>
  }
}

export function ConnectionsPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [searchParams, setSearchParams] = useSearchParams()

  const { data: providers, isLoading: providersLoading } = useProviders()
  const { data: accounts } = useAccounts()

  const startConnection = useStartConnection()
  const disconnect = useDisconnectAccount()

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

              return (
                <div key={provider.provider} className="flex flex-col gap-2">
                  {index > 0 && <Divider className="my-0" />}
                  <Space>
                    <Typography.Text strong>{providerLabel(provider.provider)}</Typography.Text>
                    {account && statusTag(account.status, t)}
                  </Space>

                  {account ? (
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
    </div>
  )
}
