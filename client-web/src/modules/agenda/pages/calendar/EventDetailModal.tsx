import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  App,
  Button,
  Checkbox,
  DatePicker,
  Descriptions,
  Divider,
  Form,
  Input,
  Modal,
  Popconfirm,
  Segmented,
  Select,
  Space,
  Typography,
} from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CalendarDto, EventOccurrenceDto, EventScope } from '../../models'
import { formatDateTime } from '../../lib/datetime'
import { AlertsEditor } from '../../components/AlertsEditor'
import { useDeleteEvent, useEvent, useUpdateEvent } from '../../hooks/useEvents'

interface EventDetailModalProps {
  open: boolean
  occurrence: EventOccurrenceDto | null
  calendars: CalendarDto[]
  onClose: () => void
}

interface EditValues {
  title: string
  range: [Dayjs, Dayjs]
  isAllDay: boolean
  calendarId: string
  location?: string
  url?: string
  description?: string
}

export function EventDetailModal({ open, occurrence, calendars, onClose }: EventDetailModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<EditValues>()
  const [editing, setEditing] = useState(false)
  const [scope, setScope] = useState<EventScope>('this')
  const [wasOpen, setWasOpen] = useState(open)

  // Reset view/scope each time the modal opens — adjusting state on the prop change.
  if (open !== wasOpen) {
    setWasOpen(open)
    if (open) {
      setEditing(false)
      setScope('this')
    }
  }

  // The series row carries the rrule the occurrence read omits — drives scope awareness.
  const { data: series } = useEvent(open ? (occurrence?.eventId ?? null) : null)
  const isRecurring = !!series?.rrule
  const updateMutation = useUpdateEvent()
  const deleteMutation = useDeleteEvent()

  useEffect(() => {
    if (editing && occurrence) {
      form.setFieldsValue({
        title: occurrence.title,
        range: [dayjs(occurrence.startsAt), dayjs(occurrence.endsAt)],
        isAllDay: occurrence.isAllDay,
        calendarId: occurrence.calendarId,
        location: occurrence.location ?? undefined,
        url: occurrence.url ?? undefined,
        description: occurrence.description ?? undefined,
      })
    }
  }, [editing, occurrence, form])

  if (!occurrence) return null
  const calendarName = calendars.find((c) => c.id === occurrence.calendarId)?.name

  async function handleUpdate(values: EditValues) {
    if (!occurrence) return
    try {
      const [start, end] = values.range
      await updateMutation.mutateAsync({
        id: occurrence.eventId,
        scope: isRecurring ? scope : 'all',
        occurrenceStart: occurrence.originalStartsAt,
        body: {
          title: values.title,
          startsAt: start.toISOString(),
          endsAt: end.toISOString(),
          isAllDay: values.isAllDay,
          calendarId: values.calendarId,
          location: values.location ?? null,
          url: values.url ?? null,
          description: values.description ?? null,
        },
      })
      message.success(t('agenda.events.updated'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.events.saveError')))
    }
  }

  async function handleDelete() {
    if (!occurrence) return
    try {
      await deleteMutation.mutateAsync({
        id: occurrence.eventId,
        scope: isRecurring ? scope : 'all',
        occurrenceStart: occurrence.originalStartsAt,
      })
      message.success(t('agenda.events.deleted'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.events.deleteError')))
    }
  }

  const scopeOptions: { value: EventScope; label: string }[] = [
    { value: 'this', label: t('agenda.events.scopeThis') },
    { value: 'this-and-future', label: t('agenda.events.scopeThisAndFuture') },
    { value: 'all', label: t('agenda.events.scopeAll') },
  ]

  return (
    <Modal
      open={open}
      title={editing ? t('agenda.events.editTitle') : t('agenda.events.detailTitle')}
      onCancel={onClose}
      footer={null}
      destroyOnHidden
    >
      {isRecurring && (
        <Space direction="vertical" size={4} className="mb-2" style={{ width: '100%' }}>
          <Typography.Text type="secondary">{t('agenda.events.scopeTitle')}</Typography.Text>
          <Segmented<EventScope>
            value={scope}
            onChange={setScope}
            options={scopeOptions}
          />
        </Space>
      )}

      {!editing && (
        <>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={t('agenda.events.titleLabel')}>
              {occurrence.title}
            </Descriptions.Item>
            <Descriptions.Item label={t('agenda.events.calendar')}>{calendarName}</Descriptions.Item>
            <Descriptions.Item label={t('agenda.events.startsAt')}>
              {formatDateTime(occurrence.startsAt)}
            </Descriptions.Item>
            <Descriptions.Item label={t('agenda.events.endsAt')}>
              {formatDateTime(occurrence.endsAt)}
            </Descriptions.Item>
            {occurrence.location && (
              <Descriptions.Item label={t('agenda.events.location')}>
                {occurrence.location}
              </Descriptions.Item>
            )}
            {occurrence.description && (
              <Descriptions.Item label={t('agenda.events.description')}>
                {occurrence.description}
              </Descriptions.Item>
            )}
          </Descriptions>

          <Space className="mt-4">
            <Button type="primary" onClick={() => setEditing(true)}>
              {t('common.edit')}
            </Button>
            <Popconfirm
              title={t('agenda.events.deleteConfirm')}
              okText={t('common.delete')}
              cancelText={t('common.cancel')}
              onConfirm={handleDelete}
            >
              <Button danger loading={deleteMutation.isPending}>
                {t('common.delete')}
              </Button>
            </Popconfirm>
          </Space>

          <Divider style={{ margin: '16px 0 8px' }} />
          <Typography.Text strong>{t('agenda.events.alerts')}</Typography.Text>
          <AlertsEditor subjectType="event" subjectId={occurrence.eventId} />
        </>
      )}

      {editing && (
        <Form form={form} layout="vertical" onFinish={handleUpdate}>
          <Form.Item
            name="title"
            label={t('agenda.events.titleLabel')}
            rules={[{ required: true, message: t('agenda.events.titleRequired') }]}
          >
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="calendarId" label={t('agenda.events.calendar')}>
            <Select options={calendars.map((c) => ({ value: c.id, label: c.name }))} />
          </Form.Item>
          <Form.Item name="range" label={t('agenda.events.startsAt')}>
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
          <Space>
            <Button type="primary" htmlType="submit" loading={updateMutation.isPending}>
              {t('common.save')}
            </Button>
            <Button onClick={() => setEditing(false)}>{t('common.cancel')}</Button>
          </Space>
        </Form>
      )}
    </Modal>
  )
}
