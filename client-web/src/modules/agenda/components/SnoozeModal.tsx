import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Modal } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { useSnoozeReminder } from '../hooks/useReminders'

interface SnoozeModalProps {
  open: boolean
  reminderId: string | null
  onClose: () => void
}

/** Picks a "snooze until" instant and calls POST /reminders/{id}/snooze. */
export function SnoozeModal({ open, reminderId, onClose }: SnoozeModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [until, setUntil] = useState<Dayjs | null>(() => dayjs().add(1, 'hour'))
  const snoozeMutation = useSnoozeReminder()

  async function handleOk() {
    if (!reminderId || !until) return
    try {
      await snoozeMutation.mutateAsync({ id: reminderId, until: until.toISOString() })
      message.success(t('agenda.reminders.snoozed'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.reminders.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('agenda.reminders.snooze')}
      onCancel={onClose}
      onOk={handleOk}
      okButtonProps={{ loading: snoozeMutation.isPending, disabled: !until }}
      okText={t('agenda.reminders.snooze')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <DatePicker
        showTime
        value={until}
        onChange={setUntil}
        style={{ width: '100%' }}
        aria-label={t('agenda.reminders.snoozeUntil')}
      />
    </Modal>
  )
}
