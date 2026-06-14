import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, AutoComplete, Form, Input, InputNumber, Modal, Select } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import { ACCOUNT_TYPES, type AccountDto } from '../../models'
import { ACCOUNT_TYPE_META, COMMON_CURRENCIES } from '../../lib/enums'
import { useCreateAccount, useUpdateAccount } from '../../hooks/useAccounts'

interface AccountFormModalProps {
  open: boolean
  /** Conta em edição; ausente = criação. */
  account?: AccountDto | null
  onClose: () => void
}

interface AccountFormValues {
  name: string
  type: AccountDto['type']
  currency: string
  institution?: string
  description?: string
  color?: string
  icon?: string
  displayOrder: number
  openingBalance?: number
}

export function AccountFormModal({ open, account, onClose }: AccountFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<AccountFormValues>()
  const isEdit = !!account

  const createMutation = useCreateAccount()
  const updateMutation = useUpdateAccount()
  const pending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    if (!open) return
    if (account) {
      form.setFieldsValue({
        name: account.name,
        type: account.type,
        currency: account.currency,
        institution: account.institution ?? undefined,
        description: account.description ?? undefined,
        color: account.color ?? undefined,
        icon: account.icon ?? undefined,
        displayOrder: account.displayOrder,
      })
    } else {
      form.resetFields()
      form.setFieldsValue({ type: 'checking', currency: 'BRL', displayOrder: 0 })
    }
  }, [open, account, form])

  async function handleSubmit(values: AccountFormValues) {
    try {
      if (isEdit && account) {
        await updateMutation.mutateAsync({
          id: account.id,
          body: {
            name: values.name,
            type: values.type,
            institution: values.institution ?? null,
            description: values.description ?? null,
            color: values.color ?? null,
            icon: values.icon ?? null,
            displayOrder: values.displayOrder,
          },
        })
        message.success(t('finances.accounts.updated'))
      } else {
        await createMutation.mutateAsync({
          name: values.name,
          type: values.type,
          currency: values.currency,
          institution: values.institution ?? null,
          description: values.description ?? null,
          color: values.color ?? null,
          icon: values.icon ?? null,
          displayOrder: values.displayOrder,
          openingBalance: values.openingBalance ?? null,
        })
        message.success(t('finances.accounts.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.accounts.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={isEdit ? t('finances.accounts.editTitle') : t('finances.accounts.newTitle')}
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
          label={t('finances.accounts.name')}
          rules={[{ required: true, message: t('finances.accounts.nameRequired') }]}
        >
          <Input maxLength={120} />
        </Form.Item>

        <Form.Item
          name="type"
          label={t('finances.accounts.type')}
          rules={[{ required: true }]}
        >
          <Select
            options={ACCOUNT_TYPES.map((type) => ({
              value: type,
              label: t(ACCOUNT_TYPE_META[type].labelKey),
            }))}
          />
        </Form.Item>

        <Form.Item
          name="currency"
          label={t('finances.accounts.currency')}
          rules={[{ required: true, message: t('finances.accounts.currencyRequired') }]}
          normalize={(value: string) => value?.toUpperCase()}
          tooltip={isEdit ? t('finances.accounts.currencyLocked') : undefined}
        >
          <AutoComplete
            disabled={isEdit}
            options={COMMON_CURRENCIES.map((c) => ({ value: c }))}
            filterOption={(input, option) =>
              (option?.value ?? '').toUpperCase().includes(input.toUpperCase())
            }
          />
        </Form.Item>

        {!isEdit && (
          <Form.Item name="openingBalance" label={t('finances.accounts.openingBalance')}>
            <InputNumber style={{ width: '100%' }} step={0.01} />
          </Form.Item>
        )}

        <Form.Item name="institution" label={t('finances.accounts.institution')}>
          <Input maxLength={120} />
        </Form.Item>

        <Form.Item name="description" label={t('finances.accounts.description')}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>

        <Form.Item name="displayOrder" label={t('finances.accounts.displayOrder')} initialValue={0}>
          <InputNumber style={{ width: '100%' }} min={0} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
