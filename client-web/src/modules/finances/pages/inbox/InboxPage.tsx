import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Flex, Input, Modal, Popconfirm, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { toErrorMessage } from '@/lib/api/envelope'
import type { PendingTransactionDto } from '../../models'
import { kindDirection, transactionKindLabelKey } from '../../lib/enums'
import { formatDate } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { useAccountNames } from '../../hooks/useAccounts'
import { useCardNames } from '../../hooks/useCards'
import { useCategoryNames } from '../../hooks/useCategories'
import {
  useApprovePendingTransaction,
  useApprovePendingTransactionBatch,
  usePendingTransactions,
  useRejectPendingTransaction,
} from '../../hooks/usePendingTransactions'
import { PendingTransactionEditModal } from './PendingTransactionEditModal'

export function InboxPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([])
  const [editing, setEditing] = useState<PendingTransactionDto | null>(null)
  const [editOpen, setEditOpen] = useState(false)
  const [rejecting, setRejecting] = useState<PendingTransactionDto | null>(null)
  const [rejectReason, setRejectReason] = useState('')

  const { data, isLoading } = usePendingTransactions({ take: 1000 })
  const accountNames = useAccountNames()
  const cardNames = useCardNames()
  const categoryNames = useCategoryNames()
  const approveMutation = useApprovePendingTransaction()
  const rejectMutation = useRejectPendingTransaction()
  const batchMutation = useApprovePendingTransactionBatch()

  function openEdit(pending: PendingTransactionDto) {
    setEditing(pending)
    setEditOpen(true)
  }

  async function handleApprove(p: PendingTransactionDto) {
    try {
      await approveMutation.mutateAsync(p.id)
      message.success(t('finances.inbox.approved'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.actionError')))
    }
  }

  async function confirmReject() {
    if (!rejecting) return
    try {
      await rejectMutation.mutateAsync({ id: rejecting.id, reason: rejectReason || null })
      message.success(t('finances.inbox.rejected'))
      setRejecting(null)
      setRejectReason('')
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.actionError')))
    }
  }

  async function handleApproveSelected() {
    try {
      const count = await batchMutation.mutateAsync(selectedRowKeys.map(String))
      message.success(t('finances.inbox.batchApproved', { count }))
      setSelectedRowKeys([])
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.inbox.actionError')))
    }
  }

  const columns: ColumnsType<PendingTransactionDto> = [
    {
      title: t('finances.transactions.occurredOn'),
      dataIndex: 'occurredOn',
      width: 110,
      render: (value: string) => formatDate(value),
      sorter: (a, b) => a.occurredOn.localeCompare(b.occurredOn),
    },
    {
      title: t('finances.transactions.description'),
      dataIndex: 'description',
      sorter: (a, b) => a.description.localeCompare(b.description),
    },
    {
      title: t('finances.recurring.destination'),
      key: 'destination',
      render: (_, p) =>
        p.cardId
          ? cardNames.get(p.cardId) ?? '—'
          : p.accountId
            ? accountNames.get(p.accountId) ?? '—'
            : '—',
    },
    {
      title: t('finances.transactions.category'),
      key: 'category',
      render: (_, p) => {
        const categoryId = p.systemCategoryId ?? p.userCategoryId
        return categoryId ? categoryNames.get(categoryId) ?? '—' : '—'
      },
    },
    {
      title: t('finances.inbox.source'),
      dataIndex: 'source',
      render: (_, p) => <Tag>{t(`finances.inbox.sources.${p.source}`)}</Tag>,
    },
    {
      title: t('finances.transactions.kind'),
      dataIndex: 'kind',
      render: (_, p) => t(transactionKindLabelKey(p.kind)),
    },
    {
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
      align: 'right',
      render: (_, p) =>
        p.amount == null ? (
          '—'
        ) : (
          <CurrencyAmount amount={p.amount} currency={p.currency} direction={kindDirection(p.kind)} />
        ),
      sorter: (a, b) => (a.amount ?? 0) - (b.amount ?? 0),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'right',
      render: (_, p) => (
        <Space>
          <Button size="small" type="primary" onClick={() => handleApprove(p)}>
            {t('finances.inbox.approve')}
          </Button>
          <Button size="small" onClick={() => openEdit(p)}>
            {t('common.edit')}
          </Button>
          <Button size="small" danger onClick={() => setRejecting(p)}>
            {t('finances.inbox.reject')}
          </Button>
        </Space>
      ),
    },
  ]

  return (
    <Card>
      <Flex justify="space-between" align="center" wrap gap="small" className="mb-4">
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('nav.inbox')}
        </Typography.Title>
        {selectedRowKeys.length > 0 && (
          <Popconfirm
            title={t('finances.inbox.batchApproveConfirm', { count: selectedRowKeys.length })}
            okText={t('finances.inbox.approve')}
            cancelText={t('common.cancel')}
            onConfirm={handleApproveSelected}
          >
            <Button type="primary" loading={batchMutation.isPending}>
              {t('finances.inbox.approveSelected', { count: selectedRowKeys.length })}
            </Button>
          </Popconfirm>
        )}
      </Flex>

      <Table
        rowKey="id"
        loading={isLoading}
        dataSource={data}
        columns={columns}
        size="middle"
        rowSelection={{ selectedRowKeys, onChange: setSelectedRowKeys }}
        pagination={{ pageSize: 25, showTotal: (total) => `${total} registros` }}
      />

      <PendingTransactionEditModal
        open={editOpen}
        pending={editing}
        onClose={() => setEditOpen(false)}
      />

      <Modal
        open={!!rejecting}
        title={t('finances.inbox.rejectTitle')}
        onCancel={() => {
          setRejecting(null)
          setRejectReason('')
        }}
        onOk={confirmReject}
        okButtonProps={{ danger: true, loading: rejectMutation.isPending }}
        okText={t('finances.inbox.reject')}
        cancelText={t('common.cancel')}
        destroyOnHidden
      >
        <Input.TextArea
          rows={3}
          maxLength={500}
          value={rejectReason}
          onChange={(e) => setRejectReason(e.target.value)}
          placeholder={t('finances.inbox.rejectReasonPlaceholder')}
        />
      </Modal>
    </Card>
  )
}
