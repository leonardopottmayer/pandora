import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Checkbox, ColorPicker, Form, Input, Modal } from 'antd'
import type { Color } from 'antd/es/color-picker'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CalendarDto } from '../../models'
import { browserTimeZone } from '../../lib/datetime'
import { useCreateCalendar, useUpdateCalendar } from '../../hooks/useCalendars'

interface CalendarFormModalProps {
  open: boolean
  calendar?: CalendarDto | null
  onClose: () => void
}

interface CalendarFormValues {
  name: string
  color?: string | Color
  isDefault: boolean
}

function toHex(color?: string | Color): string | null {
  if (!color) return null
  return typeof color === 'string' ? color : color.toHexString()
}

export function CalendarFormModal({ open, calendar, onClose }: CalendarFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<CalendarFormValues>()
  const isEdit = !!calendar

  const createMutation = useCreateCalendar()
  const updateMutation = useUpdateCalendar()
  const pending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    if (!open) return
    if (calendar) {
      form.setFieldsValue({
        name: calendar.name,
        color: calendar.color ?? undefined,
        isDefault: calendar.isDefault,
      })
    } else {
      form.resetFields()
      form.setFieldsValue({ isDefault: false, color: '#1677ff' })
    }
  }, [open, calendar, form])

  async function handleSubmit(values: CalendarFormValues) {
    try {
      if (isEdit && calendar) {
        await updateMutation.mutateAsync({
          id: calendar.id,
          body: { name: values.name, color: toHex(values.color), isDefault: values.isDefault },
        })
        message.success(t('agenda.calendars.updated'))
      } else {
        await createMutation.mutateAsync({
          name: values.name,
          color: toHex(values.color),
          isDefault: values.isDefault,
          timeZone: browserTimeZone(),
        })
        message.success(t('agenda.calendars.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.calendars.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={isEdit ? t('agenda.calendars.editTitle') : t('agenda.calendars.newTitle')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: pending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item
          name="name"
          label={t('agenda.calendars.name')}
          rules={[{ required: true, message: t('agenda.calendars.nameRequired') }]}
        >
          <Input maxLength={120} />
        </Form.Item>
        <Form.Item name="color" label={t('agenda.calendars.color')}>
          <ColorPicker format="hex" />
        </Form.Item>
        <Form.Item name="isDefault" valuePropName="checked">
          <Checkbox>{t('agenda.calendars.default')}</Checkbox>
        </Form.Item>
      </Form>
    </Modal>
  )
}
