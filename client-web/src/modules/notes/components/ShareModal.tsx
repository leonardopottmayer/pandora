import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Input, Modal, Space, Switch, Typography } from 'antd'
import { CopyOutlined } from '@ant-design/icons'

interface ShareModalProps {
  open: boolean
  publicToken: string | null
  saving: boolean
  onChange: (isPublic: boolean) => void
  onClose: () => void
}

/** The public link of a page: anyone with it reads the page, without signing in. */
export function ShareModal({ open, publicToken, saving, onChange, onClose }: ShareModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const url = publicToken ? `${window.location.origin}/p/${publicToken}` : ''

  async function copy() {
    try {
      await navigator.clipboard.writeText(url)
      message.success(t('notes.blocks.linkCopied'))
    } catch {
      message.error(t('notes.blocks.copyError'))
    }
  }

  return (
    <Modal open={open} title={t('notes.share.title')} onCancel={onClose} footer={null} destroyOnHidden>
      <div className="flex flex-col gap-4">
        <label className="flex cursor-pointer items-center justify-between gap-4">
          <span className="flex flex-col">
            <Typography.Text strong>{t('notes.share.public')}</Typography.Text>
            <Typography.Text type="secondary" className="text-xs">{t('notes.share.publicHint')}</Typography.Text>
          </span>
          <Switch checked={!!publicToken} loading={saving} onChange={onChange} aria-label={t('notes.share.public')} />
        </label>
        {publicToken && (
          <>
            <Space.Compact className="w-full">
              <Input readOnly value={url} aria-label={t('notes.share.link')} onFocus={(event) => event.target.select()} />
              <Button icon={<CopyOutlined />} onClick={() => void copy()}>{t('notes.share.copy')}</Button>
            </Space.Compact>
            <Alert type="info" showIcon title={t('notes.share.note')} />
          </>
        )}
      </div>
    </Modal>
  )
}
