import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, Input, InputNumber, Modal, Radio, Select } from 'antd'
import type { Dayjs } from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { type TransactionKind } from '../../models'
import { transactionKindLabelKey } from '../../lib/enums'
import { AccountSelect } from '../../components/AccountSelect'
import { CardSelect } from '../../components/CardSelect'
import { CategorySelect, type CategorySelection } from '../../components/CategorySelect'
import { useCreateTransaction } from '../../hooks/useTransactions'

type Target = 'account' | 'card'

// Tipos que o usuário pode lançar manualmente (transferências têm fluxo próprio;
// pagamento de fatura e as pernas de transferência são gerados pelo backend).
const ACCOUNT_KINDS: TransactionKind[] = [
  'income',
  'expense',
  'adjustment',
  'yield',
  'investment-contribution',
  'investment-redemption',
  'opening-balance',
]
const CARD_KINDS: TransactionKind[] = ['expense', 'refund']

const INCOME_KINDS = new Set<TransactionKind>(['income', 'yield', 'investment-redemption', 'refund'])
const EXPENSE_KINDS = new Set<TransactionKind>(['expense', 'investment-contribution'])

function natureForKind(kind: TransactionKind): 'income' | 'expense' | undefined {
  if (INCOME_KINDS.has(kind)) return 'income'
  if (EXPENSE_KINDS.has(kind)) return 'expense'
  return undefined
}

interface TransactionFormValues {
  target: Target
  accountId?: string
  cardId?: string
  kind: TransactionKind
  amount: number
  occurredOn: Dayjs
  description: string
  payee?: string
  notes?: string
  category?: CategorySelection
  installments?: number
}

interface TransactionFormModalProps {
  open: boolean
  onClose: () => void
}

export function TransactionFormModal({ open, onClose }: TransactionFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<TransactionFormValues>()
  const target: Target = Form.useWatch('target', form) ?? 'account'
  const kind = Form.useWatch('kind', form)

  const createMutation = useCreateTransaction()

  useEffect(() => {
    if (!open) return
    form.resetFields()
    form.setFieldsValue({ target: 'account', kind: 'expense', installments: 1 })
  }, [open, form])

  const kindOptions = (target === 'account' ? ACCOUNT_KINDS : CARD_KINDS).map((k) => ({
    value: k,
    label: t(transactionKindLabelKey(k)),
  }))

  async function handleSubmit(values: TransactionFormValues) {
    try {
      await createMutation.mutateAsync({
        accountId: values.target === 'account' ? values.accountId : null,
        cardId: values.target === 'card' ? values.cardId : null,
        kind: values.kind,
        amount: values.amount,
        occurredOn: values.occurredOn.format('YYYY-MM-DD'),
        description: values.description,
        payee: values.payee ?? null,
        notes: values.notes ?? null,
        systemCategoryId: values.category?.systemCategoryId ?? null,
        userCategoryId: values.category?.userCategoryId ?? null,
        installments: values.target === 'card' ? (values.installments ?? 1) : 1,
      })
      message.success(t('finances.transactions.created'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.transactions.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('finances.transactions.new')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: createMutation.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
      width={560}
      styles={{ body: { maxHeight: 'calc(100vh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item name="target" label={t('finances.transactions.target')}>
          <Radio.Group
            onChange={() => {
              // O conjunto de tipos muda conforme o alvo; redefine para um valido.
              form.setFieldValue('kind', 'expense')
            }}
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
            <AccountSelect />
          </Form.Item>
        ) : (
          <Form.Item
            name="cardId"
            label={t('finances.transactions.card')}
            rules={[{ required: true, message: t('finances.transactions.cardRequired') }]}
          >
            <CardSelect />
          </Form.Item>
        )}

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

        {target === 'card' && (
          <Form.Item
            name="installments"
            label={t('finances.transactions.installments')}
            initialValue={1}
          >
            <InputNumber style={{ width: '100%' }} min={1} max={48} />
          </Form.Item>
        )}

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
