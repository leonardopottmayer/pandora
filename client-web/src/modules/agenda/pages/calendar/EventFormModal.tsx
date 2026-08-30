import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Checkbox, DatePicker, Form, Input, Modal, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CalendarDto } from '../../models'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { RecurrencePicker } from '../../components/RecurrencePicker'
import { useCreateEvent } from '../../hooks/useEvents'

interface EventFormModalProps {
  open: boolean
  calendars: CalendarDto[]
  /** Pre-selected day/time when creating from a calendar cell. */
  defaultStart?: Dayjs | null
  onClose: () => void
}

interface EventFormValues {
  calendarId: string
  title: string
  range: [Dayjs, Dayjs]
  isAllDay: boolean
  description?: string
  location?: string
  url?: string
}

export function EventFormModal({ open, calendars, defaultStart, onClose }: EventFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { timeZone } = usePreferences()
  const [form] = Form.useForm<EventFormValues>()
  const [rrule, setRrule] = useState<string | null>(null)
  const [wasOpen, setWasOpen] = useState(open)
  const createMutation = useCreateEvent()

  const defaultCalendarId = calendars.find((c) => c.isDefault)?.id ?? calendars[0]?.id

  // Reset the recurrence on open — adjusting state on the prop change, not in an effect.
  if (open !== wasOpen) {
    setWasOpen(open)
    if (open) setRrule(null)
  }

  useEffect(() => {
    if (!open) return
    form.resetFields()
    const start = defaultStart ?? dayjs().minute(0).second(0)
    form.setFieldsValue({
      calendarId: defaultCalendarId,
      isAllDay: false,
      range: [start, start.add(1, 'hour')],
    })
  }, [open, defaultStart, defaultCalendarId, form])

  async function handleSubmit(values: EventFormValues) {
    try {
      const [start, end] = values.range
      await createMutation.mutateAsync({
        calendarId: values.calendarId,
        title: values.title,
        startsAt: start.toISOString(),
        endsAt: end.toISOString(),
        isAllDay: values.isAllDay,
        description: values.description ?? null,
        location: values.location ?? null,
        url: values.url ?? null,
        timeZone,
        rrule,
      })
      message.success(t('agenda.events.created'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.events.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('agenda.events.newTitle')}
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
          label={t('agenda.events.titleLabel')}
          rules={[{ required: true, message: t('agenda.events.titleRequired') }]}
        >
          <Input maxLength={200} />
        </Form.Item>

        <Form.Item
          name="calendarId"
          label={t('agenda.events.calendar')}
          rules={[{ required: true, message: t('agenda.events.calendarRequired') }]}
        >
          <Select options={calendars.map((c) => ({ value: c.id, label: c.name }))} />
        </Form.Item>

        <Form.Item
          name="range"
          label={t('agenda.events.startsAt')}
          rules={[{ required: true, message: t('agenda.events.startRequired') }]}
        >
          <DatePicker.RangePicker showTime style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="isAllDay" valuePropName="checked">
          <Checkbox>{t('agenda.events.isAllDay')}</Checkbox>
        </Form.Item>

        <Form.Item name="location" label={t('agenda.events.location')}>
          <Input maxLength={200} />
        </Form.Item>

        <Form.Item name="url" label={t('agenda.events.url')}>
          <Input maxLength={500} />
        </Form.Item>

        <Form.Item name="description" label={t('agenda.events.description')}>
          <Input.TextArea rows={2} maxLength={2000} />
        </Form.Item>

        <Form.Item label={t('agenda.recurrence.label')}>
          <RecurrencePicker value={rrule} onChange={setRrule} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
