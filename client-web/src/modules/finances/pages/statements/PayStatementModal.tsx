import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, Input, InputNumber, Modal } from 'antd'
import type { Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { AccountSelect } from '../../components/AccountSelect'
import { usePayStatement } from '../../hooks/useStatements'

interface PayStatementValues {
  accountId: string
  amount: number
  occurredOn?: Dayjs
  description?: string
}

interface PayStatementModalProps {
  open: boolean
  statementId: string
  /** Saldo restante sugerido como valor inicial. */
  remainingAmount?: number
  onClose: () => void
}

export function PayStatementModal({ open, statementId, remainingAmount, onClose }: PayStatementModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<PayStatementValues>()
  const payMutation = usePayStatement()

  useEffect(() => {
    if (!open) return
    form.resetFields()
    if (remainingAmount != null) form.setFieldValue('amount', remainingAmount)
  }, [open, remainingAmount, form])

  async function handleSubmit(values: PayStatementValues) {
    try {
      await payMutation.mutateAsync({
        id: statementId,
        body: {
          accountId: values.accountId,
          amount: values.amount,
          occurredOn: values.occurredOn ? values.occurredOn.format('YYYY-MM-DD') : null,
          description: values.description ?? null,
        },
      })
      message.success(t('finances.statements.paid'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.statements.payError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('finances.statements.pay')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: payMutation.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item
          name="accountId"
          label={t('finances.statements.payAccount')}
          rules={[{ required: true, message: t('finances.transactions.accountRequired') }]}
        >
          <AccountSelect />
        </Form.Item>
        <Form.Item
          name="amount"
          label={t('finances.statements.payAmount')}
          rules={[{ required: true, message: t('finances.transactions.amountRequired') }]}
        >
          <InputNumber style={{ width: '100%' }} min={0.01} step={0.01} />
        </Form.Item>
        <Form.Item name="occurredOn" label={t('finances.transactions.occurredOn')}>
          <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
        </Form.Item>
        <Form.Item name="description" label={t('finances.transactions.description')}>
          <Input maxLength={200} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
