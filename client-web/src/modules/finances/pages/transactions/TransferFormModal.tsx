import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, Input, InputNumber, Modal } from 'antd'
import type { Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { AccountSelect } from '../../components/AccountSelect'
import { useCreateTransfer } from '../../hooks/useTransactions'

interface TransferFormValues {
  fromAccountId: string
  toAccountId: string
  amountOut: number
  amountIn?: number
  fxRate?: number
  occurredOn: Dayjs
  description: string
  notes?: string
}

interface TransferFormModalProps {
  open: boolean
  onClose: () => void
}

export function TransferFormModal({ open, onClose }: TransferFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<TransferFormValues>()
  const fromAccountId = Form.useWatch('fromAccountId', form)

  const transferMutation = useCreateTransfer()

  useEffect(() => {
    if (!open) return
    form.resetFields()
  }, [open, form])

  async function handleSubmit(values: TransferFormValues) {
    try {
      await transferMutation.mutateAsync({
        fromAccountId: values.fromAccountId,
        toAccountId: values.toAccountId,
        amountOut: values.amountOut,
        amountIn: values.amountIn ?? null,
        fxRate: values.fxRate ?? null,
        occurredOn: values.occurredOn.format('YYYY-MM-DD'),
        description: values.description,
        notes: values.notes ?? null,
      })
      message.success(t('finances.transactions.transferDone'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.transactions.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('finances.transactions.transfer')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: transferMutation.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item
          name="fromAccountId"
          label={t('finances.transactions.fromAccount')}
          rules={[{ required: true, message: t('finances.transactions.accountRequired') }]}
        >
          <AccountSelect />
        </Form.Item>

        <Form.Item
          name="toAccountId"
          label={t('finances.transactions.toAccount')}
          rules={[{ required: true, message: t('finances.transactions.accountRequired') }]}
        >
          <AccountSelect excludeIds={fromAccountId ? [fromAccountId] : []} />
        </Form.Item>

        <Form.Item
          name="amountOut"
          label={t('finances.transactions.amountOut')}
          rules={[{ required: true, message: t('finances.transactions.amountRequired') }]}
        >
          <InputNumber style={{ width: '100%' }} min={0.01} step={0.01} />
        </Form.Item>

        <Form.Item
          name="amountIn"
          label={t('finances.transactions.amountIn')}
          tooltip={t('finances.transactions.amountInHint')}
        >
          <InputNumber style={{ width: '100%' }} min={0.01} step={0.01} />
        </Form.Item>

        <Form.Item
          name="occurredOn"
          label={t('finances.transactions.occurredOn')}
          rules={[{ required: true, message: t('finances.transactions.occurredOnRequired') }]}
        >
          <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
        </Form.Item>

        <Form.Item
          name="description"
          label={t('finances.transactions.description')}
          rules={[{ required: true, message: t('finances.transactions.descriptionRequired') }]}
        >
          <Input maxLength={200} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
