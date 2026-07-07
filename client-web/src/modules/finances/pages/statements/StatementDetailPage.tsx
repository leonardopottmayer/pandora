import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import {
  App, Breadcrumb, Button, Card, Col, DatePicker, Input, InputNumber, Popconfirm, Row, Select, Space, Statistic, Table, Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  TRANSACTION_STATUSES,
  TRANSACTION_KINDS,
  type TransactionDto,
  type TransactionKind,
  type TransactionStatus,
} from '../../models'
import {
  STATEMENT_STATUS_META,
  TRANSACTION_STATUS_META,
  kindDirection,
  transactionKindLabelKey,
} from '../../lib/enums'
import { formatDate, formatMoney, formatReferenceMonth } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { EnumTag } from '../../components/EnumTag'
import { useCard } from '../../hooks/useCards'
import { useCategoryNames } from '../../hooks/useCategories'
import { useCloseStatement, useReopenStatement, useSettleStatement, useStatement } from '../../hooks/useStatements'
import { PayStatementModal } from './PayStatementModal'

interface TxFilters {
  text?: string
  statuses?: TransactionStatus[]
  kinds?: TransactionKind[]
  categoryIds?: string[]
  occurredFrom?: string
  occurredTo?: string
  amountMin?: number
  amountMax?: number
}

