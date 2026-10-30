import { useTranslation } from 'react-i18next'
import { Badge, Menu } from 'antd'
import type { MenuProps } from 'antd'
import type { MailLabel, Mailbox } from '../models'
import { useLabels } from '../hooks/useMail'

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
  const labels = useLabels(mailboxes.map((m) => m.id))

  const labelName = (l: MailLabel) => (l.role ? t(`mail.role.${l.role}`) : l.name)
  const withCount = (text: string, unread: number) => (
    <span className="flex items-center justify-between gap-2">
      <span className="truncate">{text}</span>
      {unread > 0 && <Badge count={unread} overflowCount={999} color="blue" />}
    </span>
  )

  const items: MenuProps['items'] = [
    { key: 'all', label: t('mail.allMailboxes') },
    ...mailboxes.map((m, index) => ({
      type: 'group' as const,
      key: `group:${m.id}`,
      label: m.displayName ?? m.address,
      children: [
        { key: `mailbox:${m.id}`, label: t('mail.allMail') },
        ...(labels[index]?.data ?? []).map((l) => ({
          key: `label:${m.id}:${l.id}`,
          label: withCount(labelName(l), l.unread),
        })),
      ],
    })),
  ]

  return (
    <Menu
      mode="inline"
      className="!border-0"
      selectedKeys={[folderKey(folder)]}
      onClick={({ key }) => onSelect(parseKey(key))}
      items={items}
    />
  )
}
