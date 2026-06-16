import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  App, Button, Card, Col, DatePicker, Flex, Input, InputNumber,
  Popconfirm, Row, Select, Space, Table, Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PlusOutlined, SwapOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  TRANSACTION_KINDS,
  TRANSACTION_STATUSES,
  type SystemCategoryDto,
  type TransactionDto,
  type TransactionKind,
  type TransactionStatus,
  type UserCategoryDto,
} from '../../models'
import {
  TRANSACTION_STATUS_META,
  kindDirection,
  transactionKindLabelKey,
} from '../../lib/enums'
import { formatDate, formatReferenceMonth } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { EnumTag } from '../../components/EnumTag'
import { useAccounts, useAccountNames } from '../../hooks/useAccounts'
import { useCards, useCardNames } from '../../hooks/useCards'
import { useCategoryNames, useSystemCategories, useUserCategories } from '../../hooks/useCategories'
import { usePostTransaction, useTransactions, useVoidTransaction } from '../../hooks/useTransactions'
import { TransactionFormModal } from './TransactionFormModal'
import { TransferFormModal } from './TransferFormModal'

interface ClientFilters {
  text?: string
  accountIds?: string[]
  cardIds?: string[]
  statuses?: TransactionStatus[]
  kinds?: TransactionKind[]
  categoryIds?: string[]
  occurredFrom?: string
  occurredTo?: string
  dueDateFrom?: string
  dueDateTo?: string
  referenceMonths?: string[]
  amountMin?: number
  amountMax?: number
}

function flattenCats(cats: Array<SystemCategoryDto | UserCategoryDto>): Array<{ id: string; name: string }> {
  const result: Array<{ id: string; name: string }> = []
  function visit(list: typeof cats) {
    for (const c of list) {
      result.push({ id: c.id, name: c.name })
      if (c.children.length) visit(c.children as typeof cats)
    }
  }
  visit(cats)
  return result
}

