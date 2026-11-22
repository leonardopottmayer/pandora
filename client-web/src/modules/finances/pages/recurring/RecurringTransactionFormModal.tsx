import { useEffect, useRef, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Alert, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Radio, Select, Space, Switch, Typography, theme } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { todayIn } from '@/lib/today'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  RECURRENCE_FREQUENCIES,
  type RecurrenceFrequency,
  type RecurringTransactionDto,
  type TransactionKind,
} from '../../models'
import { transactionKindLabelKey } from '../../lib/enums'
import { formatDate } from '../../lib/format'
import { scheduleDates } from '../../lib/recurrence'
import { AccountSelect } from '../../components/AccountSelect'
import { CardSelect } from '../../components/CardSelect'
import { CategorySelect, type CategorySelection } from '../../components/CategorySelect'
import { useAccounts } from '../../hooks/useAccounts'
import { useCards } from '../../hooks/useCards'
import {
  useCreateRecurringTransaction,
  useUpdateRecurringTransaction,
} from '../../hooks/useRecurringTransactions'

type Target = 'account' | 'card'

/** How a recurrence ends: one way at a time, as in a calendar. */
type EndMode = 'never' | 'date' | 'count'

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

/** How many of the next dates the form previews. */
const PREVIEW_COUNT = 3

function natureForKind(kind: TransactionKind): 'income' | 'expense' | undefined {
  if (INCOME_KINDS.has(kind)) return 'income'
  if (EXPENSE_KINDS.has(kind)) return 'expense'
  return undefined
}

