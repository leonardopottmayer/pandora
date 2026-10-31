import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Popconfirm, Space, Tooltip } from 'antd'
import type { MenuProps } from 'antd'
import {
  CheckOutlined,
  DeleteOutlined,
  EyeOutlined,
  FolderOutlined,
  InboxOutlined,
  MailOutlined,
  StarFilled,
  StarOutlined,
  TagOutlined,
} from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  MAX_MESSAGES_PER_ACTION,
  canMoveTo,
  hasMultipleLabels,
  type MailLabel,
  type Mailbox,
  type MessageAction,
  type MessageSummary,
} from '../models'
import { useLabels, useMessageAction } from '../hooks/useMail'

function chunks<T>(items: T[], size: number): T[][] {
  const result: T[][] = []
  for (let i = 0; i < items.length; i += size) result.push(items.slice(i, i + size))
  return result
}

/**
 * What can be done to these messages. Each goes to the provider first, so nothing is updated optimistically: the row
 * changes when the provider has accepted. A selection in several mailboxes becomes one request per mailbox, and
 * moving and labels, which belong to one mailbox, are not offered for it.
 */
export function MailActions({
  messages,
  mailboxes,
  onDone,
}: {
  messages: MessageSummary[]
  mailboxes: Mailbox[]
  onDone?: (action: MessageAction, ids: string[]) => void
}) {
  const { t } = useTranslation()
  const { message: toast } = App.useApp()
  const apply = useMessageAction()

  const mailboxIds = [...new Set(messages.map((m) => m.mailboxId))]
  const onlyMailbox = mailboxIds.length === 1 ? mailboxes.find((m) => m.id === mailboxIds[0]) : undefined
  const [labelsQuery] = useLabels(onlyMailbox ? [onlyMailbox.id] : [])
  const labels: MailLabel[] = labelsQuery?.data ?? []

  const title = (l: MailLabel) => (l.role ? t(`mail.role.${l.role}`) : l.name)

  async function run(action: MessageAction, labelId?: string) {
    try {
      const requests = mailboxIds.flatMap((mailboxId) =>
        chunks(
          messages.filter((m) => m.mailboxId === mailboxId).map((m) => m.id),
          MAX_MESSAGES_PER_ACTION,
        ).map((messageIds) => apply.mutateAsync({ mailboxId, messageIds, action, labelId })),
      )
      await Promise.all(requests)
      onDone?.(action, messages.map((m) => m.id))
    } catch (err) {
      toast.error(toErrorMessage(err, t('mail.actions.error')))
    }
  }

  const moveItems: MenuProps['items'] = labels.filter(canMoveTo).map((l) => ({ key: l.id, label: title(l) }))

  const hasLabel = (labelId: string) => messages.every((m) => m.labelIds.includes(labelId))
  const labelItems: MenuProps['items'] = labels
    .filter((l) => l.kind === 'user')
    .map((l) => ({ key: l.id, label: l.name, icon: hasLabel(l.id) ? <CheckOutlined /> : <span /> }))

  const iconButton = (label: string, icon: React.ReactNode, onClick?: () => void) => (
    <Tooltip title={label}>
      <Button size="small" aria-label={label} icon={icon} disabled={apply.isPending} onClick={onClick} />
    </Tooltip>
  )

  return (
    <Space size={4} wrap>
      {messages.some((m) => m.isUnread) &&
        iconButton(t('mail.actions.markRead'), <EyeOutlined />, () => void run('mark-read'))}
      {messages.some((m) => !m.isUnread) &&
        iconButton(t('mail.actions.markUnread'), <MailOutlined />, () => void run('mark-unread'))}
      {messages.some((m) => !m.isStarred) &&
        iconButton(t('mail.actions.star'), <StarOutlined />, () => void run('star'))}
      {messages.some((m) => m.isStarred) &&
        iconButton(t('mail.actions.unstar'), <StarFilled />, () => void run('unstar'))}
      {iconButton(t('mail.actions.archive'), <InboxOutlined />, () => void run('archive'))}
      <Popconfirm
        title={t('mail.actions.trashConfirm', { count: messages.length })}
        okText={t('mail.actions.trash')}
        onConfirm={() => run('trash')}
      >
        {iconButton(t('mail.actions.trash'), <DeleteOutlined />)}
      </Popconfirm>
      {onlyMailbox && moveItems.length > 0 && (
        <Dropdown trigger={['click']} menu={{ items: moveItems, onClick: ({ key }) => void run('move', key) }}>
          {iconButton(t('mail.actions.move'), <FolderOutlined />)}
        </Dropdown>
      )}
      {onlyMailbox && hasMultipleLabels(onlyMailbox.provider) && labelItems.length > 0 && (
        <Dropdown
          trigger={['click']}
          menu={{
            items: labelItems,
            onClick: ({ key }) => void run(hasLabel(key) ? 'remove-label' : 'add-label', key),
          }}
        >
          {iconButton(t('mail.actions.label'), <TagOutlined />)}
        </Dropdown>
      )}
    </Space>
  )
}
