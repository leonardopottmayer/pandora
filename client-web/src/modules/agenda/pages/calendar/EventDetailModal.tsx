import { useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, App, Button, Checkbox, DatePicker, Form, Input, Modal, Select, Typography, theme } from 'antd'
import {
  AlignLeftOutlined,
  BellOutlined,
  CalendarOutlined,
  EnvironmentOutlined,
  LinkOutlined,
  RetweetOutlined,
} from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import type { CalendarDto, EventOccurrenceDto, EventScope } from '../../models'
import { formatEventRange } from '../../lib/datetime'
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
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const { timeZone } = usePreferences()
  const [form] = Form.useForm<EditValues>()
  const [editing, setEditing] = useState(false)
  const [scope, setScope] = useState<EventScope>('this')
  const [choosingDeleteScope, setChoosingDeleteScope] = useState(false)
  const [wasOpen, setWasOpen] = useState(open)

  // Reset view/scope each time the modal opens — adjusting state on the prop change.
  if (open !== wasOpen) {
    setWasOpen(open)
    if (open) {
      setEditing(false)
      setScope('this')
      setChoosingDeleteScope(false)
    }
  }

  // The series row carries the rrule the occurrence read omits — drives scope awareness.
  const { data: series, isLoading: seriesLoading, isError: seriesError, error: seriesFailure } = useEvent(open ? (occurrence?.eventId ?? null) : null)
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

  async function handleDelete(deleteScope: EventScope) {
    if (!occurrence) return
    try {
      await deleteMutation.mutateAsync({
        id: occurrence.eventId,
        scope: deleteScope,
        occurrenceStart: occurrence.originalStartsAt,
      })
      message.success(t('agenda.events.deleted'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.events.deleteError')))
    }
  }

  // A repeating event asks which occurrences when deleting; a single one just confirms.
  function startDelete() {
    if (isRecurring) {
      setChoosingDeleteScope(true)
      return
    }
    modal.confirm({
      title: t('agenda.events.deleteConfirm'),
      content: occurrence?.title,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => handleDelete('all'),
    })
  }

  const scopeOptions: { value: EventScope; label: string }[] = [
    { value: 'this', label: t('agenda.events.scopeThis') },
    { value: 'this-and-future', label: t('agenda.events.scopeThisAndFuture') },
    { value: 'all', label: t('agenda.events.scopeAll') },
  ]

  const color = calendars.find((c) => c.id === occurrence.calendarId)?.color ?? token.colorPrimary
  const eventCalendar = calendars.find((c) => c.id === occurrence.calendarId)
  // A read-only Google calendar is written by the sync only: its events are edited in Google.
  const readOnly = eventCalendar?.isReadOnly === true
  // An event in a Google calendar stays in it, and none moves into one.
  const movableTo = eventCalendar?.origin === 'External' ? [eventCalendar] : calendars.filter((c) => c.origin !== 'External')
  const line = (icon: ReactNode, content: ReactNode) => (
    <div className="flex items-start gap-3">
      <span className="w-4 shrink-0 pt-0.5 text-center" style={{ color: token.colorTextTertiary }}>
        {icon}
      </span>
      <div className="min-w-0 flex-1 break-words">{content}</div>
    </div>
  )

  return (
    <Modal
      open={open}
      title={editing ? t('agenda.events.editTitle') : null}
      onCancel={onClose}
      footer={editing ? <>
        <Button onClick={() => setEditing(false)}>{t('common.cancel')}</Button>
        <Button type="primary" onClick={() => form.submit()} loading={updateMutation.isPending}>{t('common.save')}</Button>
      </> : null}
      destroyOnHidden
      width={640}
      styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
    >
      {seriesError && <Alert type="error" showIcon title={seriesFailure.message} className="mb-3" />}
      {!editing && (
        <div className="flex flex-col gap-3 pt-1">
          <div className="flex items-start gap-3 pe-6">
            <span className="mt-2 size-3.5 shrink-0 rounded" style={{ background: color }} />
            <div className="flex min-w-0 flex-col">
              <Typography.Title level={4} className="!mb-0 break-words">
                {occurrence.title}
              </Typography.Title>
              <Typography.Text type="secondary">
                {formatEventRange(occurrence.startsAt, occurrence.endsAt, occurrence.isAllDay, timeZone, i18n.language)}
                {isRecurring && (
                  <span title={t('agenda.events.repeats')}>
                    {' · '}
                    <RetweetOutlined aria-label={t('agenda.events.repeats')} />
                  </span>
                )}
              </Typography.Text>
            </div>
          </div>

          {line(<CalendarOutlined />, calendarName)}
          {occurrence.location && line(<EnvironmentOutlined />, occurrence.location)}
          {occurrence.url &&
            line(
              <LinkOutlined />,
              <Typography.Link href={occurrence.url} target="_blank" rel="noreferrer">
                {occurrence.url}
              </Typography.Link>,
            )}
          {occurrence.description &&
            line(<AlignLeftOutlined />, <span className="whitespace-pre-wrap">{occurrence.description}</span>)}
          {line(<BellOutlined />, <AlertsEditor subjectType="event" subjectId={occurrence.eventId} />)}

          {readOnly ? (
            <Alert
              type="info"
              showIcon
              title={t('agenda.google.readOnlyEvent')}
              action={
                <Typography.Link href="https://calendar.google.com/" target="_blank" rel="noreferrer">
                  {t('agenda.google.openGoogle')}
                </Typography.Link>
              }
            />
          ) : choosingDeleteScope ? (
            <div className="flex flex-col gap-2 rounded-lg p-3" style={{ background: token.colorFillQuaternary }}>
              <Typography.Text strong>{t('agenda.events.deleteWhich')}</Typography.Text>
              <div className="flex flex-wrap gap-2">
                {scopeOptions.map((option) => (
                  <Button
                    key={option.value}
                    danger
                    loading={deleteMutation.isPending && deleteMutation.variables?.scope === option.value}
                    onClick={() => handleDelete(option.value)}
                  >
                    {option.label}
                  </Button>
                ))}
                <Button type="text" onClick={() => setChoosingDeleteScope(false)}>
                  {t('common.cancel')}
                </Button>
              </div>
            </div>
          ) : (
            <div className="flex justify-end gap-2 pt-1">
              <Button danger disabled={seriesLoading || seriesError} onClick={startDelete}>
                {t('common.delete')}
              </Button>
              <Button type="primary" disabled={seriesLoading || seriesError} onClick={() => setEditing(true)}>
                {t('common.edit')}
              </Button>
            </div>
          )}
        </div>
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
          <Form.Item name="calendarId" label={t('agenda.events.calendar')} rules={[{ required: true, message: t('agenda.events.calendarRequired') }]}>
            <Select options={movableTo.map((c) => ({ value: c.id, label: c.name }))} />
          </Form.Item>
          <Form.Item name="range" label={t('agenda.events.startsAt')} rules={[{ required: true, message: t('agenda.events.startRequired') }]}>
            <DatePicker.RangePicker format="DD/MM/YYYY HH:mm" showTime={{ format: 'HH:mm' }} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isAllDay" valuePropName="checked">
            <Checkbox>{t('agenda.events.isAllDay')}</Checkbox>
          </Form.Item>
          <details open={!!(occurrence.location || occurrence.url || occurrence.description) || undefined}>
            <summary className="mb-3 w-fit cursor-pointer select-none text-sm">{t('agenda.events.moreOptions')}</summary>
            <Form.Item name="location" label={t('agenda.events.location')}>
              <Input maxLength={200} />
            </Form.Item>
            <Form.Item name="url" label={t('agenda.events.url')}>
              <Input maxLength={500} />
            </Form.Item>
            <Form.Item name="description" label={t('agenda.events.description')}>
              <Input.TextArea rows={2} maxLength={2000} />
            </Form.Item>
          </details>
          {isRecurring && (
            <Form.Item label={t('agenda.events.scopeTitle')}>
              <Select<EventScope> value={scope} onChange={setScope} options={scopeOptions} aria-label={t('agenda.events.scopeTitle')} />
            </Form.Item>
          )}
        </Form>
      )}
    </Modal>
  )
}
