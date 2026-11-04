import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import {
  Alert, App, Breadcrumb, Button, Drawer, Empty, Popconfirm, Skeleton, Space, Table, Typography, theme,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PaperClipOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TransactionDto } from '../../models'
import {
  STATEMENT_STATUS_META,
  TRANSACTION_STATUS_META,
  kindDirection,
  transactionKindLabelKey,
} from '../../lib/enums'
import { formatDate, formatMoney, formatReferenceMonth } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { EnumTag } from '../../components/EnumTag'
import { AttachmentsPanel } from '../../components/AttachmentsPanel'
import { StatTile } from '../../components/StatTile'
import { useCard } from '../../hooks/useCards'
import { useCategoryNames } from '../../hooks/useCategories'
import { useCloseStatement, useReopenStatement, useSettleStatement, useStatement } from '../../hooks/useStatements'
import { PayStatementModal } from './PayStatementModal'
import { StatementTransactionFilters, type StatementFilters } from './StatementTransactionFilters'

export function StatementDetailPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const { id = '' } = useParams<{ id: string }>()
  const [payOpen, setPayOpen] = useState(false)
  const [txFilters, setTxFilters] = useState<StatementFilters>({})
  const [attachmentsOpen, setAttachmentsOpen] = useState(false)
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)

  const { data, isLoading, isError, error } = useStatement(id)
  const closeMutation = useCloseStatement()
  const reopenMutation = useReopenStatement()
  const settleMutation = useSettleStatement()
  const categoryNames = useCategoryNames()

  const statement = data?.statement
  const { data: card } = useCard(statement?.cardId ?? '')
  const currency = card?.currency ?? data?.transactions?.[0]?.currency ?? 'BRL'

  const txCategoryOptions = useMemo(() => {
    const seen = new Set<string>()
    const opts: { value: string; label: string }[] = []
    for (const tx of data?.transactions ?? []) {
      const catId = tx.systemCategoryId ?? tx.userCategoryId
      if (catId && !seen.has(catId)) {
        seen.add(catId)
        opts.push({ value: catId, label: categoryNames.get(catId) ?? catId })
      }
    }
    return opts
  }, [data?.transactions, categoryNames])

  const filteredTransactions = useMemo(() => {
    const f = txFilters
    return (data?.transactions ?? []).filter((tx) => {
      if (f.text) {
        const q = f.text.toLowerCase()
        const catId = tx.systemCategoryId ?? tx.userCategoryId
        const catName = catId ? (categoryNames.get(catId) ?? '') : ''
        if (!tx.description.toLowerCase().includes(q) && !catName.toLowerCase().includes(q)) return false
      }
      if (f.statuses?.length && !f.statuses.includes(tx.status)) return false
      if (f.kinds?.length && !f.kinds.includes(tx.kind)) return false
      if (f.categoryIds?.length) {
        const catId = tx.systemCategoryId ?? tx.userCategoryId
        if (!catId || !f.categoryIds.includes(catId)) return false
      }
      if (f.occurredFrom && tx.occurredOn < f.occurredFrom) return false
      if (f.occurredTo && tx.occurredOn > f.occurredTo) return false
      if (f.amountMin != null && tx.amount < f.amountMin) return false
      if (f.amountMax != null && tx.amount > f.amountMax) return false
      return true
    })
  }, [data?.transactions, txFilters, categoryNames])

  async function handleClose() {
    try {
      await closeMutation.mutateAsync(id)
      message.success(t('finances.statements.closed'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.statements.saveError')))
    }
  }

  async function handleReopen() {
    try {
      await reopenMutation.mutateAsync(id)
      message.success(t('finances.statements.reopened'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.statements.reopenError')))
    }
  }

  async function handleSettle() {
    try {
      await settleMutation.mutateAsync(id)
      message.success(t('finances.statements.settled'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.statements.saveError')))
    }
  }

  const columns: ColumnsType<TransactionDto> = [
    {
      title: t('finances.transactions.occurredOn'),
      dataIndex: 'occurredOn',
      width: 110,
      responsive: ['md'],
      render: (value: string) => formatDate(value),
      sorter: { compare: (a, b) => a.occurredOn.localeCompare(b.occurredOn), multiple: 1 },
    },
    {
      title: t('finances.transactions.description'),
      dataIndex: 'description',
      render: (description: string, tx) => {
        const categoryId = tx.systemCategoryId ?? tx.userCategoryId
        const category = categoryId ? categoryNames.get(categoryId) : undefined
        return <div className="flex min-w-0 flex-col gap-1">
          <span className="truncate" title={description}>{description}</span>
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
            <Typography.Text type="secondary" className="tabular-nums md:hidden">{formatDate(tx.occurredOn)}</Typography.Text>
            {tx.installmentNumber != null && tx.installmentPlanId != null && (
              <Typography.Text type="secondary" className="lg:hidden">
                {t('finances.transactions.installment')} {tx.installmentNumber}
              </Typography.Text>
            )}
            {category && <Typography.Text type="secondary" className="max-w-full truncate lg:hidden">{category}</Typography.Text>}
            <Typography.Text type="secondary" className="xl:hidden">{t(transactionKindLabelKey(tx.kind))}</Typography.Text>
            <span className="lg:hidden"><EnumTag meta={TRANSACTION_STATUS_META[tx.status]} /></span>
          </span>
        </div>
      },
      sorter: { compare: (a, b) => a.description.localeCompare(b.description), multiple: 2 },
    },
    {
      title: t('finances.transactions.installment'),
      key: 'installmentNumber',
      width: 80,
      responsive: ['lg'],
      render: (_, tx) =>
        tx.installmentNumber != null && tx.installmentPlanId != null ? tx.installmentNumber : '—',
      sorter: {
        compare: (a, b) => (a.installmentNumber ?? -1) - (b.installmentNumber ?? -1),
        multiple: 3,
      },
    },
    {
      title: t('finances.transactions.category'),
      key: 'category',
      width: 150,
      responsive: ['lg'],
      ellipsis: true,
      render: (_, tx) => {
        const categoryId = tx.systemCategoryId ?? tx.userCategoryId
        return categoryId ? categoryNames.get(categoryId) ?? '—' : '—'
      },
      sorter: {
        compare: (a, b) => {
          const idA = a.systemCategoryId ?? a.userCategoryId
          const idB = b.systemCategoryId ?? b.userCategoryId
          return (idA ? (categoryNames.get(idA) ?? '') : '').localeCompare(
            idB ? (categoryNames.get(idB) ?? '') : '',
          )
        },
        multiple: 4,
      },
    },
    {
      title: t('finances.transactions.kind'),
      dataIndex: 'kind',
      width: 140,
      responsive: ['xl'],
      render: (_, tx) => t(transactionKindLabelKey(tx.kind)),
      sorter: { compare: (a, b) => a.kind.localeCompare(b.kind), multiple: 5 },
    },
    {
      title: t('finances.transactions.status'),
      dataIndex: 'status',
      width: 112,
      responsive: ['lg'],
      render: (_, tx) => <EnumTag meta={TRANSACTION_STATUS_META[tx.status]} />,
      sorter: { compare: (a, b) => a.status.localeCompare(b.status), multiple: 6 },
    },
    {
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
      width: 124,
      align: 'right',
      render: (_, tx) => (
        <CurrencyAmount amount={tx.amount} currency={tx.currency} direction={kindDirection(tx.kind)} />
      ),
      sorter: { compare: (a, b) => a.amount - b.amount, multiple: 7 },
    },
  ]

  const canClose = statement?.status === 'open'
  const canReopen = statement != null && statement.status !== 'open' && statement.status !== 'paid'
  const canPay = statement != null && statement.status !== 'paid'
  const canSettle = statement != null && statement.remainingAmount > 0

  if (isLoading) return <Skeleton active />
  if (isError) return <Alert type="error" showIcon title={error.message} />
  if (!statement) return <Empty description={t('finances.statements.notFound')}><Link to="/finances/cards">{t('nav.cards')}</Link></Empty>

  return <div className="flex min-w-0 flex-col gap-6 lg:h-[calc(100dvh-112px)]">
    <Breadcrumb className="shrink-0" items={[
      { title: <Link to="/finances/cards">{t('nav.cards')}</Link> },
      { title: <Link to={`/finances/cards/${statement.cardId}`} className="inline-block max-w-60 truncate align-bottom">{card?.name ?? t('finances.imports.card')}</Link> },
      { title: formatReferenceMonth(statement.referenceMonth) },
    ]} />
    <div className="flex shrink-0 flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-start sm:justify-between">
      <div className="min-w-0 flex-1">
        <PageHeading title={formatReferenceMonth(statement.referenceMonth)} description={t('finances.statements.cycleDates', { closing: formatDate(statement.closingDate), due: formatDate(statement.dueDate) })} />
        <div className="mt-2"><EnumTag meta={STATEMENT_STATUS_META[statement.status]} /></div>
      </div>
      <Space wrap>
        {canPay && <Button type="primary" onClick={() => setPayOpen(true)}>{t('finances.statements.pay')}</Button>}
        {canClose && <Button onClick={handleClose} loading={closeMutation.isPending}>{t('finances.statements.close')}</Button>}
        {canReopen && <Button onClick={handleReopen} loading={reopenMutation.isPending}>{t('finances.statements.reopen')}</Button>}
        {canSettle && <Popconfirm title={t('finances.statements.settle')} description={t('finances.statements.settleConfirm')}
          okText={t('finances.statements.settle')} cancelText={t('common.cancel')} onConfirm={handleSettle}>
          <Button loading={settleMutation.isPending}>{t('finances.statements.settle')}</Button>
        </Popconfirm>}
        <Button icon={<PaperClipOutlined aria-hidden />} onClick={() => setAttachmentsOpen(true)}>{t('finances.attachments.title')}</Button>
      </Space>
    </div>
    <div className="grid shrink-0 grid-cols-2 gap-3 sm:grid-cols-3 [&>div]:min-w-0 [&>div]:px-3 [&>div:first-child]:col-span-2 sm:[&>div:first-child]:col-span-1">
      <StatTile label={t('finances.statements.total')} value={<span className="tabular-nums text-lg sm:text-2xl">{formatMoney(statement.totalAmount, currency)}</span>} />
      <StatTile label={t('finances.statements.paidAmount')} value={<span className="tabular-nums text-lg sm:text-2xl">{formatMoney(statement.paidAmount, currency)}</span>} />
      <StatTile label={t('finances.statements.remaining')} value={<span className="tabular-nums text-lg sm:text-2xl" style={statement.status === 'overdue' ? { color: token.colorError } : undefined}>{formatMoney(statement.remainingAmount, currency)}</span>} />
    </div>
    <StatementTransactionFilters value={txFilters} onChange={setTxFilters} categories={txCategoryOptions} currency={currency} />
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
      <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
        <Table rowKey="id" dataSource={filteredTransactions} columns={columns} tableLayout="fixed" size="middle" pagination={false}
          sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
          locale={{ emptyText: t('finances.statements.noTransactions') }} />
      </div>
      <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>{t('finances.statements.transactionCount', { count: filteredTransactions.length })}</Typography.Text>
    </div>
    <Drawer open={attachmentsOpen} onClose={() => setAttachmentsOpen(false)} title={t('finances.attachments.title')} size={640} destroyOnHidden>
      {attachmentsOpen && <AttachmentsPanel owner={{ cardStatementId: id }} />}
    </Drawer>
    {payOpen && <PayStatementModal open statementId={id} remainingAmount={statement.remainingAmount} onClose={() => setPayOpen(false)} />}
  </div>
}
