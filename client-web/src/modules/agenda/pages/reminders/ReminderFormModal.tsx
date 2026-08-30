import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, Input, Modal } from 'antd'
import type { Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { RecurrencePicker } from '../../components/RecurrencePicker'
import { useCreateReminder } from '../../hooks/useReminders'

interface ReminderFormModalProps {
  open: boolean
  onClose: () => void
}

interface ReminderFormValues {
  title: string
  notes?: string
  remindAt: Dayjs
}

export function ReminderFormModal({ open, onClose }: ReminderFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { timeZone } = usePreferences()
  const [form] = Form.useForm<ReminderFormValues>()
  const [rrule, setRrule] = useState<string | null>(null)
  const [wasOpen, setWasOpen] = useState(open)
  const createMutation = useCreateReminder()

  // Reset the form on open — adjusting state on the prop change, not in an effect.
  if (open !== wasOpen) {
    setWasOpen(open)
    if (open) {
      form.resetFields()
      setRrule(null)
    }
  }

  async function handleSubmit(values: ReminderFormValues) {
    try {
      await createMutation.mutateAsync({
        title: values.title,
        notes: values.notes ?? null,
        remindAt: values.remindAt.toISOString(),
        timeZone,
        rrule,
      })
      message.success(t('agenda.reminders.created'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.reminders.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('agenda.reminders.newTitle')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: createMutation.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item
          name="title"
          label={t('agenda.reminders.titleLabel')}
          rules={[{ required: true, message: t('agenda.reminders.titleRequired') }]}
        >
          <Input maxLength={200} />
        </Form.Item>

        <Form.Item
          name="remindAt"
          label={t('agenda.reminders.remindAt')}
          rules={[{ required: true, message: t('agenda.reminders.remindAtRequired') }]}
        >
          <DatePicker showTime style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="notes" label={t('agenda.reminders.notes')}>
          <Input.TextArea rows={2} maxLength={1000} />
        </Form.Item>

        <Form.Item label={t('agenda.recurrence.label')}>
          <RecurrencePicker value={rrule} onChange={setRrule} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
