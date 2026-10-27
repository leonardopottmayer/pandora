import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Form, Input, Modal, Space, Spin, Table, Tag, Typography, theme } from 'antd'
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
  type ProviderCatalogItem,
} from '../models'
import {
  useAccounts,
  useDisconnectAccount,
  useIntegrationEvents,
  useProviders,
  useRenameAccount,
  useReplaceSecret,
  useSaveApiKey,
  useSavePassword,
  useStartConnection,
} from '../hooks/useIntegrations'
import { usageLabel } from '../lib/usageLabel'

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

/** The form of the "add account" / "replace secret" modals: which fields depend on the auth kind. */
interface SecretForm {
  label?: string
  username?: string
  secret: string
}

type SecretModal =
  | { mode: 'add'; provider: ProviderCatalogItem }
  | { mode: 'replace'; account: ExternalAccount }

export function ConnectionsPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [searchParams, setSearchParams] = useSearchParams()

  const { data: providers, isLoading: providersLoading } = useProviders()
  const { data: accounts } = useAccounts()
  const { data: events } = useIntegrationEvents()

  const startConnection = useStartConnection()
  const disconnect = useDisconnectAccount()
  const rename = useRenameAccount()
  const saveApiKey = useSaveApiKey()
  const savePassword = useSavePassword()
  const replaceSecret = useReplaceSecret()

  const [secretModal, setSecretModal] = useState<SecretModal | null>(null)
  const [form] = Form.useForm<SecretForm>()

  // Surface the callback outcome, then drop the query param so a refresh does not repeat it.
  const outcome = searchParams.get('integration')
  useEffect(() => {
    if (outcome) {
      if (outcome === 'error') message.error(t('connections.connectError'))
      if (outcome === 'other-account') message.warning(t('connections.otherAccount'), 8)
      const next = new URLSearchParams(searchParams)
      next.delete('integration')
      setSearchParams(next, { replace: true })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [outcome])

  async function handleConnect(provider: string, accountId?: string) {
    try {
      const url = await startConnection.mutateAsync({ provider, redirectAfter: REDIRECT_AFTER, accountId })
      window.location.assign(url)
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.connectError')))
    }
  }

  async function handleRename(account: ExternalAccount, label: string) {
    if (!label.trim() || label.trim() === account.label) return
    try {
      await rename.mutateAsync({ id: account.id, label: label.trim() })
      message.success(t('connections.renamed'))
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.renameError')))
    }
  }

  function confirmDisconnect(account: ExternalAccount) {
    const inUse = account.usages.length > 0
    modal.confirm({
      title: inUse ? t('connections.disconnectUsedTitle') : t('connections.disconnectConfirm'),
      content: inUse ? (
        <div className="flex flex-col gap-1">
          <Typography.Text>{t('connections.disconnectUsedDesc')}</Typography.Text>
          <ul className="m-0 pl-5">
            {account.usages.map((u) => (
              <li key={`${u.module}.${u.purpose}.${u.detail ?? ''}`}>{usageLabel(t, u)}</li>
            ))}
          </ul>
        </div>
      ) : undefined,
      okText: t('connections.disconnect'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await disconnect.mutateAsync(account.id)
          message.success(t('connections.disconnected'))
        } catch (err) {
          message.error(toErrorMessage(err, t('connections.disconnectError')))
        }
      },
    })
  }

  function openSecretModal(next: SecretModal) {
    form.resetFields()
    setSecretModal(next)
  }

  const modalAuthKind =
    secretModal?.mode === 'add' ? secretModal.provider.authKind : secretModal?.account.authKind
  const modalProvider = secretModal?.mode === 'add' ? secretModal.provider.provider : secretModal?.account.provider
  const isPassword = modalAuthKind === 'password'

  async function handleSecretSubmit(values: SecretForm) {
    if (!secretModal || !modalProvider) return
    try {
      if (secretModal.mode === 'replace') {
        await replaceSecret.mutateAsync({ id: secretModal.account.id, secret: values.secret })
        message.success(t('connections.secretReplaced'))
      } else if (isPassword) {
        await savePassword.mutateAsync({
          provider: modalProvider,
          username: values.username ?? '',
          password: values.secret,
          label: values.label,
        })
        message.success(t('connections.password.saved'))
      } else {
        await saveApiKey.mutateAsync({ provider: modalProvider, apiKey: values.secret, label: values.label })
        message.success(t('connections.apiKey.saved'))
      }
      setSecretModal(null)
    } catch (err) {
      message.error(
        toErrorMessage(
          err,
          secretModal.mode === 'replace'
            ? t('connections.replaceError')
            : isPassword
              ? t('connections.password.saveError')
              : t('connections.apiKey.saveError'),
        ),
      )
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

  function renderAccount(account: ExternalAccount) {
    const revoked = account.status === 'revoked'
    return (
      <div key={account.id} className="flex flex-col gap-2 rounded-lg p-3" style={cardStyle}>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <Typography.Text strong editable={{ onChange: (label) => handleRename(account, label) }}>
            {account.label}
          </Typography.Text>
          {statusTag(account.status, t)}
        </div>
        {account.displayName && account.displayName !== account.label && (
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
        <div className="flex flex-wrap items-center gap-1.5">
          <Typography.Text type="secondary" className="text-xs">
            {t('connections.usedBy')}:
          </Typography.Text>
          {account.usages.length === 0 ? (
            <Typography.Text type="secondary" className="text-xs">
              {t('connections.notUsed')}
            </Typography.Text>
          ) : (
            account.usages.map((u) => (
              <Tag key={`${u.module}.${u.purpose}.${u.detail ?? ''}`} color="blue">
                {usageLabel(t, u)}
              </Tag>
            ))
          )}
        </div>
        <Space wrap>
          {account.authKind === 'oauth' ? (
            <Button
              size="small"
              type={revoked ? 'primary' : 'default'}
              loading={startConnection.isPending}
              onClick={() => handleConnect(account.provider, account.id)}
            >
              {t('connections.reconnect')}
            </Button>
          ) : (
            <Button size="small" onClick={() => openSecretModal({ mode: 'replace', account })}>
              {account.authKind === 'password' ? t('connections.replacePassword') : t('connections.replaceKey')}
            </Button>
          )}
          <Button size="small" danger onClick={() => confirmDisconnect(account)}>
            {t('connections.disconnect')}
          </Button>
        </Space>
      </div>
    )
  }

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
          <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
            {(providers ?? []).map((provider) => {
              const name = provider.displayName ?? providerLabel(provider.provider)
              const providerAccounts = (accounts ?? []).filter((a) => a.provider === provider.provider)

              return (
                <div key={provider.provider} className="flex flex-col gap-3 rounded-xl p-4" style={cardStyle}>
                  <div className="flex items-center justify-between gap-2">
                    <Typography.Text strong>{name}</Typography.Text>
                    <Button
                      size="small"
                      type="primary"
                      loading={provider.authKind === 'oauth' && startConnection.isPending}
                      onClick={() =>
                        provider.authKind === 'oauth'
                          ? handleConnect(provider.provider)
                          : openSecretModal({ mode: 'add', provider })
                      }
                    >
                      {t('connections.addAccount')}
                    </Button>
                  </div>
                  {providerAccounts.length === 0 ? (
                    <Typography.Text type="secondary">{t('connections.noAccounts')}</Typography.Text>
                  ) : (
                    providerAccounts.map(renderAccount)
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

      <Modal
        open={secretModal !== null}
        title={
          secretModal?.mode === 'add'
            ? t('connections.addAccountTitle', {
                provider: secretModal.provider.displayName ?? providerLabel(secretModal.provider.provider),
              })
            : secretModal
              ? secretModal.account.label
              : undefined
        }
        okText={t('connections.apiKey.save')}
        confirmLoading={saveApiKey.isPending || savePassword.isPending || replaceSecret.isPending}
        onOk={() => form.submit()}
        onCancel={() => setSecretModal(null)}
        destroyOnHidden
      >
        <Form<SecretForm> form={form} layout="vertical" onFinish={handleSecretSubmit} preserve={false}>
          {secretModal?.mode === 'add' && (
            <Form.Item name="label" label={t('connections.labelField')}>
              <Input placeholder={t('connections.labelPlaceholder')} maxLength={100} />
            </Form.Item>
          )}
          {secretModal?.mode === 'add' && isPassword && (
            <Form.Item name="username" label={t('connections.password.username')} rules={[{ required: true }]}>
              <Input autoComplete="username" />
            </Form.Item>
          )}
          <Form.Item
            name="secret"
            label={isPassword ? t('connections.password.password') : t('connections.apiKey.placeholder')}
            rules={[{ required: true }]}
          >
            <Input.Password autoComplete={isPassword ? 'current-password' : 'off'} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  )
}
