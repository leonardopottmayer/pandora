import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Empty, Flex, Popconfirm, Select, Spin, Typography, Upload } from 'antd'
import {
  DeleteOutlined,
  FileImageOutlined,
  FilePdfOutlined,
  InboxOutlined,
  LinkOutlined,
  UploadOutlined,
} from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { AttachmentDto, AttachmentKind, AttachmentOwner } from '../models'
import { ATTACHMENT_KIND_META } from '../lib/enums'
import {
  useAssignAttachment,
  useAttachments,
  useDeleteAttachment,
  useUploadAttachment,
} from '../hooks/useAttachments'
import { openAttachment } from '../services/attachments.service'
import { EnumTag } from './EnumTag'
import { LinkTransactionModal } from './LinkTransactionModal'

const MAX_BYTES = 25 * 1024 * 1024
const KINDS: AttachmentKind[] = ['receipt', 'bill', 'invoice', 'other']

function formatSize(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

interface AttachmentsPanelProps {
  /** The transaction, suggestion or statement the files belong to — or the queue. */
  owner: AttachmentOwner
}

/**
 * The files attached to a transaction, a suggestion or a statement: open, remove, add images and PDFs, or
 * take one from the queue of files shared with the bot. On the queue itself, a file is filed under a
 * transaction from here (a suggestion or a statement takes it from its own panel).
 */
export function AttachmentsPanel({ owner }: AttachmentsPanelProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const isQueue = 'queued' in owner
  const { data, isLoading } = useAttachments(owner)
  const { data: queued } = useAttachments({ queued: true })
  const upload = useUploadAttachment(owner)
  const remove = useDeleteAttachment(owner)
  const assign = useAssignAttachment()
  const [kind, setKind] = useState<AttachmentKind>('receipt')
  const [filing, setFiling] = useState<AttachmentDto | null>(null)

  async function handleAssign(id: string, target: AttachmentOwner) {
    try {
      await assign.mutateAsync({ id, owner: target })
      message.success(t('finances.attachments.assigned'))
      setFiling(null)
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.attachments.assignError')))
    }
  }

  async function handleUpload(file: File) {
    if (file.size > MAX_BYTES) {
      message.error(t('finances.attachments.tooLarge', { name: file.name }))
      return
    }
    try {
      await upload.mutateAsync({ kind, file })
      message.success(t('finances.attachments.uploaded', { name: file.name }))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.attachments.uploadError')))
    }
  }

  async function handleOpen(attachment: AttachmentDto) {
    try {
      await openAttachment(attachment)
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.attachments.openError')))
    }
  }

  async function handleDelete(attachment: AttachmentDto) {
    try {
      await remove.mutateAsync(attachment.id)
      message.success(t('finances.attachments.deleted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.attachments.deleteError')))
    }
  }

  return (
    <div>
      <Flex justify="space-between" align="center" wrap gap="small" className="mb-2">
        <Typography.Text strong>
          {t(isQueue ? 'finances.attachments.queueTitle' : 'finances.attachments.title')}
        </Typography.Text>
        <Flex gap="small">
          {!isQueue && !!queued?.length && (
            <Dropdown
              menu={{
                items: queued.map((a) => ({
                  key: a.id,
                  label: a.note ? `${a.fileName} — ${a.note}` : a.fileName,
                })),
                onClick: ({ key }) => void handleAssign(key, owner),
              }}
            >
              <Button size="small" icon={<InboxOutlined />} loading={assign.isPending}>
                {t('finances.attachments.fromQueue', { count: queued.length })}
              </Button>
            </Dropdown>
          )}
          <Select
            size="small"
            value={kind}
            onChange={setKind}
            aria-label={t('finances.attachments.kind')}
            style={{ width: 140 }}
            options={KINDS.map((k) => ({ value: k, label: t(ATTACHMENT_KIND_META[k].labelKey) }))}
          />
          <Upload
            multiple
            accept="image/*,application/pdf"
            showUploadList={false}
            beforeUpload={(file) => {
              void handleUpload(file)
              return false
            }}
          >
            <Button size="small" icon={<UploadOutlined />} loading={upload.isPending}>
              {t('finances.attachments.upload')}
            </Button>
          </Upload>
        </Flex>
      </Flex>

      {isQueue && (
        <Typography.Paragraph type="secondary" className="mb-2">
          {t('finances.attachments.queueHint')}
        </Typography.Paragraph>
      )}

      {isLoading ? (
        <Spin size="small" />
      ) : !data?.length ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('finances.attachments.empty')} />
      ) : (
        <Flex vertical gap={4}>
          {data.map((a) => (
            <Flex key={a.id} align="center" justify="space-between" gap="small">
              <Flex align="center" gap="small" style={{ minWidth: 0 }}>
                {a.contentType === 'application/pdf' ? <FilePdfOutlined /> : <FileImageOutlined />}
                <EnumTag meta={ATTACHMENT_KIND_META[a.kind]} />
                <Typography.Link onClick={() => handleOpen(a)} ellipsis style={{ maxWidth: 260 }}>
                  {a.fileName}
                </Typography.Link>
                {a.note && (
                  <Typography.Text type="secondary" ellipsis style={{ maxWidth: 220 }}>
                    {a.note}
                  </Typography.Text>
                )}
                <Typography.Text type="secondary">{formatSize(a.sizeBytes)}</Typography.Text>
              </Flex>
              <Flex gap={4}>
                {isQueue && (
                  <Button size="small" icon={<LinkOutlined />} onClick={() => setFiling(a)}>
                    {t('finances.attachments.assign')}
                  </Button>
                )}
                <Popconfirm
                  title={t('finances.attachments.deleteConfirm')}
                  okText={t('common.delete')}
                  cancelText={t('common.cancel')}
                  onConfirm={() => handleDelete(a)}
                >
                  <Button size="small" type="text" danger icon={<DeleteOutlined />} aria-label={t('common.delete')} />
                </Popconfirm>
              </Flex>
            </Flex>
          ))}
        </Flex>
      )}

      <LinkTransactionModal
        open={!!filing}
        title={t('finances.attachments.assignTitle', { name: filing?.fileName })}
        okText={t('finances.attachments.assign')}
        loading={assign.isPending}
        onClose={() => setFiling(null)}
        onPick={(transactionId) => filing && handleAssign(filing.id, { transactionId })}
      />
    </div>
  )
}
