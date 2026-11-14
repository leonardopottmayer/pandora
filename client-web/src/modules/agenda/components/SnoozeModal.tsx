import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, DatePicker, Modal, Typography } from 'antd'
import type { Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { formatDue, fromPicker, nowPicker } from '../lib/datetime'
import { useSnoozeReminder } from '../hooks/useReminders'

interface SnoozeModalProps {
  open: boolean
  reminderId: string | null
  onClose: () => void
}

/** Snoozes a reminder by a shortcut, or until a picked instant. */
export function SnoozeModal({ open, reminderId, onClose }: SnoozeModalProps) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const { timeZone } = usePreferences()
  const [until, setUntil] = useState<Dayjs | null>(() => nowPicker(timeZone).add(1, 'hour'))
  const [previous, setPrevious] = useState({ open, reminderId })
  if (open !== previous.open || reminderId !== previous.reminderId) {
    setPrevious({ open, reminderId })
    if (open) setUntil(nowPicker(timeZone).add(1, 'hour'))
  }
  const snoozeMutation = useSnoozeReminder()

  const now = nowPicker(timeZone)
  const shortcuts: { key: string; until: Dayjs }[] = [
    { key: 'in10m', until: now.add(10, 'minute') },
    { key: 'in1h', until: now.add(1, 'hour') },
    { key: 'in3h', until: now.add(3, 'hour') },
    { key: 'tomorrow', until: now.add(1, 'day').hour(9).minute(0).second(0) },
  ]

  async function snooze(to: Dayjs) {
    if (!reminderId) return
    const instant = fromPicker(to, timeZone)
    try {
      await snoozeMutation.mutateAsync({ id: reminderId, until: instant })
      // Says when it comes back, since nothing on screen moves to show it.
      message.success(
        t('agenda.reminders.snoozedTo', { when: formatDue(instant, true, timeZone, i18n.language) }),
      )
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
      onOk={() => until && snooze(until)}
      okButtonProps={{ loading: snoozeMutation.isPending, disabled: !until }}
      okText={t('agenda.reminders.snooze')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <div className="flex flex-col gap-4 pt-1">
        <div className="grid grid-cols-2 gap-2">
          {shortcuts.map((shortcut) => (
            <Button key={shortcut.key} disabled={snoozeMutation.isPending} onClick={() => snooze(shortcut.until)}>
              {t(`agenda.reminders.snoozeShortcuts.${shortcut.key}`)}
            </Button>
          ))}
        </div>
        <div className="flex flex-col gap-1.5">
          <Typography.Text type="secondary">{t('agenda.reminders.snoozeUntil')}</Typography.Text>
          <DatePicker
            format="DD/MM/YYYY HH:mm"
            showTime={{ format: 'HH:mm' }}
            value={until}
            onChange={setUntil}
            style={{ width: '100%' }}
            aria-label={t('agenda.reminders.snoozeUntil')}
          />
        </div>
      </div>
    </Modal>
  )
}
