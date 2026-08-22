import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Checkbox, Form, Input, Modal } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TaskListDto } from '../../models'
import { useCreateTaskList, useUpdateTaskList } from '../../hooks/useTaskLists'

interface TaskListFormModalProps {
  open: boolean
  list?: TaskListDto | null
  onClose: () => void
}

interface TaskListFormValues {
  name: string
  isDefault: boolean
}

export function TaskListFormModal({ open, list, onClose }: TaskListFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<TaskListFormValues>()
  const isEdit = !!list

  const createMutation = useCreateTaskList()
  const updateMutation = useUpdateTaskList()
  const pending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    if (!open) return
    if (list) {
      form.setFieldsValue({ name: list.name, isDefault: list.isDefault })
    } else {
      form.resetFields()
      form.setFieldsValue({ isDefault: false })
    }
  }, [open, list, form])

  async function handleSubmit(values: TaskListFormValues) {
    try {
      if (isEdit && list) {
        await updateMutation.mutateAsync({
          id: list.id,
          body: { name: values.name, isDefault: values.isDefault },
        })
        message.success(t('agenda.taskLists.updated'))
      } else {
        await createMutation.mutateAsync({ name: values.name, isDefault: values.isDefault })
        message.success(t('agenda.taskLists.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('agenda.taskLists.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={isEdit ? t('agenda.taskLists.editTitle') : t('agenda.taskLists.newTitle')}
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
          label={t('agenda.taskLists.name')}
          rules={[{ required: true, message: t('agenda.taskLists.nameRequired') }]}
        >
          <Input maxLength={120} />
        </Form.Item>
        <Form.Item name="isDefault" valuePropName="checked">
          <Checkbox>{t('agenda.taskLists.default')}</Checkbox>
        </Form.Item>
      </Form>
    </Modal>
  )
}
