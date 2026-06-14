import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, AutoComplete, Form, Input, InputNumber, Modal } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CardDto } from '../../models'
import { COMMON_CURRENCIES } from '../../lib/enums'
import { AccountSelect } from '../../components/AccountSelect'
import { useCreateCard, useUpdateCard } from '../../hooks/useCards'

interface CardFormModalProps {
  open: boolean
  card?: CardDto | null
  onClose: () => void
}

interface CardFormValues {
  name: string
  brand?: string
  lastFour?: string
  creditLimit?: number
  closingDay: number
  dueDay: number
  currency: string
  defaultPaymentAccountId?: string
}

export function CardFormModal({ open, card, onClose }: CardFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<CardFormValues>()
  const isEdit = !!card

  const createMutation = useCreateCard()
  const updateMutation = useUpdateCard()
  const pending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    if (!open) return
    if (card) {
      form.setFieldsValue({
        name: card.name,
        brand: card.brand ?? undefined,
        lastFour: card.lastFour ?? undefined,
        creditLimit: card.creditLimit ?? undefined,
        closingDay: card.closingDay,
        dueDay: card.dueDay,
        currency: card.currency,
        defaultPaymentAccountId: card.defaultPaymentAccountId ?? undefined,
      })
    } else {
      form.resetFields()
      form.setFieldsValue({ currency: 'BRL', closingDay: 1, dueDay: 10 })
    }
  }, [open, card, form])

  async function handleSubmit(values: CardFormValues) {
    try {
      if (isEdit && card) {
        await updateMutation.mutateAsync({
          id: card.id,
          body: {
            name: values.name,
            brand: values.brand ?? null,
            lastFour: values.lastFour ?? null,
            creditLimit: values.creditLimit ?? null,
            closingDay: values.closingDay,
            dueDay: values.dueDay,
            defaultPaymentAccountId: values.defaultPaymentAccountId ?? null,
          },
        })
        message.success(t('finances.cards.updated'))
      } else {
        await createMutation.mutateAsync({
          name: values.name,
          brand: values.brand ?? null,
          lastFour: values.lastFour ?? null,
          creditLimit: values.creditLimit ?? null,
          closingDay: values.closingDay,
          dueDay: values.dueDay,
          currency: values.currency,
          defaultPaymentAccountId: values.defaultPaymentAccountId ?? null,
        })
        message.success(t('finances.cards.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.cards.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={isEdit ? t('finances.cards.editTitle') : t('finances.cards.newTitle')}
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
          label={t('finances.cards.name')}
          rules={[{ required: true, message: t('finances.cards.nameRequired') }]}
        >
          <Input maxLength={120} />
        </Form.Item>

        <Form.Item name="brand" label={t('finances.cards.brand')}>
          <Input maxLength={60} />
        </Form.Item>

        <Form.Item
          name="lastFour"
          label={t('finances.cards.lastFour')}
          rules={[{ len: 4, pattern: /^\d{4}$/, message: t('finances.cards.lastFourInvalid'), required: false }]}
        >
          <Input maxLength={4} />
        </Form.Item>

        <Form.Item name="creditLimit" label={t('finances.cards.creditLimit')}>
          <InputNumber style={{ width: '100%' }} min={0} step={100} />
        </Form.Item>

        <Form.Item
          name="closingDay"
          label={t('finances.cards.closingDay')}
          rules={[{ required: true, message: t('finances.cards.dayRequired') }]}
        >
          <InputNumber style={{ width: '100%' }} min={1} max={28} />
        </Form.Item>

        <Form.Item
          name="dueDay"
          label={t('finances.cards.dueDay')}
          rules={[{ required: true, message: t('finances.cards.dayRequired') }]}
        >
          <InputNumber style={{ width: '100%' }} min={1} max={28} />
        </Form.Item>

        <Form.Item
          name="currency"
          label={t('finances.cards.currency')}
          rules={[{ required: true, message: t('finances.accounts.currencyRequired') }]}
          normalize={(value: string) => value?.toUpperCase()}
          tooltip={isEdit ? t('finances.accounts.currencyLocked') : undefined}
        >
          <AutoComplete disabled={isEdit} options={COMMON_CURRENCIES.map((c) => ({ value: c }))} />
        </Form.Item>

        <Form.Item name="defaultPaymentAccountId" label={t('finances.cards.defaultPaymentAccount')}>
          <AccountSelect />
        </Form.Item>
      </Form>
    </Modal>
  )
}
