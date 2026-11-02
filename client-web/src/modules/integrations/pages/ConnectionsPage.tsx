import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Dropdown, Form, Input, Modal, Space, Spin, Tabs, Tag, Typography } from 'antd'
import { EllipsisOutlined, PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingRow } from '@/components/settings/SettingRow'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { toErrorMessage } from '@/lib/api/envelope'
import { providerLabel, type ExternalAccount, type IntegrationStatus, type ProviderCatalogItem } from '../models'
import {
  useAccounts,
  useDisconnectAccount,
  useProviders,
  useRenameAccount,
  useReplaceSecret,
  useSaveApiKey,
  useSavePassword,
  useStartConnection,
} from '../hooks/useIntegrations'
import { usageLabel } from '../lib/usageLabel'
import { IntegrationActivity } from '../components/IntegrationActivity'

const REDIRECT_AFTER = '/integrations/connections'
const ACTIVITY_TAB = 'activity'
const ACCOUNTS_TAB = 'accounts'

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

/** The form of the account modals: which fields show depends on the mode and the auth kind. */
interface AccountForm {
  label?: string
  username?: string
  secret?: string
}

type AccountModal =
  | { mode: 'add'; provider: ProviderCatalogItem }
  | { mode: 'replace'; account: ExternalAccount }
  | { mode: 'rename'; account: ExternalAccount }

