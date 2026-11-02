import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ReactNode } from 'react'
import { App, Button, Dropdown, Form, Input, Menu, Modal } from 'antd'
import type { MenuProps } from 'antd'
import {
  ContainerOutlined,
  DeleteOutlined,
  FileTextOutlined,
  FolderOutlined,
  InboxOutlined,
  MailOutlined,
  MoreOutlined,
  PlusOutlined,
  SendOutlined,
  StopOutlined,
  TagOutlined,
} from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { hasMultipleLabels, type LabelRole, type MailLabel, type Mailbox } from '../models'
import { useCreateLabel, useDeleteLabel, useLabels, useRenameLabel } from '../hooks/useMail'

const ROLE_ICON: Record<LabelRole, ReactNode> = {
  inbox: <InboxOutlined />,
  sent: <SendOutlined />,
  drafts: <FileTextOutlined />,
  trash: <DeleteOutlined />,
  spam: <StopOutlined />,
  archive: <ContainerOutlined />,
}

/** What the list shows: every mailbox, one mailbox, or one label of a mailbox. */
export interface MailFolder {
  mailboxId?: string
  labelId?: string
}

function folderKey(f: MailFolder): string {
  return f.labelId ? `label:${f.mailboxId}:${f.labelId}` : f.mailboxId ? `mailbox:${f.mailboxId}` : 'all'
}

function parseKey(key: string): MailFolder {
  const [kind, mailboxId, labelId] = key.split(':')
  return kind === 'label' ? { mailboxId, labelId } : kind === 'mailbox' ? { mailboxId } : {}
}

/** Creating in a mailbox, or renaming a label. */
type NameEdit = { mailboxId: string; label?: undefined } | { mailboxId: string; label: MailLabel }

export function MailSidebar({
  mailboxes,
  folder,
  onSelect,
}: {
  mailboxes: Mailbox[]
  folder: MailFolder
  onSelect: (folder: MailFolder) => void
}) {
  const { t } = useTranslation()
  const { message: toast, modal } = App.useApp()
  const labels = useLabels(mailboxes.map((m) => m.id))
  const createLabel = useCreateLabel()
  const renameLabel = useRenameLabel()
  const deleteLabel = useDeleteLabel()
  const [editing, setEditing] = useState<NameEdit | null>(null)

  const labelName = (l: MailLabel) => (l.role ? t(`mail.role.${l.role}`) : l.name)

  /** A renamed folder can come back as a new label, and a deleted one is gone: the list falls back to the mailbox. */
  function leaveLabel(mailboxId: string, labelId: string) {
    if (folder.labelId === labelId) onSelect({ mailboxId })
  }

  async function saveName(name: string) {
    if (!editing) return
    try {
      if (editing.label) {
        await renameLabel.mutateAsync({ id: editing.label.id, name })
        leaveLabel(editing.mailboxId, editing.label.id)
      } else {
        await createLabel.mutateAsync({ mailboxId: editing.mailboxId, name })
      }
      setEditing(null)
    } catch (err) {
      toast.error(toErrorMessage(err, t('mail.labels.saveError')))
    }
  }

  function confirmDelete(mailboxId: string, label: MailLabel) {
    modal.confirm({
      title: t('mail.labels.deleteConfirm', { name: label.name }),
      content: t('mail.labels.deleteHint'),
      okText: t('mail.labels.delete'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteLabel.mutateAsync(label.id)
          leaveLabel(mailboxId, label.id)
        } catch (err) {
          toast.error(toErrorMessage(err, t('mail.labels.deleteError')))
        }
      },
    })
  }

  const labelRow = (mailboxId: string, l: MailLabel) => (
    <span className="flex items-center justify-between gap-1">
      <span className="truncate" style={l.unread > 0 ? { fontWeight: 600 } : undefined}>
        {labelName(l)}
      </span>
      <span className="flex shrink-0 items-center gap-1">
        {l.unread > 0 && (
          <span className="text-xs font-semibold tabular-nums" aria-label={t('mail.unreadCount', { count: l.unread })}>
            {l.unread > 999 ? '999+' : l.unread}
          </span>
        )}
        {l.kind === 'user' && (
          // The menu belongs to the row, but opening it must not select the label.
          <span onClick={(e) => e.stopPropagation()}>
            <Dropdown
              trigger={['click']}
              menu={{
                items: [
                  { key: 'rename', label: t('mail.labels.rename') },
                  { key: 'delete', label: t('mail.labels.delete'), danger: true },
                ],
                onClick: ({ key, domEvent }) => {
                  domEvent.stopPropagation()
                  if (key === 'rename') setEditing({ mailboxId, label: l })
                  else confirmDelete(mailboxId, l)
                },
              }}
            >
              <Button
                type="text"
                size="small"
                aria-label={t('mail.labels.menu', { name: l.name })}
                icon={<MoreOutlined />}
              />
            </Dropdown>
          </span>
        )}
      </span>
    </span>
  )

  const items: MenuProps['items'] = [
    ...(mailboxes.length > 1 ? [{ key: 'all', icon: <MailOutlined />, label: t('mail.allMailboxes') }] : []),
    ...mailboxes.map((m, index) => ({
      type: 'group' as const,
      key: `group:${m.id}`,
      label: <span className="block truncate">{m.displayName ?? m.address}</span>,
      children: [
        { key: `mailbox:${m.id}`, icon: <MailOutlined />, label: t('mail.allMail') },
        ...(labels[index]?.data ?? []).map((l) => ({
          key: `label:${m.id}:${l.id}`,
          icon: l.role ? ROLE_ICON[l.role] : hasMultipleLabels(m.provider) ? <TagOutlined /> : <FolderOutlined />,
          label: labelRow(m.id, l),
        })),
        { key: `new:${m.id}`, icon: <PlusOutlined />, label: t('mail.labels.new') },
      ],
    })),
  ]

  return (
    <>
      <Menu
        mode="inline"
        style={{ borderInlineEnd: 0, background: 'transparent' }}
        // With one mailbox there is no "all mailboxes": everything is that mailbox's mail.
        selectedKeys={[mailboxes.length === 1 && !folder.mailboxId ? `mailbox:${mailboxes[0].id}` : folderKey(folder)]}
        onClick={({ key }) => {
          if (key.startsWith('new:')) setEditing({ mailboxId: key.slice('new:'.length) })
          else onSelect(parseKey(key))
        }}
        items={items}
      />
      {editing && (
        <LabelNameModal
          initialName={editing.label?.name}
          saving={createLabel.isPending || renameLabel.isPending}
          onSave={(name) => void saveName(name)}
          onClose={() => setEditing(null)}
        />
      )}
    </>
  )
}

function LabelNameModal({
  initialName,
  saving,
  onSave,
  onClose,
}: {
  initialName?: string
  saving: boolean
  onSave: (name: string) => void
  onClose: () => void
}) {
  const { t } = useTranslation()
  const [form] = Form.useForm<{ name: string }>()

  return (
    <Modal
      open
      title={initialName ? t('mail.labels.rename') : t('mail.labels.new')}
      okText={t('mail.labels.save')}
      confirmLoading={saving}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" initialValues={{ name: initialName }} onFinish={(v) => onSave(v.name.trim())}>
        <Form.Item
          name="name"
          label={t('mail.labels.name')}
          rules={[{ required: true, whitespace: true, max: 100 }]}
        >
          <Input autoFocus maxLength={100} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
