import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Descriptions, Drawer, Form, Input, Spin, Tag, Typography } from 'antd'
import type { TransactionDto } from '../models'
import { useTransaction, usePostTransaction, useUnvoidTransaction, useUpdateTransaction, useVoidTransaction } from '../hooks/useTransactions'
import { useAccountNames } from '../hooks/useAccounts'
import { useCardNames } from '../hooks/useCards'
import { kindDirection, transactionKindLabelKey, TRANSACTION_STATUS_META } from '../lib/enums'
import { formatDate, formatReferenceMonth } from '../lib/format'
import { toErrorMessage } from '@/lib/api/envelope'
import { AttachmentsPanel } from './AttachmentsPanel'
import { CategorySelect, type CategorySelection } from './CategorySelect'
import { CurrencyAmount } from './CurrencyAmount'

interface DetailForm {
  description: string
  category?: CategorySelection
  payee?: string
  notes?: string
}

/**
 * One transaction beside the list: what it is, the parts that can be corrected (description, category,
 * payee, notes), its attachments, and posting or voiding it. Fetches only while a transaction is open.
 */
export function TransactionDetailDrawer({ transactionId, onClose }: { transactionId: string | null; onClose: () => void }) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<DetailForm>()
  const { data: tx, isLoading } = useTransaction(transactionId)
  const accountNames = useAccountNames()
  const cardNames = useCardNames()
  const update = useUpdateTransaction()
  const post = usePostTransaction()
  const voidTx = useVoidTransaction()
  const unvoid = useUnvoidTransaction()

  useEffect(() => {
    if (!tx) return
    form.setFieldsValue({
      description: tx.description,
      category: { systemCategoryId: tx.systemCategoryId, userCategoryId: tx.userCategoryId },
      payee: tx.payee ?? undefined,
      notes: tx.notes ?? undefined,
    })
  }, [tx, form])

  async function save(values: DetailForm) {
    if (!tx) return
    try {
      await update.mutateAsync({
        id: tx.id,
        body: {
          description: values.description.trim(),
          payee: values.payee?.trim() || null,
          notes: values.notes?.trim() || null,
          systemCategoryId: values.category?.systemCategoryId ?? null,
          userCategoryId: values.category?.userCategoryId ?? null,
        },
      })
      message.success(t('finances.transactions.updated'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.transactions.saveError')))
    }
  }

  async function run(action: Promise<unknown>, done: string) {
    try {
      await action
      message.success(done)
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.transactions.saveError')))
    }
  }

  function confirmVoid(current: TransactionDto) {
    modal.confirm({
      title: t('finances.transactions.voidConfirm'),
      content: current.description,
      okText: t('finances.transactions.void'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => run(voidTx.mutateAsync({ id: current.id }), t('finances.transactions.voided')),
    })
  }

  const place = tx?.cardId ? cardNames.get(tx.cardId) : tx?.accountId ? accountNames.get(tx.accountId) : null

  return (
    <Drawer
      open={!!transactionId}
      title={t('finances.transactions.detailTitle')}
      size={480}
      onClose={onClose}
      destroyOnHidden
      footer={
        tx && (
          <div className="flex flex-wrap items-center gap-2">
            {tx.status === 'pending' && (
              <Button
                loading={post.isPending}
                onClick={() => void run(post.mutateAsync(tx.id), t('finances.transactions.posted'))}
              >
                {t('finances.transactions.post')}
              </Button>
            )}
            {tx.status === 'void' ? (
              <Button
                loading={unvoid.isPending}
                onClick={() => void run(unvoid.mutateAsync(tx.id), t('finances.transactions.unvoided'))}
              >
                {t('finances.transactions.unvoid')}
              </Button>
            ) : (
              <Button danger onClick={() => confirmVoid(tx)}>
                {t('finances.transactions.void')}
              </Button>
            )}
            <Button type="primary" className="ms-auto" loading={update.isPending} onClick={() => form.submit()}>
              {t('common.save')}
            </Button>
          </div>
        )
      }
    >
      {isLoading || !tx ? (
        <Spin className="m-8 block" />
      ) : (
        <div className="flex flex-col gap-5">
          <div className="flex flex-col gap-1">
            <span className="text-2xl font-semibold tabular-nums">
              <CurrencyAmount
                amount={tx.amount}
                currency={tx.currency}
                direction={tx.status === 'void' ? undefined : kindDirection(tx.kind)}
              />
            </span>
            <span className="flex flex-wrap items-center gap-2">
              <Typography.Text type="secondary">
                {[formatDate(tx.occurredOn), place].filter(Boolean).join(' · ')}
              </Typography.Text>
              {tx.status !== 'posted' && (
                <Tag color={TRANSACTION_STATUS_META[tx.status].color}>{t(TRANSACTION_STATUS_META[tx.status].labelKey)}</Tag>
              )}
            </span>
            {tx.status === 'void' && tx.voidReason && (
              <Typography.Text type="secondary" className="text-xs">
                {tx.voidReason}
              </Typography.Text>
            )}
          </div>

          <Form form={form} layout="vertical" onFinish={save} requiredMark={false}>
            <Form.Item
              name="description"
              label={t('finances.transactions.description')}
              rules={[{ required: true, whitespace: true }]}
            >
              <Input maxLength={200} />
            </Form.Item>
            <Form.Item name="category" label={t('finances.transactions.category')}>
              <CategorySelect />
            </Form.Item>
            <Form.Item name="payee" label={t('finances.transactions.payee')}>
              <Input maxLength={200} />
            </Form.Item>
            <Form.Item name="notes" label={t('finances.transactions.notes')} className="!mb-0">
              <Input.TextArea rows={3} maxLength={2000} />
            </Form.Item>
          </Form>

          <Descriptions column={1} size="small">
            <Descriptions.Item label={t('finances.transactions.kind')}>{t(transactionKindLabelKey(tx.kind))}</Descriptions.Item>
            {tx.installmentPlanId && tx.installmentNumber != null && (
              <Descriptions.Item label={t('finances.transactions.installment')}>{tx.installmentNumber}</Descriptions.Item>
            )}
            {tx.statementReferenceMonth && (
              <Descriptions.Item label={t('finances.transactions.statement')}>
                {formatReferenceMonth(tx.statementReferenceMonth)}
                {tx.statementDueDate && ` · ${t('finances.transactions.due', { date: formatDate(tx.statementDueDate) })}`}
              </Descriptions.Item>
            )}
          </Descriptions>

          <AttachmentsPanel owner={{ transactionId: tx.id }} />
        </div>
      )}
    </Drawer>
  )
}