/** A titled block of the form, its fields two by two from sm up. */
function FormGroup({ title, columns = 2, children }: { title: string; columns?: 1 | 2; children: ReactNode }) {
  const { token } = theme.useToken()
  return (
    <section className="mb-2">
      <div
        className="mb-3 pb-1 text-xs font-semibold uppercase tracking-wider"
        style={{ color: token.colorTextSecondary, borderBottom: `1px solid ${token.colorBorderSecondary}` }}
      >
        {title}
      </div>
      <div className={columns === 2 ? 'grid grid-cols-1 gap-x-4 sm:grid-cols-2' : ''}>{children}</div>
    </section>
  )
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
  endMode: EndMode
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
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<RecurringFormValues>()
  const isEdit = !!recurring
  const target: Target = Form.useWatch('target', form) ?? 'account'
  const kind = Form.useWatch('kind', form)
  const accountId = Form.useWatch('accountId', form)
  const cardId = Form.useWatch('cardId', form)
  const frequency = Form.useWatch('frequency', form)
  const interval = Form.useWatch('interval', form)
  const startDate = Form.useWatch('startDate', form)
  const endMode: EndMode = Form.useWatch('endMode', form) ?? 'never'
  const endDate = Form.useWatch('endDate', form)
  const maxOccurrences = Form.useWatch('maxOccurrences', form)

  const { data: accounts } = useAccounts()
  const { data: cards } = useCards()
  const currency =
    target === 'card' ? cards?.find((c) => c.id === cardId)?.currency : accounts?.find((a) => a.id === accountId)?.currency

  const createMutation = useCreateRecurringTransaction()
  const updateMutation = useUpdateRecurringTransaction()
  const pending = createMutation.isPending || updateMutation.isPending

  const followedName = useRef('')

  useEffect(() => {
    if (!open) return
    followedName.current = ''
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
        // One with both an end date and a limit shows the date; saving keeps only that one.
        endMode: recurring.endDate ? 'date' : recurring.maxOccurrences ? 'count' : 'never',
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
        startDate: todayIn(timeZone),
        endMode: 'never',
        autoPost: false,
        autoGenerate: true,
      })
    }
  }, [open, recurring, form, timeZone])

  const kindOptions = (target === 'account' ? ACCOUNT_KINDS : CARD_KINDS).map((k) => ({
    value: k,
    label: t(transactionKindLabelKey(k)),
  }))

  // The dates it will fall on: from the next one when editing, from the start when creating.
  const previewStart = recurring && recurring.status !== 'finished' ? dayjs(recurring.nextOccurrenceOn) : startDate
  const preview =
    previewStart && frequency && interval
      ? scheduleDates(
          previewStart,
          frequency,
          interval,
          PREVIEW_COUNT,
          endMode === 'date' ? endDate : null,
          endMode === 'count' && maxOccurrences ? maxOccurrences - (recurring?.occurrencesCount ?? 0) : Infinity,
        )
      : []

  async function handleSubmit(values: RecurringFormValues) {
    const ends = {
      endDate: values.endMode === 'date' && values.endDate ? values.endDate.format('YYYY-MM-DD') : null,
      maxOccurrences: values.endMode === 'count' ? (values.maxOccurrences ?? null) : null,
    }
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
            ...ends,
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
          ...ends,
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
      width={720}
      styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={handleSubmit}
        onValuesChange={(changed: Partial<RecurringFormValues>) => {
          // The name is what most transactions are called too: it fills the description while the
          // description is still empty or still the name it was given.
          if (isEdit || !('name' in changed)) return
          const description = form.getFieldValue('description') ?? ''
          if (description === '' || description === followedName.current) form.setFieldValue('description', changed.name)
          followedName.current = changed.name ?? ''
        }}
      >
        <FormGroup title={t('finances.recurring.groupWhat')}>
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
            <Input maxLength={120} placeholder={t('finances.recurring.namePlaceholder')} />
          </Form.Item>

          <Form.Item name="kind" label={t('finances.transactions.kind')} rules={[{ required: true }]}>
            <Select options={kindOptions} disabled={isEdit} />
          </Form.Item>

          <Form.Item label={t('finances.transactions.amount')} required className="!mb-0">
            <Form.Item
              name="amount"
              rules={[{ required: true, message: t('finances.transactions.amountRequired') }]}
              className="!mb-1"
            >
              <InputNumber style={{ width: '100%' }} min={0.01} step={0.01} prefix={currency} />
            </Form.Item>
            <Form.Item name="amountIsEstimate" valuePropName="checked">
              <Checkbox>{t('finances.recurring.amountIsEstimate')}</Checkbox>
            </Form.Item>
          </Form.Item>

          <Form.Item name="category" label={t('finances.transactions.category')}>
            <CategorySelect nature={kind ? natureForKind(kind) : undefined} />
          </Form.Item>

          <Form.Item
            name="description"
            label={t('finances.transactions.description')}
            extra={t('finances.recurring.descriptionHint')}
            rules={[{ required: true, message: t('finances.transactions.descriptionRequired') }]}
          >
            <Input maxLength={200} />
          </Form.Item>

          <Form.Item name="payee" label={t('finances.transactions.payee')}>
            <Input maxLength={120} />
          </Form.Item>
        </FormGroup>

        <FormGroup title={t('finances.recurring.groupWhen')}>
          <Form.Item label={t('finances.recurring.repeats')} required tooltip={t('finances.recurring.intervalHint')}>
            <Space.Compact className="w-full">
              <Form.Item
                name="interval"
                noStyle
                rules={[{ required: true, message: t('finances.recurring.intervalRequired') }]}
              >
                <InputNumber
                  className="w-20"
                  min={1}
                  max={365}
                  disabled={isEdit}
                  aria-label={t('finances.recurring.interval')}
                />
              </Form.Item>
              <Form.Item name="frequency" noStyle rules={[{ required: true }]}>
                <Select
                  className="flex-1"
                  disabled={isEdit}
                  aria-label={t('finances.recurring.frequency')}
                  options={RECURRENCE_FREQUENCIES.map((f) => ({
                    value: f,
                    label: t(`finances.recurring.unit.${f}`, { count: interval ?? 1 }),
                  }))}
                />
              </Form.Item>
            </Space.Compact>
          </Form.Item>

          <Form.Item
            name="startDate"
            label={t('finances.recurring.startDate')}
            tooltip={t('finances.recurring.startDateHint')}
            rules={[{ required: true, message: t('finances.recurring.startDateRequired') }]}
          >
            <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" disabled={isEdit} />
          </Form.Item>

          <Form.Item name="endMode" label={t('finances.recurring.ends')}>
            <Radio.Group
              optionType="button"
              options={[
                { value: 'never', label: t('finances.recurring.endsNever') },
                { value: 'date', label: t('finances.recurring.endsOn') },
                { value: 'count', label: t('finances.recurring.endsAfter') },
              ]}
            />
          </Form.Item>

          {endMode === 'date' && (
            <Form.Item
              name="endDate"
              label={t('finances.recurring.endDate')}
              rules={[{ required: true, message: t('finances.recurring.endDateRequired') }]}
            >
              <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
            </Form.Item>
          )}
          {endMode === 'count' && (
            <Form.Item
              name="maxOccurrences"
              label={t('finances.recurring.maxOccurrences')}
              tooltip={t('finances.recurring.maxOccurrencesHint')}
              rules={[{ required: true, message: t('finances.recurring.maxOccurrencesRequired') }]}
            >
              <InputNumber style={{ width: '100%' }} min={1} suffix={t('finances.recurring.times')} />
            </Form.Item>
          )}

          {preview.length > 0 && (
            <Typography.Text type="secondary" className="col-span-full -mt-2 mb-4 block text-xs">
              {t('finances.recurring.preview', { dates: preview.map(formatDate).join(', ') })}
            </Typography.Text>
          )}
        </FormGroup>

        <FormGroup title={t('finances.recurring.groupAutomation')} columns={1}>
          {target === 'account' ? (
            <Form.Item
              name="autoPost"
              label={t('finances.recurring.autoPost')}
              valuePropName="checked"
              extra={t('finances.recurring.autoPostHint')}
            >
              <Switch />
            </Form.Item>
          ) : (
            <Alert className="mb-4" type="info" showIcon title={t('finances.recurring.cardAlwaysInbox')} />
          )}

          <Form.Item
            name="autoGenerate"
            label={t('finances.recurring.autoGenerate')}
            valuePropName="checked"
            extra={t('finances.recurring.autoGenerateHint')}
            className="!mb-0"
          >
            <Switch />
          </Form.Item>
        </FormGroup>
      </Form>
    </Modal>
  )
}
