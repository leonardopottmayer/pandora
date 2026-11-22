import { useTranslation } from 'react-i18next'
import { App, Form, Input, Modal } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { useReplaceSecret } from '@/modules/integrations/hooks/useIntegrations'
import type { Mailbox } from '../models'
import { useSyncMailbox, useUpdateMailbox } from '../hooks/useMail'
import { ServerFields } from './ConnectMailboxModal'

interface FixForm {
  password?: string
  imapHost?: string
  imapPort?: number
  smtpHost?: string
  smtpPort?: number
}

/**
 * Fixes a Titan or IMAP mailbox: a new password goes to its Integrations account, new IMAP servers are
 * tested and saved, then one cycle runs — which tests the connection and leaves `needs-reconnect`.
 */
export function FixConnectionModal({ mailbox, onClose }: { mailbox: Mailbox | null; onClose: () => void }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<FixForm>()
  const replaceSecret = useReplaceSecret()
  const update = useUpdateMailbox()
  const sync = useSyncMailbox()

  async function handleFinish(values: FixForm) {
    if (!mailbox) return
    try {
      if (values.password) await replaceSecret.mutateAsync({ id: mailbox.externalAccountId, secret: values.password })

      const serverChanged =
        mailbox.provider === 'imap' &&
        (values.imapHost?.trim() !== mailbox.imapHost ||
          values.imapPort !== mailbox.imapPort ||
          values.smtpHost?.trim() !== mailbox.smtpHost ||
          values.smtpPort !== mailbox.smtpPort)
      if (serverChanged)
        await update.mutateAsync({
          id: mailbox.id,
          displayName: mailbox.displayName,
          imapHost: values.imapHost!.trim(),
          imapPort: values.imapPort!,
          smtpHost: values.smtpHost!.trim(),
          smtpPort: values.smtpPort!,
        })

      const synced = await sync.mutateAsync(mailbox.id)
      if (synced.status === 'needs-reconnect' || synced.status === 'error')
        message.error(synced.lastError ?? t('mail.accounts.syncError'))
      else {
        message.success(t('mail.accounts.fixed'))
        onClose()
      }
    } catch (err) {
      message.error(toErrorMessage(err, t('mail.accounts.syncError')))
    }
  }

  return (
    <Modal
      open={mailbox !== null}
      title={mailbox ? t('mail.accounts.fixTitle', { address: mailbox.address }) : undefined}
      okText={t('mail.accounts.fixSave')}
      confirmLoading={replaceSecret.isPending || update.isPending || sync.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form<FixForm>
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        preserve={false}
        initialValues={{
          imapHost: mailbox?.imapHost ?? undefined,
          imapPort: mailbox?.imapPort ?? undefined,
          smtpHost: mailbox?.smtpHost ?? undefined,
          smtpPort: mailbox?.smtpPort ?? undefined,
        }}
      >
        <Form.Item name="password" label={t('mail.accounts.newPassword')} extra={t('mail.accounts.newPasswordHint')}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        {mailbox?.provider === 'imap' && <ServerFields />}
      </Form>
    </Modal>
  )
}
