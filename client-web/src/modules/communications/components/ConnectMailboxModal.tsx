import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Form, Input, InputNumber, Modal, Segmented } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { AccountSelect } from '@/modules/integrations/components/AccountSelect'
import { useProviders, useSavePassword, useStartConnection } from '@/modules/integrations/hooks/useIntegrations'
import { ACCOUNT_PROVIDER, GMAIL_RETURN, GMAIL_SCOPE, type Mailbox, type MailProvider } from '../models'
import { useConnectMailbox } from '../hooks/useMail'

interface ConnectForm {
  provider: MailProvider
  displayName?: string
  username?: string
  password?: string
  imapHost?: string
  imapPort?: number
  smtpHost?: string
  smtpPort?: number
}

interface ConnectMailboxModalProps {
  open: boolean
  initialProvider: MailProvider
  /** Accounts that already have a mailbox: one mailbox per account. */
  usedAccountIds: string[]
  onClose: () => void
  onConnected: (mailbox: Mailbox) => void
}

/**
 * Adds a mailbox. Gmail uses a Google account from Integrations (or connects a new one with the Gmail
 * scope); Titan and IMAP store the password in Integrations first. The server tests the connection
 * before creating anything, so a wrong password comes back as an error here.
 */
export function ConnectMailboxModal({ open, initialProvider, usedAccountIds, onClose, onConnected }: ConnectMailboxModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<ConnectForm>()
  const provider = Form.useWatch('provider', form) ?? initialProvider
  const [accountId, setAccountId] = useState<string | null>(null)

  const { data: providers } = useProviders()
  const google = providers?.find((p) => p.provider === 'google')
  const startConnection = useStartConnection()
  const savePassword = useSavePassword()
  const connect = useConnectMailbox()

  async function connectGoogle() {
    try {
      const url = await startConnection.mutateAsync({
        provider: 'google',
        redirectAfter: GMAIL_RETURN,
        scopes: [...(google?.defaultScopes ?? []), GMAIL_SCOPE],
      })
      window.location.assign(url)
    } catch (err) {
      message.error(toErrorMessage(err, t('connections.connectError')))
    }
  }

  async function handleFinish(values: ConnectForm) {
    try {
      const externalAccountId =
        values.provider === 'gmail'
          ? accountId
          : ((await savePassword.mutateAsync({
              provider: ACCOUNT_PROVIDER[values.provider],
              username: values.username!.trim(),
              password: values.password!,
            })) as string)
      if (!externalAccountId) return

      const mailbox = await connect.mutateAsync({
        provider: values.provider,
        externalAccountId,
        displayName: values.displayName?.trim() || undefined,
        ...(values.provider === 'imap'
          ? {
              imapHost: values.imapHost!.trim(),
              imapPort: values.imapPort!,
              smtpHost: values.smtpHost!.trim(),
              smtpPort: values.smtpPort!,
            }
          : {}),
      })
      message.success(t('mail.accounts.connected', { address: mailbox.address }))
      onConnected(mailbox)
    } catch (err) {
      message.error(toErrorMessage(err, t('mail.accounts.connectError')))
    }
  }

  const isPassword = provider !== 'gmail'

  return (
    <Modal
      open={open}
      title={t('mail.accounts.add')}
      okText={t('mail.accounts.connect')}
      okButtonProps={{ disabled: provider === 'gmail' && !accountId }}
      confirmLoading={savePassword.isPending || connect.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form<ConnectForm>
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        preserve={false}
        initialValues={{ provider: initialProvider, imapPort: 993, smtpPort: 465 }}
      >
        <Form.Item name="provider">
          <Segmented<MailProvider>
            block
            options={[
              { value: 'gmail', label: 'Gmail', disabled: !google },
              { value: 'titan', label: 'Titan' },
              { value: 'imap', label: t('mail.provider.imap') },
            ]}
          />
        </Form.Item>

        {provider === 'gmail' && (
          <Form.Item label={t('mail.accounts.googleAccount')}>
            <AccountSelect
              providers={['google']}
              authKind="oauth"
              requiredScopes={[GMAIL_SCOPE]}
              excludeIds={usedAccountIds}
              value={accountId}
              onChange={setAccountId}
              redirectAfter={GMAIL_RETURN}
            />
            <Button type="link" className="mt-1 px-0" loading={startConnection.isPending} onClick={connectGoogle}>
              {t('mail.accounts.connectGoogle')}
            </Button>
          </Form.Item>
        )}

        {isPassword && (
          <>
            <Form.Item
              name="username"
              label={t('mail.accounts.email')}
              rules={[{ required: true, type: 'email' }]}
              extra={provider === 'titan' ? t('mail.accounts.titanHint') : undefined}
            >
              <Input autoComplete="username" />
            </Form.Item>
            <Form.Item name="password" label={t('connections.password.password')} rules={[{ required: true }]}>
              <Input.Password autoComplete="current-password" />
            </Form.Item>
          </>
        )}

        {provider === 'imap' && <ServerFields />}

        <Form.Item name="displayName" label={t('mail.accounts.displayName')}>
          <Input placeholder={t('mail.accounts.displayNamePlaceholder')} maxLength={100} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/** IMAP and SMTP host + port; used when adding a generic IMAP mailbox and when fixing one. */
export function ServerFields() {
  const { t } = useTranslation()
  return (
    <div className="grid grid-cols-[1fr_7rem] gap-x-3">
      <Form.Item name="imapHost" label={t('mail.accounts.imapHost')} rules={[{ required: true, whitespace: true }]}>
        <Input placeholder="imap.example.com" />
      </Form.Item>
      <Form.Item name="imapPort" label={t('mail.accounts.port')} rules={[{ required: true }]}>
        <InputNumber min={1} max={65535} className="w-full" />
      </Form.Item>
      <Form.Item name="smtpHost" label={t('mail.accounts.smtpHost')} rules={[{ required: true, whitespace: true }]}>
        <Input placeholder="smtp.example.com" />
      </Form.Item>
      <Form.Item name="smtpPort" label={t('mail.accounts.port')} rules={[{ required: true }]}>
        <InputNumber min={1} max={65535} className="w-full" />
      </Form.Item>
    </div>
  )
}