export function TransactionsListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [clientFilters, setClientFilters] = useState<ClientFilters>({})
  const [formOpen, setFormOpen] = useState(false)
  const [transferOpen, setTransferOpen] = useState(false)

  const { data, isLoading } = useTransactions({ take: 1000 })
  const { data: accounts } = useAccounts()
  const { data: cards } = useCards()
  const { data: systemCategories } = useSystemCategories()
  const { data: userCategories } = useUserCategories()
  const categoryNames = useCategoryNames()
  const accountNames = useAccountNames()
  const cardNames = useCardNames()
  const postMutation = usePostTransaction()
  const voidMutation = useVoidTransaction()

  function upd<K extends keyof ClientFilters>(key: K, value: ClientFilters[K]) {
    setClientFilters((f) => {
      const next = { ...f, [key]: value }
      if (value === undefined || (Array.isArray(value) && value.length === 0)) delete next[key]
      return next
    })
  }

  const referenceMonthOptions = useMemo(() => {
    const seen = new Set<string>()
    const opts: { value: string; label: string }[] = []
    for (const tx of data ?? []) {
      if (tx.statementReferenceMonth && !seen.has(tx.statementReferenceMonth)) {
        seen.add(tx.statementReferenceMonth)
        opts.push({ value: tx.statementReferenceMonth, label: formatReferenceMonth(tx.statementReferenceMonth) })
      }
    }
    return opts.sort((a, b) => a.value.localeCompare(b.value))
  }, [data])

  const categoryOptions = useMemo(() => {
    const sys = flattenCats(systemCategories ?? [])
    const usr = flattenCats(userCategories ?? [])
    return [
      { label: t('finances.categories.systemGroup'), options: sys.map((c) => ({ value: c.id, label: c.name })) },
      { label: t('finances.categories.userGroup'), options: usr.map((c) => ({ value: c.id, label: c.name })) },
    ].filter((g) => g.options.length > 0)
  }, [systemCategories, userCategories, t])

  const filteredData = useMemo(() => {
    const f = clientFilters
    return (data ?? []).filter((tx) => {
      if (f.text) {
        const q = f.text.toLowerCase()
        const catId = tx.systemCategoryId ?? tx.userCategoryId
        const catName = catId ? (categoryNames.get(catId) ?? '') : ''
        if (!tx.description.toLowerCase().includes(q) && !catName.toLowerCase().includes(q)) return false
      }
      if (f.accountIds?.length && !(tx.accountId && f.accountIds.includes(tx.accountId))) return false
      if (f.cardIds?.length && !(tx.cardId && f.cardIds.includes(tx.cardId))) return false
      if (f.statuses?.length && !f.statuses.includes(tx.status)) return false
      if (f.kinds?.length && !f.kinds.includes(tx.kind)) return false
      if (f.categoryIds?.length) {
        const id = tx.systemCategoryId ?? tx.userCategoryId
        if (!id || !f.categoryIds.includes(id)) return false
      }
      if (f.occurredFrom && tx.occurredOn < f.occurredFrom) return false
      if (f.occurredTo && tx.occurredOn > f.occurredTo) return false
      if (f.dueDateFrom && (!tx.statementDueDate || tx.statementDueDate < f.dueDateFrom)) return false
      if (f.dueDateTo && (!tx.statementDueDate || tx.statementDueDate > f.dueDateTo)) return false
      if (f.referenceMonths?.length && (!tx.statementReferenceMonth || !f.referenceMonths.includes(tx.statementReferenceMonth))) return false
      if (f.amountMin != null && tx.amount < f.amountMin) return false
      if (f.amountMax != null && tx.amount > f.amountMax) return false
      return true
    })
  }, [data, clientFilters, categoryNames])

  async function handlePost(tx: TransactionDto) {
    try {
      await postMutation.mutateAsync(tx.id)
      message.success(t('finances.transactions.posted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.transactions.saveError')))
    }
  }

  async function handleVoid(tx: TransactionDto) {
    try {
      await voidMutation.mutateAsync({ id: tx.id })
      message.success(t('finances.transactions.voided'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.transactions.saveError')))
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
      title: t('finances.transactions.origin'),
      key: 'origin',
      render: (_, tx) => {
        if (tx.cardId) return cardNames.get(tx.cardId) ?? '—'
        if (tx.accountId) return accountNames.get(tx.accountId) ?? '—'
        return '—'
      },
      sorter: {
        compare: (a, b) => {
          const nameA = (a.cardId ? cardNames.get(a.cardId) : a.accountId ? accountNames.get(a.accountId) : '') ?? ''
          const nameB = (b.cardId ? cardNames.get(b.cardId) : b.accountId ? accountNames.get(b.accountId) : '') ?? ''
          return nameA.localeCompare(nameB)
        },
        multiple: 3,
      },
    },
    {
      title: t('finances.transactions.creditCard'),
      children: [
        {
          title: t('finances.transactions.installment'),
          key: 'installmentNumber',
          width: 80,
          render: (_, tx) =>
            tx.installmentNumber != null && tx.installmentPlanId != null ? tx.installmentNumber : '—',
          sorter: {
            compare: (a, b) => (a.installmentNumber ?? -1) - (b.installmentNumber ?? -1),
            multiple: 4,
          },
        },
        {
          title: t('finances.transactions.statement'),
          key: 'statement',
          render: (_, tx) =>
            tx.statementReferenceMonth ? formatReferenceMonth(tx.statementReferenceMonth) : '—',
          sorter: {
            compare: (a, b) =>
              (a.statementReferenceMonth ?? '').localeCompare(b.statementReferenceMonth ?? ''),
            multiple: 5,
          },
        },
        {
          title: t('finances.transactions.effectiveDate'),
          key: 'effectiveDate',
          width: 110,
          render: (_, tx) => (tx.statementDueDate ? formatDate(tx.statementDueDate) : '—'),
          sorter: {
            compare: (a, b) =>
              (a.statementDueDate ?? '').localeCompare(b.statementDueDate ?? ''),
            multiple: 6,
          },
        },
      ],
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
          const nameA = (() => { const id = a.systemCategoryId ?? a.userCategoryId; return id ? (categoryNames.get(id) ?? '') : '' })()
          const nameB = (() => { const id = b.systemCategoryId ?? b.userCategoryId; return id ? (categoryNames.get(id) ?? '') : '' })()
          return nameA.localeCompare(nameB)
        },
        multiple: 7,
      },
    },
    {
      title: t('finances.transactions.kind'),
      dataIndex: 'kind',
      render: (_, tx) => t(transactionKindLabelKey(tx.kind)),
      sorter: { compare: (a, b) => a.kind.localeCompare(b.kind), multiple: 8 },
    },
    {
      title: t('finances.transactions.status'),
      dataIndex: 'status',
      render: (_, tx) => <EnumTag meta={TRANSACTION_STATUS_META[tx.status]} />,
      sorter: { compare: (a, b) => a.status.localeCompare(b.status), multiple: 9 },
    },
    {
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
      align: 'right',
      render: (_, tx) => (
        <CurrencyAmount amount={tx.amount} currency={tx.currency} direction={kindDirection(tx.kind)} />
      ),
      sorter: { compare: (a, b) => a.amount - b.amount, multiple: 10 },
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'right',
      render: (_, tx) => (
        <Space>
          {tx.status === 'pending' && (
            <Button size="small" onClick={() => handlePost(tx)}>
              {t('finances.transactions.post')}
            </Button>
          )}
          {tx.status !== 'void' && (
            <Popconfirm
              title={t('finances.transactions.voidConfirm')}
              okText={t('finances.transactions.void')}
              cancelText={t('common.cancel')}
              onConfirm={() => handleVoid(tx)}
            >
              <Button size="small" danger>
                {t('finances.transactions.void')}
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ]

  return (
    <Card>
      <Flex justify="space-between" align="center" wrap gap="small" className="mb-4">
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('nav.transactions')}
        </Typography.Title>
        <Space>
          <Button icon={<SwapOutlined />} onClick={() => setTransferOpen(true)}>
            {t('finances.transactions.transfer')}
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setFormOpen(true)}>
            {t('finances.transactions.new')}
          </Button>
        </Space>
      </Flex>

      <Row gutter={[8, 8]} className="mb-4">
        <Col xs={24} sm={12} lg={8}>
          <Input.Search
            allowClear
            placeholder={t('common.search')}
            onSearch={(v) => upd('text', v || undefined)}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <DatePicker.RangePicker
            style={{ width: '100%' }}
            format="YYYY-MM-DD"
            placeholder={[t('finances.filters.from'), t('finances.filters.to')]}
            onChange={(_, s) => {
              upd('occurredFrom', s[0] || undefined)
              upd('occurredTo', s[1] || undefined)
            }}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <DatePicker.RangePicker
            style={{ width: '100%' }}
            format="YYYY-MM-DD"
            placeholder={[t('finances.transactions.effectiveDate') + ' ' + t('finances.filters.from'), t('finances.transactions.effectiveDate') + ' ' + t('finances.filters.to')]}
            onChange={(_, s) => {
              upd('dueDateFrom', s[0] || undefined)
              upd('dueDateTo', s[1] || undefined)
            }}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Select
            mode="multiple"
            allowClear
            style={{ width: '100%' }}
            placeholder={t('finances.filters.accounts')}
            optionFilterProp="label"
            onChange={(v: string[]) => upd('accountIds', v.length ? v : undefined)}
            options={(accounts ?? []).map((a) => ({ value: a.id, label: a.name }))}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Select
            mode="multiple"
            allowClear
            style={{ width: '100%' }}
            placeholder={t('finances.filters.cards')}
            optionFilterProp="label"
            onChange={(v: string[]) => upd('cardIds', v.length ? v : undefined)}
            options={(cards ?? []).map((c) => ({
              value: c.id,
              label: c.lastFour ? `${c.name} ····${c.lastFour}` : c.name,
            }))}
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
        <Col xs={24} sm={12} lg={8}>
          <Select
            mode="multiple"
            allowClear
            style={{ width: '100%' }}
            placeholder={t('finances.transactions.category')}
            optionFilterProp="label"
            onChange={(v: string[]) => upd('categoryIds', v.length ? v : undefined)}
            options={categoryOptions}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Select
            mode="multiple"
            allowClear
            style={{ width: '100%' }}
            placeholder={t('finances.filters.referenceMonth')}
            onChange={(v: string[]) => upd('referenceMonths', v.length ? v : undefined)}
            options={referenceMonthOptions}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Space.Compact style={{ width: '100%' }}>
            <InputNumber
              style={{ width: '50%' }}
              placeholder={t('finances.transactions.amount') + ' ' + t('finances.filters.from')}
              onChange={(v) => upd('amountMin', v ?? undefined)}
            />
            <InputNumber
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
        dataSource={filteredData}
        columns={columns}
        size="middle"
        pagination={{ pageSize: 25, showTotal: (total) => `${total} registros` }}
      />

      <TransactionFormModal open={formOpen} onClose={() => setFormOpen(false)} />
      <TransferFormModal open={transferOpen} onClose={() => setTransferOpen(false)} />
    </Card>
  )
}
