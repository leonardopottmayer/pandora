import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Alert, DatePicker, Form, Input, InputNumber, Modal, Radio, Select, Switch } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  RECURRENCE_FREQUENCIES,
  type RecurrenceFrequency,
  type RecurringTransactionDto,
  type TransactionKind,
} from '../../models'
import { recurrenceFrequencyLabelKey, transactionKindLabelKey } from '../../lib/enums'
import { AccountSelect } from '../../components/AccountSelect'
import { CardSelect } from '../../components/CardSelect'
import { CategorySelect, type CategorySelection } from '../../components/CategorySelect'
import {
  useCreateRecurringTransaction,
  useUpdateRecurringTransaction,
} from '../../hooks/useRecurringTransactions'

type Target = 'account' | 'card'

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

interface RecurringFormValues {
  target: Target
  accountId?: string
  cardId?: string
  kind: TransactionKind
  name: string
  amount: number
  amountIsEstimate: boolean
  description: string
  payee?: string
  category?: CategorySelection
  frequency: RecurrenceFrequency
  interval: number
  startDate: Dayjs
  endDate?: Dayjs
  maxOccurrences?: number
  autoPost: boolean
  autoGenerate: boolean
}

interface RecurringTransactionFormModalProps {
  open: boolean
  recurring?: RecurringTransactionDto | null
  onClose: () => void
}

