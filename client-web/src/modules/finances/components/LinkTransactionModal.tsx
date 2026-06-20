import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Input, Modal, Table } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { TransactionDto } from '../models'
import { useTransactions } from '../hooks/useTransactions'
import { kindDirection } from '../lib/enums'
import { formatDate } from '../lib/format'
import { CurrencyAmount } from './CurrencyAmount'

interface LinkTransactionModalProps {
  open: boolean
  /** Pre-fills the search box (e.g. the suggestion's description). */
  defaultSearch?: string
  /** Restricts the candidate list to a single account, when the suggestion targets one. */
  accountId?: string | null
  loading?: boolean
  onClose: () => void
  onPick: (transactionId: string) => void
}

/** Lets the user pick an existing transaction to reconcile an import suggestion against. */
export function LinkTransactionModal({
  open,
  defaultSearch,
  accountId,
  loading,
  onClose,
  onPick,
}: LinkTransactionModalProps) {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<string | null>(null)

  useEffect(() => {
    if (open) {
      setSearch(defaultSearch ?? '')
      setSelected(null)
    }
  }, [open, defaultSearch])

  const { data, isLoading } = useTransactions({
    text: search || undefined,
    accountId: accountId ?? undefined,
    take: 50,
  })

  const columns: ColumnsType<TransactionDto> = [
    {
      title: t('finances.transactions.occurredOn'),
      dataIndex: 'occurredOn',
      width: 110,
      render: (v: string) => formatDate(v),
    },
    {
      title: t('finances.transactions.description'),
      dataIndex: 'description',
      ellipsis: true,
    },
    {
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
      align: 'right',
      width: 140,
      render: (_, r) => (
        <CurrencyAmount amount={r.amount} currency={r.currency} direction={kindDirection(r.kind)} />
      ),
    },
  ]

  return (
    <Modal
      open={open}
      title={t('finances.imports.linkTitle')}
      onCancel={onClose}
      onOk={() => selected && onPick(selected)}
      okText={t('finances.imports.linkConfirm')}
      okButtonProps={{ disabled: !selected, loading }}
      cancelText={t('common.cancel')}
      width={640}
      destroyOnHidden
    >
      <Input.Search
        allowClear
        placeholder={t('finances.imports.linkSearchPlaceholder')}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="mb-3"
      />
      <Table<TransactionDto>
        rowKey="id"
        size="small"
        columns={columns}
        dataSource={data}
        loading={isLoading}
        pagination={{ pageSize: 8 }}
        rowSelection={{
          type: 'radio',
          selectedRowKeys: selected ? [selected] : [],
          onChange: (keys) => setSelected(keys[0] as string),
        }}
        onRow={(r) => ({ onClick: () => setSelected(r.id) })}
      />
    </Modal>
  )
}
