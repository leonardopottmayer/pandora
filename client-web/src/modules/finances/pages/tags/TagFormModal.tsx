import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, ColorPicker, Form, Input, Modal } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TagDto } from '../../models'
import { useCreateTag, useUpdateTag } from '../../hooks/useTags'

interface TagFormModalProps {
  open: boolean
  tag?: TagDto | null
  onClose: () => void
}

interface TagFormValues {
  name: string
  color?: string
}

export function TagFormModal({ open, tag, onClose }: TagFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<TagFormValues>()
  const isEdit = !!tag

  const createMutation = useCreateTag()
  const updateMutation = useUpdateTag()
  const pending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    if (!open) return
    if (tag) form.setFieldsValue({ name: tag.name, color: tag.color ?? undefined })
    else form.resetFields()
  }, [open, tag, form])

  async function handleSubmit(values: TagFormValues) {
    const body = { name: values.name, color: values.color ?? null }
    try {
      if (isEdit && tag) {
        await updateMutation.mutateAsync({ id: tag.id, body })
        message.success(t('finances.tags.updated'))
      } else {
        await createMutation.mutateAsync(body)
        message.success(t('finances.tags.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.tags.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={isEdit ? t('finances.tags.editTitle') : t('finances.tags.newTitle')}
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
          label={t('finances.tags.name')}
          rules={[{ required: true, message: t('finances.tags.nameRequired') }]}
        >
          <Input maxLength={60} />
        </Form.Item>
        <Form.Item
          name="color"
          label={t('finances.tags.color')}
          getValueFromEvent={(_, hex: string) => hex}
        >
          <ColorPicker format="hex" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
