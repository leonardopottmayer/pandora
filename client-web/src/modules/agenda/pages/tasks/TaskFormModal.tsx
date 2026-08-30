import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Checkbox, DatePicker, Divider, Form, Input, Modal, Select, Typography } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { TASK_PRIORITIES, type TaskDto, type TaskPriority } from '../../models'
import { TASK_PRIORITY_META } from '../../lib/enums'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { RecurrencePicker } from '../../components/RecurrencePicker'
import { AlertsEditor } from '../../components/AlertsEditor'
import { useCreateTask, useUpdateTask } from '../../hooks/useTasks'
import { useTaskLists } from '../../hooks/useTaskLists'

interface TaskFormModalProps {
  open: boolean
  /** Task being edited; absent = create mode. */
  task?: TaskDto | null
  /** Pre-selected list when creating. */
  defaultListId?: string
  /** Parent task id when creating a subtask. */
  parentTaskId?: string | null
  onClose: () => void
}

interface TaskFormValues {
  listId: string
  title: string
  notes?: string
  priority: TaskPriority
  dueAt?: Dayjs | null
  dueHasTime: boolean
}

export function TaskFormModal({
  open,
  task,
  defaultListId,
  parentTaskId,
  onClose,
}: TaskFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { timeZone } = usePreferences()
  const [form] = Form.useForm<TaskFormValues>()
  const [rrule, setRrule] = useState<string | null>(null)
  const [wasOpen, setWasOpen] = useState(open)
  const isEdit = !!task

  // Seed the recurrence on open — adjusting state on the prop change, not in an effect.
  if (open !== wasOpen) {
    setWasOpen(open)
    if (open) setRrule(task?.rrule ?? null)
  }

  const { data: lists } = useTaskLists()
  const createMutation = useCreateTask()
  const updateMutation = useUpdateTask()
  const pending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    if (!open) return
    if (task) {
      form.setFieldsValue({
        listId: task.listId,
        title: task.title,
        notes: task.notes ?? undefined,
        priority: task.priority,
        dueAt: task.dueAt ? dayjs(task.dueAt) : null,
        dueHasTime: task.dueHasTime,
      })
    } else {
      form.resetFields()
      form.setFieldsValue({
        listId: defaultListId,
        priority: 'None',
        dueHasTime: false,
      })
    }
  }, [open, task, defaultListId, form])

  async function handleSubmit(values: TaskFormValues) {
    try {
      if (isEdit && task) {
        await updateMutation.mutateAsync({
          id: task.id,
          body: {
            title: values.title,
            notes: values.notes ?? null,
            priority: values.priority,
            dueAt: values.dueAt ? values.dueAt.toISOString() : null,
            dueHasTime: values.dueHasTime,
          },
        })
        message.success(t('agenda.tasks.updated'))
      } else {
        await createMutation.mutateAsync({
          listId: values.listId,
          title: values.title,
          notes: values.notes ?? null,
          parentTaskId: parentTaskId ?? null,
          priority: values.priority,
          dueAt: values.dueAt ? values.dueAt.toISOString() : null,
          dueHasTime: values.dueHasTime,
          timeZone,
          rrule,
        })
        message.success(t('agenda.tasks.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.tasks.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={isEdit ? t('agenda.tasks.editTitle') : t('agenda.tasks.newTitle')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: pending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        {!isEdit && (
          <Form.Item
            name="listId"
            label={t('agenda.tasks.list')}
            rules={[{ required: true, message: t('agenda.tasks.listRequired') }]}
          >
            <Select
              options={(lists ?? []).map((l) => ({ value: l.id, label: l.name }))}
            />
          </Form.Item>
        )}

        <Form.Item
          name="title"
          label={t('agenda.tasks.titleLabel')}
          rules={[{ required: true, message: t('agenda.tasks.titleRequired') }]}
        >
          <Input maxLength={200} />
        </Form.Item>

        <Form.Item name="priority" label={t('agenda.tasks.priority')}>
          <Select
            options={TASK_PRIORITIES.map((p) => ({
              value: p,
              label: t(TASK_PRIORITY_META[p].labelKey),
            }))}
          />
        </Form.Item>

        <Form.Item name="dueAt" label={t('agenda.tasks.dueAt')}>
          <DatePicker
            showTime={form.getFieldValue('dueHasTime')}
            style={{ width: '100%' }}
          />
        </Form.Item>

        <Form.Item name="dueHasTime" valuePropName="checked">
          <Checkbox>{t('agenda.tasks.dueHasTime')}</Checkbox>
        </Form.Item>

        <Form.Item name="notes" label={t('agenda.tasks.notes')}>
          <Input.TextArea rows={2} maxLength={2000} />
        </Form.Item>

        {!isEdit && (
          <Form.Item label={t('agenda.recurrence.label')}>
            {/* Tasks reject COUNT — no "after N occurrences" option. */}
            <RecurrencePicker value={rrule} onChange={setRrule} allowCount={false} />
          </Form.Item>
        )}

        {isEdit && task && (
          <>
            <Divider style={{ margin: '8px 0' }} />
            <Typography.Text strong>{t('agenda.tasks.alerts')}</Typography.Text>
            <AlertsEditor subjectType="task" subjectId={task.id} />
          </>
        )}
      </Form>
    </Modal>
  )
}
