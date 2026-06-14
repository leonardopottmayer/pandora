import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Form, Input, InputNumber, Modal, Select } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { TRANSACTION_NATURES, type TransactionNature, type UserCategoryDto } from '../../models'
import { TRANSACTION_NATURE_META } from '../../lib/enums'
import {
  useCreateUserCategory,
  useUpdateUserCategory,
  useUserCategories,
} from '../../hooks/useCategories'

interface CategoryFormModalProps {
  open: boolean
  category?: UserCategoryDto | null
  onClose: () => void
}

interface CategoryFormValues {
  name: string
  nature: TransactionNature
  parentCategoryId?: string
  color?: string
  icon?: string
  displayOrder: number
}

export function CategoryFormModal({ open, category, onClose }: CategoryFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<CategoryFormValues>()
  const isEdit = !!category
  const nature = Form.useWatch('nature', form)

  const { data: userCategories } = useUserCategories()
  const createMutation = useCreateUserCategory()
  const updateMutation = useUpdateUserCategory()
  const pending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    if (!open) return
    if (category) {
      form.setFieldsValue({
        name: category.name,
        nature: category.nature,
        parentCategoryId: category.parentCategoryId ?? undefined,
        color: category.color ?? undefined,
        icon: category.icon ?? undefined,
        displayOrder: category.displayOrder,
      })
    } else {
      form.resetFields()
      form.setFieldsValue({ nature: 'expense', displayOrder: 0 })
    }
  }, [open, category, form])

  // Candidatos a pai: categorias de topo (sem pai) da mesma natureza.
  const parentOptions = (userCategories ?? [])
    .filter((c) => !c.parentCategoryId && c.nature === nature && c.id !== category?.id)
    .map((c) => ({ value: c.id, label: c.name }))

  async function handleSubmit(values: CategoryFormValues) {
    try {
      if (isEdit && category) {
        await updateMutation.mutateAsync({
          id: category.id,
          body: {
            name: values.name,
            color: values.color ?? null,
            icon: values.icon ?? null,
            displayOrder: values.displayOrder,
          },
        })
        message.success(t('finances.categories.updated'))
      } else {
        await createMutation.mutateAsync({
          name: values.name,
          nature: values.nature,
          parentCategoryId: values.parentCategoryId ?? null,
          color: values.color ?? null,
          icon: values.icon ?? null,
          displayOrder: values.displayOrder,
        })
        message.success(t('finances.categories.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.categories.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={isEdit ? t('finances.categories.editTitle') : t('finances.categories.newTitle')}
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
          label={t('finances.categories.name')}
          rules={[{ required: true, message: t('finances.categories.nameRequired') }]}
        >
          <Input maxLength={120} />
        </Form.Item>

        <Form.Item name="nature" label={t('finances.categories.nature')} rules={[{ required: true }]}>
          <Select
            disabled={isEdit}
            options={TRANSACTION_NATURES.map((n) => ({
              value: n,
              label: t(TRANSACTION_NATURE_META[n].labelKey),
            }))}
          />
        </Form.Item>

        {!isEdit && (
          <Form.Item name="parentCategoryId" label={t('finances.categories.parent')}>
            <Select allowClear options={parentOptions} placeholder={t('finances.categories.noParent')} />
          </Form.Item>
        )}

        <Form.Item name="displayOrder" label={t('finances.accounts.displayOrder')} initialValue={0}>
          <InputNumber style={{ width: '100%' }} min={0} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
