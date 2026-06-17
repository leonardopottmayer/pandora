import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { type PendingTransactionDto, type TransactionKind } from '../../models'
import { transactionKindLabelKey } from '../../lib/enums'
import { CategorySelect, type CategorySelection } from '../../components/CategorySelect'
import { useUpdatePendingTransaction } from '../../hooks/usePendingTransactions'

const ACCOUNT_KINDS: TransactionKind[] = [
  'income',
  'expense',
  'adjustment',
  'yield',
  'investment-contribution',
  'investment-redemption',
]
const CARD_KINDS: TransactionKind[] = ['expense', 'refund']

const INCOME_KINDS = new Set<TransactionKind>(['income', 'yield', 'investment-redemption', 'refund'])
const EXPENSE_KINDS = new Set<TransactionKind>(['expense', 'investment-contribution'])

function natureForKind(kind: TransactionKind): 'income' | 'expense' | undefined {
  if (INCOME_KINDS.has(kind)) return 'income'
  if (EXPENSE_KINDS.has(kind)) return 'expense'
  return undefined
}

interface PendingFormValues {
  kind: TransactionKind
  amount: number
  occurredOn: Dayjs
  description: string
  payee?: string
  notes?: string
  category?: CategorySelection
}

interface PendingTransactionEditModalProps {
  open: boolean
  pending?: PendingTransactionDto | null
  onClose: () => void
}

export function PendingTransactionEditModal({
  open,
  pending,
  onClose,
}: PendingTransactionEditModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<PendingFormValues>()
  const kind = Form.useWatch('kind', form)
  const updateMutation = useUpdatePendingTransaction()

  const isCard = !!pending?.cardId
  const kindOptions = (isCard ? CARD_KINDS : ACCOUNT_KINDS).map((k) => ({
    value: k,
    label: t(transactionKindLabelKey(k)),
  }))

  useEffect(() => {
    if (!open || !pending) return
    form.setFieldsValue({
      kind: pending.kind,
      amount: pending.amount ?? undefined,
      occurredOn: dayjs(pending.occurredOn),
      description: pending.description,
      payee: pending.payee ?? undefined,
      notes: pending.notes ?? undefined,
      category: {
        systemCategoryId: pending.systemCategoryId,
        userCategoryId: pending.userCategoryId,
      },
    })
  }, [open, pending, form])

  async function handleSubmit(values: PendingFormValues) {
    if (!pending) return
    try {
      await updateMutation.mutateAsync({
        id: pending.id,
        body: {
          kind: values.kind,
          amount: values.amount ?? null,
          occurredOn: values.occurredOn.format('YYYY-MM-DD'),
          description: values.description,
          payee: values.payee ?? null,
          notes: values.notes ?? null,
          systemCategoryId: values.category?.systemCategoryId ?? null,
          userCategoryId: values.category?.userCategoryId ?? null,
          // Preserve the statement hint generated for card-targeted proposals.
          suggestedStatementId: pending.suggestedStatementId,
        },
      })
      message.success(t('finances.inbox.updated'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('finances.inbox.editTitle')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: updateMutation.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
      width={520}
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item name="kind" label={t('finances.transactions.kind')} rules={[{ required: true }]}>
          <Select options={kindOptions} />
        </Form.Item>

        <Form.Item
          name="amount"
          label={t('finances.transactions.amount')}
          rules={[{ required: true, message: t('finances.transactions.amountRequired') }]}
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

        <Form.Item name="category" label={t('finances.transactions.category')}>
          <CategorySelect nature={kind ? natureForKind(kind) : undefined} />
        </Form.Item>

        <Form.Item name="payee" label={t('finances.transactions.payee')}>
          <Input maxLength={120} />
        </Form.Item>

        <Form.Item name="notes" label={t('finances.transactions.notes')}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
