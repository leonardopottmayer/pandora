import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Dropdown, Empty, Input, Modal, Skeleton, Typography, theme } from 'antd'
import { AlignLeftOutlined, BellOutlined, EllipsisOutlined, PlusOutlined, RetweetOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import type { ReminderDto } from '../../models'
import { REMINDER_STATUS_META } from '../../lib/enums'
import { formatDue } from '../../lib/datetime'
import { EnumTag } from '../../components/EnumTag'
import { SnoozeModal } from '../../components/SnoozeModal'
import { useAcknowledgeReminder, useDeleteReminder, useReminders, useRenameReminder } from '../../hooks/useReminders'
import { ReminderFormModal } from './ReminderFormModal'

/** When a reminder fires next: a snooze moves it. */
const nextAt = (r: ReminderDto) => (r.status === 'Snoozed' && r.snoozedUntil) || r.remindAt

export function RemindersListPage() {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const { timeZone } = usePreferences()
  const { data, isLoading, error: loadError } = useReminders()
  const acknowledge = useAcknowledgeReminder()
  const deleteMutation = useDeleteReminder()
  const rename = useRenameReminder()
  const [modalOpen, setModalOpen] = useState(false)
  const [snoozeId, setSnoozeId] = useState<string | null>(null)
  const [renaming, setRenaming] = useState<ReminderDto | null>(null)
  const [newTitle, setNewTitle] = useState('')

  // Fired and waiting first, then the rest in the order they fire; done ones last, newest first.
  const { waiting, upcoming, closed } = useMemo(() => {
    const all = data ?? []
    const byNext = (a: ReminderDto, b: ReminderDto) => nextAt(a).localeCompare(nextAt(b))
    return {
      waiting: all.filter((r) => r.status === 'Notified').sort(byNext),
      upcoming: all.filter((r) => r.status === 'Scheduled' || r.status === 'Snoozed').sort(byNext),
      closed: all
        .filter((r) => r.status === 'Acknowledged' || r.status === 'Cancelled')
        .sort((a, b) => b.remindAt.localeCompare(a.remindAt)),
    }
  }, [data])

  // The list changing is the feedback; only failures speak up.
  async function handleAcknowledge(reminder: ReminderDto) {
    try {
      await acknowledge.mutateAsync(reminder.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.reminders.saveError')))
    }
  }

  function openRename(reminder: ReminderDto) {
    setRenaming(reminder)
    setNewTitle(reminder.title)
  }

  async function handleRename() {
    const title = newTitle.trim()
    if (!renaming || !title) return
    try {
      if (title !== renaming.title) await rename.mutateAsync({ id: renaming.id, title })
      setRenaming(null)
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.reminders.saveError')))
    }
  }

  function confirmCancel(reminder: ReminderDto) {
    modal.confirm({
      title: t('agenda.reminders.deleteConfirm'),
      content: reminder.title,
      okText: t('agenda.reminders.cancelReminder'),
      okButtonProps: { danger: true },
      cancelText: t('agenda.reminders.keep'),
      onOk: async () => {
        try {
          await deleteMutation.mutateAsync(reminder.id)
        } catch (err) {
          message.error(toErrorMessage(err, t('agenda.reminders.deleteError')))
        }
      },
    })
  }

  function renderRow(reminder: ReminderDto, first: boolean) {
    const fired = reminder.status === 'Notified'
    const closedRow = reminder.status === 'Acknowledged' || reminder.status === 'Cancelled'
    const when = formatDue(nextAt(reminder), true, timeZone, i18n.language)
    const meta = [
      <span key="when">{reminder.status === 'Snoozed' ? t('agenda.reminders.snoozedTo', { when }) : when}</span>,
      reminder.rrule && (
        <span key="rrule" title={t('agenda.reminders.recurring')}>
          <RetweetOutlined aria-label={t('agenda.reminders.recurring')} />
        </span>
      ),
      reminder.notes && (
        <span key="notes" title={reminder.notes}>
          <AlignLeftOutlined aria-label={t('agenda.reminders.notes')} />
        </span>
      ),
    ].filter(Boolean)

    return (
      <div
        key={reminder.id}
        className="flex items-center gap-3 py-2.5 pe-2 ps-4"
        style={{ borderTop: first ? undefined : `1px solid ${token.colorBorderSecondary}` }}
      >
        <BellOutlined
          style={{ color: fired ? token.colorWarning : token.colorTextTertiary }}
          aria-hidden
        />
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <Typography.Text strong={fired} type={closedRow ? 'secondary' : undefined} className="break-words">
            {reminder.title}
          </Typography.Text>
          <span className="flex flex-wrap items-center gap-x-3 text-xs" style={{ color: token.colorTextTertiary }}>
            {meta}
          </span>
        </div>
        {closedRow ? (
          <EnumTag meta={REMINDER_STATUS_META[reminder.status]} />
        ) : (
          <>
            {fired && (
              <Button size="small" type="primary" onClick={() => handleAcknowledge(reminder)}>
                {t('agenda.reminders.acknowledge')}
              </Button>
            )}
            <Dropdown
              trigger={['click']}
              menu={{
                items: [
                  { key: 'snooze', label: t('agenda.reminders.snooze'), onClick: () => setSnoozeId(reminder.id) },
                  { key: 'rename', label: t('agenda.reminders.rename'), onClick: () => openRename(reminder) },
                  { type: 'divider' as const },
                  {
                    key: 'cancel',
                    danger: true,
                    label: t('agenda.reminders.cancelReminder'),
                    onClick: () => confirmCancel(reminder),
                  },
                ],
              }}
            >
              <Button
                size="small"
                type="text"
                icon={<EllipsisOutlined />}
                aria-label={t('agenda.reminders.moreActions', { title: reminder.title })}
              />
            </Dropdown>
          </>
        )}
      </div>
    )
  }

  const section = (title: string, reminders: ReminderDto[]) =>
    reminders.length > 0 && (
      <SettingsSection title={`${title} · ${reminders.length}`}>
        {reminders.map((r, index) => renderRow(r, index === 0))}
      </SettingsSection>
    )

  if (loadError) return <Alert type="error" showIcon title={loadError.message} />

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('agenda.reminders.title')} description={t('agenda.reminders.subtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>
          {t('agenda.reminders.new')}
        </Button>
      </div>

      {isLoading ? (
        <Skeleton active />
      ) : (
        <>
          {waiting.length + upcoming.length === 0 && (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('agenda.reminders.empty')} />
          )}
          {section(t('agenda.reminders.waiting'), waiting)}
          {section(t('agenda.reminders.upcoming'), upcoming)}
          {closed.length > 0 && (
            <details>
              <summary
                className="w-fit cursor-pointer select-none px-0.5 text-xs font-semibold uppercase tracking-wider"
                style={{ color: token.colorTextSecondary }}
              >
                {t('agenda.reminders.closed')} · {closed.length}
              </summary>
              <div className="pt-2.5">
                <SettingsSection>{closed.map((r, index) => renderRow(r, index === 0))}</SettingsSection>
              </div>
            </details>
          )}
        </>
      )}

      <ReminderFormModal open={modalOpen} onClose={() => setModalOpen(false)} />
      <SnoozeModal open={!!snoozeId} reminderId={snoozeId} onClose={() => setSnoozeId(null)} />
      <Modal
        open={!!renaming}
        title={t('agenda.reminders.rename')}
        okText={t('common.save')}
        cancelText={t('common.cancel')}
        okButtonProps={{ loading: rename.isPending, disabled: !newTitle.trim() }}
        onOk={handleRename}
        onCancel={() => setRenaming(null)}
        destroyOnHidden
      >
        <Input
          autoFocus
          value={newTitle}
          maxLength={200}
          aria-label={t('agenda.reminders.titleLabel')}
          onChange={(e) => setNewTitle(e.target.value)}
          onPressEnter={handleRename}
        />
      </Modal>
    </div>
  )
}
