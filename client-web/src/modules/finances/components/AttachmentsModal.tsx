import { useTranslation } from 'react-i18next'
import { Modal } from 'antd'
import type { AttachmentOwner } from '../models'
import { AttachmentsPanel } from './AttachmentsPanel'

interface AttachmentsModalProps {
  owner: AttachmentOwner | null
  onClose: () => void
}

/** The attachments of a row, opened from a list (the inbox) without leaving the page. */
export function AttachmentsModal({ owner, onClose }: AttachmentsModalProps) {
  const { t } = useTranslation()
  return (
    <Modal
      open={!!owner}
      title={t('finances.attachments.title')}
      onCancel={onClose}
      footer={null}
      destroyOnHidden
    >
      {owner && <AttachmentsPanel owner={owner} />}
    </Modal>
  )
}