function AccountsTab() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()

  const { data: providers, isLoading: providersLoading } = useProviders()
  const { data: accounts, isLoading: accountsLoading } = useAccounts()

  const startConnection = useStartConnection()
  const disconnect = useDisconnectAccount()
  const rename = useRenameAccount()
  const saveApiKey = useSaveApiKey()
  const savePassword = useSavePassword()
  const replaceSecret = useReplaceSecret()

  const [accountModal, setAccountModal] = useState<AccountModal | null>(null)
  const [form] = Form.useForm<AccountForm>()

  async function handleConnect(provider: string, accountId?: string) {
    try {
      const url = await startConnection.mutateAsync({ provider, redirectAfter: REDIRECT_AFTER, accountId })
      window.location.assign(url)
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.connectError')))
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

  function openModal(next: AccountModal) {
    form.resetFields()
    if (next.mode === 'rename') form.setFieldsValue({ label: next.account.label })
    setAccountModal(next)
  }

  const modalAuthKind = accountModal?.mode === 'add' ? accountModal.provider.authKind : accountModal?.account.authKind
  const isPassword = modalAuthKind === 'password'

  /** What failed, in the words of the modal that is open. */
  function saveErrorMessage(current: AccountModal): string {
    if (current.mode === 'rename') return t('connections.renameError')
    if (current.mode === 'replace') return t('connections.replaceError')
    return isPassword ? t('connections.password.saveError') : t('connections.apiKey.saveError')
  }

  async function handleSubmit(values: AccountForm) {
    if (!accountModal) return
    try {
      if (accountModal.mode === 'rename') {
        const label = values.label?.trim() ?? ''
        if (label !== accountModal.account.label) {
          await rename.mutateAsync({ id: accountModal.account.id, label })
          message.success(t('connections.renamed'))
        }
      } else if (accountModal.mode === 'replace') {
        await replaceSecret.mutateAsync({ id: accountModal.account.id, secret: values.secret ?? '' })
        message.success(t('connections.secretReplaced'))
      } else if (isPassword) {
        await savePassword.mutateAsync({
          provider: accountModal.provider.provider,
          username: values.username ?? '',
          password: values.secret ?? '',
          label: values.label,
        })
        message.success(t('connections.password.saved'))
      } else {
        await saveApiKey.mutateAsync({
          provider: accountModal.provider.provider,
          apiKey: values.secret ?? '',
          label: values.label,
        })
        message.success(t('connections.apiKey.saved'))
      }
      setAccountModal(null)
    } catch (err) {
      message.error(toErrorMessage(err, saveErrorMessage(accountModal)))
    }
  }

  function renderAccount(account: ExternalAccount) {
    const needsAttention = account.status !== 'connected'
    const refreshLabel =
      account.authKind === 'oauth'
        ? t('connections.reconnect')
        : account.authKind === 'password'
          ? t('connections.replacePassword')
          : t('connections.replaceKey')
    const refresh = () =>
      account.authKind === 'oauth' ? handleConnect(account.provider, account.id) : openModal({ mode: 'replace', account })

    const usedBy =
      account.usages.length === 0
        ? t('connections.notUsed')
        : `${t('connections.usedBy')}: ${account.usages.map((u) => usageLabel(t, u)).join(', ')}`
    const hint = account.displayName && account.displayName !== account.label ? `${account.displayName} · ` : ''

    return (
      <div key={account.id}>
        <SettingRow
          label={
            <Space size={8}>
              {account.label}
              {statusTag(account.status, t)}
            </Space>
          }
          description={`${hint}${usedBy}`}
          control={
            <Space size={4}>
              {needsAttention && (
                <Button type="primary" size="small" loading={startConnection.isPending} onClick={refresh}>
                  {refreshLabel}
                </Button>
              )}
              <Dropdown
                trigger={['click']}
                placement="bottomRight"
                menu={{
                  items: [
                    { key: 'rename', label: t('connections.rename'), onClick: () => openModal({ mode: 'rename', account }) },
                    ...(needsAttention ? [] : [{ key: 'refresh', label: refreshLabel, onClick: refresh }]),
                    { type: 'divider' as const },
                    { key: 'disconnect', danger: true, label: t('connections.disconnect'), onClick: () => confirmDisconnect(account) },
                  ],
                }}
              >
                <Button type="text" size="small" icon={<EllipsisOutlined />} aria-label={t('connections.moreActions', { name: account.label })} />
              </Dropdown>
            </Space>
          }
        />
        {account.status === 'revoked' && (
          <div className="px-4 pb-4">
            <Alert type="warning" showIcon title={t('connections.reconnectNeeded')} description={account.lastError ?? undefined} />
          </div>
        )}
      </div>
    )
  }

  function addAccount(provider: ProviderCatalogItem) {
    if (provider.authKind === 'oauth') void handleConnect(provider.provider)
    else openModal({ mode: 'add', provider })
  }

  const providerName = (provider: ProviderCatalogItem) => provider.displayName ?? providerLabel(provider.provider)
  const accountsOf = (provider: ProviderCatalogItem) => (accounts ?? []).filter((a) => a.provider === provider.provider)
  const withAccounts = (providers ?? []).filter((p) => accountsOf(p).length > 0)
  const withoutAccounts = (providers ?? []).filter((p) => accountsOf(p).length === 0)

  const modalTitle =
    accountModal?.mode === 'add'
      ? t('connections.addAccountTitle', { provider: providerName(accountModal.provider) })
      : accountModal?.account.label

  if (providersLoading || accountsLoading) return <Spin />

  if ((providers ?? []).length === 0) {
    return <Typography.Text type="secondary">{t('connections.noneAvailable')}</Typography.Text>
  }

  return (
    <div className="flex flex-col gap-6">
      {withAccounts.map((provider) => (
        <SettingsSection
          key={provider.provider}
          title={providerName(provider)}
          extra={
            <Button size="small" icon={<PlusOutlined />} onClick={() => addAccount(provider)}>
              {t('connections.addAccount')}
            </Button>
          }
        >
          {accountsOf(provider).map(renderAccount)}
        </SettingsSection>
      ))}

      {withoutAccounts.length > 0 && (
        <SettingsSection title={t(withAccounts.length > 0 ? 'connections.otherProviders' : 'connections.providers')}>
          {withoutAccounts.map((provider) => (
            <SettingRow
              key={provider.provider}
              label={providerName(provider)}
              control={
                <Button size="small" onClick={() => addAccount(provider)}>
                  {t('connections.connect')}
                </Button>
              }
            />
          ))}
        </SettingsSection>
      )}

      <Modal
        open={accountModal !== null}
        title={modalTitle}
        okText={t('common.save')}
        confirmLoading={saveApiKey.isPending || savePassword.isPending || replaceSecret.isPending || rename.isPending}
        onOk={() => form.submit()}
        onCancel={() => setAccountModal(null)}
        destroyOnHidden
      >
        <Form<AccountForm> form={form} layout="vertical" onFinish={handleSubmit} preserve={false}>
          {accountModal?.mode !== 'replace' && (
            <Form.Item
              name="label"
              label={t('connections.labelField')}
              rules={accountModal?.mode === 'rename' ? [{ required: true, whitespace: true }] : undefined}
            >
              <Input placeholder={t('connections.labelPlaceholder')} maxLength={100} />
            </Form.Item>
          )}
          {accountModal?.mode === 'add' && isPassword && (
            <Form.Item name="username" label={t('connections.password.username')} rules={[{ required: true }]}>
              <Input autoComplete="username" />
            </Form.Item>
          )}
          {accountModal?.mode !== 'rename' && (
            <Form.Item
              name="secret"
              label={isPassword ? t('connections.password.password') : t('connections.apiKey.placeholder')}
              rules={[{ required: true }]}
            >
              <Input.Password autoComplete={isPassword ? 'current-password' : 'off'} />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </div>
  )
}

export function ConnectionsPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = searchParams.get('tab') === ACTIVITY_TAB ? ACTIVITY_TAB : ACCOUNTS_TAB

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

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-4">
      <PageHeading title={t('connections.title')} description={t('connections.description')} />
      <Tabs
        activeKey={tab}
        onChange={(next) => setSearchParams(next === ACTIVITY_TAB ? { tab: ACTIVITY_TAB } : {}, { replace: true })}
        items={[
          { key: ACCOUNTS_TAB, label: t('connections.tabAccounts'), children: <AccountsTab /> },
          { key: ACTIVITY_TAB, label: t('connections.activity.title'), children: <IntegrationActivity /> },
        ]}
      />
    </div>
  )
}