export function RecurringTransactionFormModal({
  open,
  recurring,
  onClose,
}: RecurringTransactionFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<RecurringFormValues>()
  const isEdit = !!recurring
  const target: Target = Form.useWatch('target', form) ?? 'account'
  const kind = Form.useWatch('kind', form)

  const createMutation = useCreateRecurringTransaction()
  const updateMutation = useUpdateRecurringTransaction()
  const pending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    if (!open) return
    if (recurring) {
      form.setFieldsValue({
        target: recurring.cardId ? 'card' : 'account',
        accountId: recurring.accountId ?? undefined,
        cardId: recurring.cardId ?? undefined,
        kind: recurring.kind,
        name: recurring.name,
        amount: recurring.amount ?? undefined,
        amountIsEstimate: recurring.amountIsEstimate,
        description: recurring.description,
        payee: recurring.payee ?? undefined,
        category: {
          systemCategoryId: recurring.systemCategoryId,
          userCategoryId: recurring.userCategoryId,
        },
        frequency: recurring.frequency,
        interval: recurring.interval,
        startDate: dayjs(recurring.startDate),
        endDate: recurring.endDate ? dayjs(recurring.endDate) : undefined,
        maxOccurrences: recurring.maxOccurrences ?? undefined,
        autoPost: recurring.autoPost,
        autoGenerate: recurring.autoGenerate,
      })
    } else {
      form.resetFields()
      form.setFieldsValue({
        target: 'account',
        kind: 'expense',
        amountIsEstimate: false,
        frequency: 'monthly',
        interval: 1,
        startDate: dayjs(),
        autoPost: false,
        autoGenerate: true,
      })
    }
  }, [open, recurring, form])

  const kindOptions = (target === 'account' ? ACCOUNT_KINDS : CARD_KINDS).map((k) => ({
    value: k,
    label: t(transactionKindLabelKey(k)),
  }))

  async function handleSubmit(values: RecurringFormValues) {
    try {
      if (isEdit && recurring) {
        await updateMutation.mutateAsync({
          id: recurring.id,
          body: {
            name: values.name,
            amount: values.amount ?? null,
            amountIsEstimate: values.amountIsEstimate,
            description: values.description,
            payee: values.payee ?? null,
            systemCategoryId: values.category?.systemCategoryId ?? null,
            userCategoryId: values.category?.userCategoryId ?? null,
            endDate: values.endDate ? values.endDate.format('YYYY-MM-DD') : null,
            maxOccurrences: values.maxOccurrences ?? null,
            autoPost: values.target === 'account' ? values.autoPost : false,
            autoGenerate: values.autoGenerate,
          },
        })
        message.success(t('finances.recurring.updated'))
      } else {
        await createMutation.mutateAsync({
          name: values.name,
          accountId: values.target === 'account' ? values.accountId : null,
          cardId: values.target === 'card' ? values.cardId : null,
          kind: values.kind,
          amount: values.amount ?? null,
          amountIsEstimate: values.amountIsEstimate,
          description: values.description,
          payee: values.payee ?? null,
          systemCategoryId: values.category?.systemCategoryId ?? null,
          userCategoryId: values.category?.userCategoryId ?? null,
          frequency: values.frequency,
          interval: values.interval,
          startDate: values.startDate.format('YYYY-MM-DD'),
          endDate: values.endDate ? values.endDate.format('YYYY-MM-DD') : null,
          maxOccurrences: values.maxOccurrences ?? null,
          autoPost: values.target === 'account' ? values.autoPost : false,
          autoGenerate: values.autoGenerate,
        })
        message.success(t('finances.recurring.created'))
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.recurring.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={isEdit ? t('finances.recurring.editTitle') : t('finances.recurring.newTitle')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: pending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
      width={560}
      styles={{ body: { maxHeight: 'calc(100vh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item name="target" label={t('finances.recurring.target')}>
          <Radio.Group
            disabled={isEdit}
            onChange={() => form.setFieldValue('kind', 'expense')}
            options={[
              { value: 'account', label: t('finances.transactions.targetAccount') },
              { value: 'card', label: t('finances.transactions.targetCard') },
            ]}
            optionType="button"
          />
        </Form.Item>

        {target === 'account' ? (
          <Form.Item
            name="accountId"
            label={t('finances.transactions.account')}
            rules={[{ required: true, message: t('finances.transactions.accountRequired') }]}
          >
            <AccountSelect disabled={isEdit} />
          </Form.Item>
        ) : (
          <Form.Item
            name="cardId"
            label={t('finances.transactions.card')}
            rules={[{ required: true, message: t('finances.transactions.cardRequired') }]}
          >
            <CardSelect disabled={isEdit} />
          </Form.Item>
        )}

        <Form.Item
          name="name"
          label={t('finances.recurring.name')}
          rules={[{ required: true, message: t('finances.recurring.nameRequired') }]}
        >
          <Input maxLength={120} />
        </Form.Item>

        <Form.Item name="kind" label={t('finances.transactions.kind')} rules={[{ required: true }]}>
          <Select options={kindOptions} disabled={isEdit} />
        </Form.Item>

        <Form.Item
          name="amount"
          label={t('finances.transactions.amount')}
          rules={[{ required: true, message: t('finances.transactions.amountRequired') }]}
        >
          <InputNumber style={{ width: '100%' }} min={0.01} step={0.01} />
        </Form.Item>

        <Form.Item name="amountIsEstimate" label={t('finances.recurring.amountIsEstimate')} valuePropName="checked">
          <Switch />
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

        <Form.Item
          name="frequency"
          label={t('finances.recurring.frequency')}
          rules={[{ required: true }]}
        >
          <Select
            disabled={isEdit}
            options={RECURRENCE_FREQUENCIES.map((f) => ({
              value: f,
              label: t(recurrenceFrequencyLabelKey(f)),
            }))}
          />
        </Form.Item>

        <Form.Item
          name="interval"
          label={t('finances.recurring.interval')}
          tooltip={t('finances.recurring.intervalHint')}
          rules={[{ required: true, message: t('finances.recurring.intervalRequired') }]}
        >
          <InputNumber style={{ width: '100%' }} min={1} max={365} disabled={isEdit} />
        </Form.Item>

        <Form.Item
          name="startDate"
          label={t('finances.recurring.startDate')}
          tooltip={t('finances.recurring.startDateHint')}
          rules={[{ required: true, message: t('finances.recurring.startDateRequired') }]}
        >
          <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" disabled={isEdit} />
        </Form.Item>

        <Form.Item name="endDate" label={t('finances.recurring.endDate')}>
          <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" allowClear />
        </Form.Item>

        <Form.Item
          name="maxOccurrences"
          label={t('finances.recurring.maxOccurrences')}
          tooltip={t('finances.recurring.maxOccurrencesHint')}
        >
          <InputNumber style={{ width: '100%' }} min={1} />
        </Form.Item>

        <Form.Item name="payee" label={t('finances.transactions.payee')}>
          <Input maxLength={120} />
        </Form.Item>

        {target === 'account' ? (
          <Form.Item
            name="autoPost"
            label={t('finances.recurring.autoPost')}
            tooltip={t('finances.recurring.autoPostHint')}
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
        ) : (
          <Alert type="info" showIcon message={t('finances.recurring.cardAlwaysInbox')} />
        )}

        <Form.Item
          name="autoGenerate"
          label={t('finances.recurring.autoGenerate')}
          tooltip={t('finances.recurring.autoGenerateHint')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>
      </Form>
    </Modal>
  )
}
