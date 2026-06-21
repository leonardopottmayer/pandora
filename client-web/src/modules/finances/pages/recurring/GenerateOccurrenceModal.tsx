import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, Input, InputNumber, Modal, Radio, Switch } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import type { RecurringTransactionDto } from '../../models'
import { useGenerateRecurringOccurrence } from '../../hooks/useRecurringTransactions'

type Destination = 'inbox' | 'transactions'

interface GenerateOccurrenceFormValues {
  destination: Destination
  advanceSchedule: boolean
  occurredOn: Dayjs
  amount?: number
  description: string
  payee?: string
  notes?: string
}

interface GenerateOccurrenceModalProps {
  open: boolean
  recurring: RecurringTransactionDto | null
  onClose: () => void
}

export function GenerateOccurrenceModal({ open, recurring, onClose }: GenerateOccurrenceModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<GenerateOccurrenceFormValues>()
  const mutation = useGenerateRecurringOccurrence()

  useEffect(() => {
    if (!open || !recurring) return
    form.setFieldsValue({
      destination: 'inbox',
      advanceSchedule: true,
      occurredOn: dayjs(recurring.nextOccurrenceOn),
      amount: recurring.amount ?? undefined,
      description: recurring.description,
      payee: recurring.payee ?? undefined,
      notes: undefined,
    })
  }, [open, recurring, form])

  async function handleSubmit(values: GenerateOccurrenceFormValues) {
    if (!recurring) return
    try {
      const result = await mutation.mutateAsync({
        id: recurring.id,
        body: {
          destination: values.destination,
          advanceSchedule: values.advanceSchedule,
          occurredOn: values.occurredOn.format('YYYY-MM-DD'),
          amount: values.amount ?? null,
          description: values.description,
          payee: values.payee ?? null,
          notes: values.notes ?? null,
        },
      })
      message.success(
        result.destination === 'inbox'
          ? t('finances.recurring.generatedToInbox')
          : t('finances.recurring.generatedToTransactions'),
      )
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.recurring.generateError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('finances.recurring.generateTitle')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: mutation.isPending }}
      okText={t('finances.recurring.generate')}
      cancelText={t('common.cancel')}
      destroyOnHidden
      width={480}
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item name="destination" label={t('finances.recurring.generateDestination')}>
          <Radio.Group
            optionType="button"
            options={[
              { value: 'inbox', label: t('finances.recurring.destinationInbox') },
              { value: 'transactions', label: t('finances.recurring.destinationTransactions') },
            ]}
          />
        </Form.Item>

        <Form.Item
          name="occurredOn"
          label={t('finances.transactions.occurredOn')}
          rules={[{ required: true, message: t('finances.transactions.occurredOnRequired') }]}
        >
          <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
        </Form.Item>

        <Form.Item name="amount" label={t('finances.transactions.amount')}>
          <InputNumber style={{ width: '100%' }} min={0.01} step={0.01} />
        </Form.Item>

        <Form.Item
          name="description"
          label={t('finances.transactions.description')}
          rules={[{ required: true, message: t('finances.transactions.descriptionRequired') }]}
        >
          <Input maxLength={200} />
        </Form.Item>

        <Form.Item name="payee" label={t('finances.transactions.payee')}>
          <Input maxLength={120} />
        </Form.Item>

        <Form.Item name="notes" label={t('finances.transactions.notes')}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>

        <Form.Item
          name="advanceSchedule"
          label={t('finances.recurring.advanceSchedule')}
          tooltip={t('finances.recurring.advanceScheduleHint')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>
      </Form>
    </Modal>
  )
}