export function StatementDetailPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { id = '' } = useParams<{ id: string }>()
  const [payOpen, setPayOpen] = useState(false)
  const [txFilters, setTxFilters] = useState<TxFilters>({})

  const { data, isLoading } = useStatement(id)
  const closeMutation = useCloseStatement()
  const reopenMutation = useReopenStatement()
  const settleMutation = useSettleStatement()
  const categoryNames = useCategoryNames()

  const statement = data?.statement
  const { data: card } = useCard(statement?.cardId ?? '')
  const currency = data?.transactions?.[0]?.currency ?? 'BRL'

  function upd<K extends keyof TxFilters>(key: K, value: TxFilters[K]) {
    setTxFilters((f) => {
      const next = { ...f, [key]: value }
      if (value === undefined || (Array.isArray(value) && value.length === 0)) delete next[key]
      return next
    })
  }

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
      render: (value: string) => formatDate(value),
      sorter: { compare: (a, b) => a.occurredOn.localeCompare(b.occurredOn), multiple: 1 },
    },
    {
      title: t('finances.transactions.description'),
      dataIndex: 'description',
      sorter: { compare: (a, b) => a.description.localeCompare(b.description), multiple: 2 },
    },
    {
      title: t('finances.transactions.installment'),
      key: 'installmentNumber',
      width: 80,
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
      render: (_, tx) => t(transactionKindLabelKey(tx.kind)),
      sorter: { compare: (a, b) => a.kind.localeCompare(b.kind), multiple: 5 },
    },
    {
      title: t('finances.transactions.status'),
      dataIndex: 'status',
      render: (_, tx) => <EnumTag meta={TRANSACTION_STATUS_META[tx.status]} />,
      sorter: { compare: (a, b) => a.status.localeCompare(b.status), multiple: 6 },
    },
    {
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
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

  return (
    <div className="flex flex-col gap-4">
      <Breadcrumb
        items={[
          { title: <Link to="/finances/cards">{t('nav.cards')}</Link> },
          card
            ? { title: <Link to={`/finances/cards/${card.id}`}>{card.name}</Link> }
            : { title: '…' },
          { title: statement ? formatReferenceMonth(statement.referenceMonth) : '…' },
        ]}
      />

      <Card loading={isLoading}>
        <Typography.Title level={4} style={{ marginTop: 0 }}>
          {statement ? formatReferenceMonth(statement.referenceMonth) : '…'}
        </Typography.Title>
        {statement && (
          <>
            <Space className="mb-4">
              <EnumTag meta={STATEMENT_STATUS_META[statement.status]} />
              {canClose && (
                <Button onClick={handleClose} loading={closeMutation.isPending}>
                  {t('finances.statements.close')}
                </Button>
              )}
              {canReopen && (
                <Button onClick={handleReopen} loading={reopenMutation.isPending}>
                  {t('finances.statements.reopen')}
                </Button>
              )}
              {canPay && (
                <Button type="primary" onClick={() => setPayOpen(true)}>
                  {t('finances.statements.pay')}
                </Button>
              )}
              {canSettle && (
                <Popconfirm
                  title={t('finances.statements.settle')}
                  description={t('finances.statements.settleConfirm')}
                  okText={t('finances.statements.settle')}
                  cancelText={t('common.cancel')}
                  onConfirm={handleSettle}
                >
                  <Button loading={settleMutation.isPending}>
                    {t('finances.statements.settle')}
                  </Button>
                </Popconfirm>
              )}
            </Space>
            <Row gutter={16}>
              <Col xs={8}>
                <Statistic
                  title={t('finances.statements.total')}
                  value={formatMoney(statement.totalAmount, currency)}
                />
              </Col>
              <Col xs={8}>
                <Statistic
                  title={t('finances.statements.paidAmount')}
                  value={formatMoney(statement.paidAmount, currency)}
                />
              </Col>
              <Col xs={8}>
                <Statistic
                  title={t('finances.statements.remaining')}
                  value={formatMoney(statement.remainingAmount, currency)}
                />
              </Col>
            </Row>
          </>
        )}
      </Card>

      <Card title={t('finances.statements.transactions')}>
        <Row gutter={[8, 8]} className="mb-4">
          <Col xs={24} sm={12} lg={8}>
            <Input
              allowClear
              placeholder={t('common.search')}
              onChange={(e) => upd('text', e.target.value || undefined)}
            />
          </Col>
          <Col xs={24} sm={12} lg={8}>
            <DatePicker.RangePicker
              style={{ width: '100%' }}
              format="YYYY-MM-DD"
              placeholder={[
                t('finances.transactions.occurredOn') + ' ' + t('finances.filters.from'),
                t('finances.transactions.occurredOn') + ' ' + t('finances.filters.to'),
              ]}
              onChange={(_, s) => {
                upd('occurredFrom', s[0] || undefined)
                upd('occurredTo', s[1] || undefined)
              }}
            />
          </Col>
          <Col xs={24} sm={12} lg={8}>
            <Select
              mode="multiple"
              allowClear
              style={{ width: '100%' }}
              placeholder={t('finances.transactions.status')}
              onChange={(v: TransactionStatus[]) => upd('statuses', v.length ? v : undefined)}
              options={TRANSACTION_STATUSES.map((s) => ({
                value: s,
                label: t(TRANSACTION_STATUS_META[s].labelKey),
              }))}
            />
          </Col>
          <Col xs={24} sm={12} lg={8}>
            <Select
              mode="multiple"
              allowClear
              style={{ width: '100%' }}
              placeholder={t('finances.transactions.kind')}
              onChange={(v: TransactionKind[]) => upd('kinds', v.length ? v : undefined)}
              options={TRANSACTION_KINDS.map((k) => ({
                value: k,
                label: t(transactionKindLabelKey(k)),
              }))}
            />
          </Col>
          {txCategoryOptions.length > 0 && (
            <Col xs={24} sm={12} lg={8}>
              <Select
                mode="multiple"
                allowClear
                style={{ width: '100%' }}
                placeholder={t('finances.transactions.category')}
                optionFilterProp="label"
                onChange={(v: string[]) => upd('categoryIds', v.length ? v : undefined)}
                options={txCategoryOptions}
              />
            </Col>
          )}
          <Col xs={24} sm={12} lg={8}>
            <Space.Compact style={{ width: '100%' }}>
              <InputNumber<number>
                style={{ width: '50%' }}
                placeholder={t('finances.transactions.amount') + ' ' + t('finances.filters.from')}
                onChange={(v) => upd('amountMin', v ?? undefined)}
              />
              <InputNumber<number>
                style={{ width: '50%' }}
                placeholder={t('finances.transactions.amount') + ' ' + t('finances.filters.to')}
                onChange={(v) => upd('amountMax', v ?? undefined)}
              />
            </Space.Compact>
          </Col>
        </Row>
        <Table
          rowKey="id"
          loading={isLoading}
          dataSource={filteredTransactions}
          columns={columns}
          size="middle"
          pagination={{ pageSize: 20 }}
        />
      </Card>

      <PayStatementModal
        open={payOpen}
        statementId={id}
        remainingAmount={statement?.remainingAmount}
        onClose={() => setPayOpen(false)}
      />
    </div>
  )
}
