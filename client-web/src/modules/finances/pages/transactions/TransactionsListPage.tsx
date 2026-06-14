import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Flex, Input, Popconfirm, Select, Space, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PlusOutlined, SwapOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import {
  TRANSACTION_STATUSES,
  type TransactionDto,
  type TransactionFilters,
  type TransactionStatus,
} from '../../models'
import { TRANSACTION_STATUS_META, kindDirection, transactionKindLabelKey } from '../../lib/enums'
import { formatDate } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { EnumTag } from '../../components/EnumTag'
import { AccountSelect } from '../../components/AccountSelect'
import { useCategoryNames } from '../../hooks/useCategories'
import { usePostTransaction, useTransactions, useVoidTransaction } from '../../hooks/useTransactions'
import { TransactionFormModal } from './TransactionFormModal'
import { TransferFormModal } from './TransferFormModal'

export function TransactionsListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [filters, setFilters] = useState<TransactionFilters>({ take: 100 })
  const [formOpen, setFormOpen] = useState(false)
  const [transferOpen, setTransferOpen] = useState(false)

  const { data, isLoading } = useTransactions(filters)
  const categoryNames = useCategoryNames()
  const postMutation = usePostTransaction()
  const voidMutation = useVoidTransaction()

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
      width: 120,
      render: (value: string) => formatDate(value),
    },
    { title: t('finances.transactions.description'), dataIndex: 'description' },
    {
      title: t('finances.transactions.category'),
      key: 'category',
      render: (_, tx) => {
        const categoryId = tx.systemCategoryId ?? tx.userCategoryId
        return categoryId ? categoryNames.get(categoryId) ?? '—' : '—'
      },
    },
    {
      title: t('finances.transactions.kind'),
      dataIndex: 'kind',
      render: (_, tx) => t(transactionKindLabelKey(tx.kind)),
    },
    {
      title: t('finances.transactions.status'),
      dataIndex: 'status',
      render: (_, tx) => <EnumTag meta={TRANSACTION_STATUS_META[tx.status]} />,
    },
    {
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
      align: 'right',
      render: (_, tx) => (
        <CurrencyAmount amount={tx.amount} currency={tx.currency} direction={kindDirection(tx.kind)} />
      ),
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

      <Space wrap className="mb-4" style={{ width: '100%' }}>
        <div style={{ minWidth: 220 }}>
          <AccountSelect
            allowClear
            value={filters.accountId}
            onChange={(accountId) => setFilters((f) => ({ ...f, accountId }))}
          />
        </div>
        <Select
          allowClear
          style={{ minWidth: 160 }}
          placeholder={t('finances.transactions.status')}
          value={filters.status}
          onChange={(status?: TransactionStatus) => setFilters((f) => ({ ...f, status }))}
          options={TRANSACTION_STATUSES.map((s) => ({
            value: s,
            label: t(TRANSACTION_STATUS_META[s].labelKey),
          }))}
        />
        <Input.Search
          allowClear
          style={{ minWidth: 200 }}
          placeholder={t('common.search')}
          onSearch={(text) => setFilters((f) => ({ ...f, text: text || undefined }))}
        />
      </Space>

      <Table
        rowKey="id"
        loading={isLoading}
        dataSource={data}
        columns={columns}
        size="middle"
        pagination={{ pageSize: 25 }}
      />

      <TransactionFormModal open={formOpen} onClose={() => setFormOpen(false)} />
      <TransferFormModal open={transferOpen} onClose={() => setTransferOpen(false)} />
    </Card>
  )
}
