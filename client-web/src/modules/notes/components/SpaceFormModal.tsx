import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, ColorPicker, Form, Input, Modal } from 'antd'
import type { Color } from 'antd/es/color-picker'
import { toErrorMessage } from '@/lib/api/envelope'
import type { SpaceDto } from '../models'
import { useCreateSpace, useUpdateSpace } from '../hooks/useSpaces'
import { PageIconPicker } from './PageIconPicker'

const COLOR_PRESETS = ['#1677ff', '#2dcd88', '#13c2c2', '#722ed1', '#eb2f96', '#fa541c', '#faad14', '#8c8c8c']

interface SpaceFormValues {
  name: string
  icon: string | null
  color: string | Color | null
  description?: string
}

interface SpaceFormModalProps {
  open: boolean
  /** The space being edited; absent = create. */
  space?: SpaceDto | null
  onClose: () => void
  /** Called with the space once saved, e.g. to open a new one. */
  onSaved?: (space: SpaceDto) => void
}

export function SpaceFormModal({ open, space, onClose, onSaved }: SpaceFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<SpaceFormValues>()
  const createSpace = useCreateSpace()
  const updateSpace = useUpdateSpace()
  const pending = createSpace.isPending || updateSpace.isPending

  useEffect(() => {
    if (!open) return
    form.setFieldsValue({
      name: space?.name ?? '',
      icon: space?.icon ?? null,
      color: space?.color ?? null,
      description: space?.description ?? undefined,
    })
  }, [open, space, form])

  async function handleSubmit(values: SpaceFormValues) {
    const color = values.color == null ? null : typeof values.color === 'string' ? values.color : values.color.toHexString()
    const body = {
      name: values.name,
      icon: values.icon,
      color,
      description: values.description?.trim() || null,
    }
    try {
      const saved = space
        ? await updateSpace.mutateAsync({ id: space.id, body })
        : await createSpace.mutateAsync(body)
      onClose()
      onSaved?.(saved)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.spaces.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t(space ? 'notes.spaces.editTitle' : 'notes.spaces.newTitle')}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      okButtonProps={{ loading: pending }}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit} requiredMark={false}>
        <Form.Item label={t('notes.spaces.name')} required>
          <div className="flex items-center gap-2">
            <Form.Item name="icon" noStyle>
              <IconField />
            </Form.Item>
            <Form.Item
              name="name"
              noStyle
              rules={[{ required: true, whitespace: true, message: t('notes.spaces.nameRequired') }]}
            >
              <Input autoFocus maxLength={100} aria-label={t('notes.spaces.name')} />
            </Form.Item>
          </div>
        </Form.Item>
        <Form.Item name="color" label={t('notes.spaces.color')}>
          <ColorPicker
            allowClear
            disabledAlpha
            presets={[{ label: t('notes.spaces.color'), colors: COLOR_PRESETS }]}
          />
        </Form.Item>
        <Form.Item name="description" label={t('notes.spaces.description')}>
          <Input.TextArea maxLength={500} autoSize={{ minRows: 2, maxRows: 5 }} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/** The page icon picker, wired as a form control. */
function IconField({ value, onChange }: { value?: string | null; onChange?: (icon: string | null) => void }) {
  return <PageIconPicker value={value ?? null} onChange={(icon) => onChange?.(icon)} />
}
