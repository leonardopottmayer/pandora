import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Flex, Popconfirm, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { RecurringTransactionDto } from '../../models'
import { RECURRING_STATUS_META, kindDirection, recurrenceFrequencyLabelKey } from '../../lib/enums'
import { formatDate } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { EnumTag } from '../../components/EnumTag'
import { useAccountNames, useAccounts } from '../../hooks/useAccounts'
import { useCardNames, useCards } from '../../hooks/useCards'
import { useCategoryNames } from '../../hooks/useCategories'
import {
  useDeleteRecurringTransaction,
  useRecurringTransactions,
  useSetRecurringPaused,
} from '../../hooks/useRecurringTransactions'
import { RecurringTransactionFormModal } from './RecurringTransactionFormModal'

export function RecurringTransactionsListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<RecurringTransactionDto | null>(null)

  const { data, isLoading } = useRecurringTransactions()
  const { data: accounts } = useAccounts()
  const { data: cards } = useCards()
  const accountNames = useAccountNames()
  const cardNames = useCardNames()
  const categoryNames = useCategoryNames()
  const deleteMutation = useDeleteRecurringTransaction()
  const pauseMutation = useSetRecurringPaused()

  const currencyByAccount = useMemo(() => {
    const map = new Map<string, string>()
    for (const a of accounts ?? []) map.set(a.id, a.currency)
    return map
  }, [accounts])
  const currencyByCard = useMemo(() => {
    const map = new Map<string, string>()
    for (const c of cards ?? []) map.set(c.id, c.currency)
    return map
  }, [cards])

  function currencyOf(r: RecurringTransactionDto): string {
    if (r.cardId) return currencyByCard.get(r.cardId) ?? 'BRL'
    if (r.accountId) return currencyByAccount.get(r.accountId) ?? 'BRL'
    return 'BRL'
  }

  function openCreate() {
    setEditing(null)
    setModalOpen(true)
  }

  function openEdit(recurring: RecurringTransactionDto) {
    setEditing(recurring)
    setModalOpen(true)
  }

  function frequencyText(r: RecurringTransactionDto): string {
    const unit = t(recurrenceFrequencyLabelKey(r.frequency))
    return r.interval === 1 ? unit : t('finances.recurring.everyN', { n: r.interval, unit })
  }

  async function handlePauseToggle(r: RecurringTransactionDto) {
    const paused = r.status === 'active'
    try {
      await pauseMutation.mutateAsync({ id: r.id, paused })
      message.success(paused ? t('finances.recurring.paused') : t('finances.recurring.resumed'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.recurring.saveError')))
    }
  }

  async function handleDelete(r: RecurringTransactionDto) {
    try {
      await deleteMutation.mutateAsync(r.id)
      message.success(t('finances.recurring.deleted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.recurring.deleteError')))
    }
  }

  const columns: ColumnsType<RecurringTransactionDto> = [
    {
      title: t('finances.recurring.name'),
      dataIndex: 'name',
      sorter: (a, b) => a.name.localeCompare(b.name),
    },
    {
      title: t('finances.recurring.destination'),
      key: 'destination',
      render: (_, r) =>
        r.cardId
          ? cardNames.get(r.cardId) ?? '—'
          : r.accountId
            ? accountNames.get(r.accountId) ?? '—'
            : '—',
    },
    {
      title: t('finances.recurring.frequency'),
      key: 'frequency',
      render: (_, r) => frequencyText(r),
    },
    {
      title: t('finances.transactions.category'),
      key: 'category',
      render: (_, r) => {
        const categoryId = r.systemCategoryId ?? r.userCategoryId
        return categoryId ? categoryNames.get(categoryId) ?? '—' : '—'
      },
    },
    {
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
      align: 'right',
      render: (_, r) =>
        r.amount == null ? (
          '—'
        ) : (
          <Space size={4}>
            <CurrencyAmount amount={r.amount} currency={currencyOf(r)} direction={kindDirection(r.kind)} />
            {r.amountIsEstimate && <Tag>{t('finances.recurring.estimate')}</Tag>}
          </Space>
        ),
    },
    {
      title: t('finances.recurring.nextOccurrence'),
      dataIndex: 'nextOccurrenceOn',
      render: (_, r) => (r.status === 'finished' ? '—' : formatDate(r.nextOccurrenceOn)),
      sorter: (a, b) => a.nextOccurrenceOn.localeCompare(b.nextOccurrenceOn),
    },
    {
      title: t('finances.recurring.occurrences'),
      dataIndex: 'occurrencesCount',
      align: 'right',
      render: (_, r) => (r.maxOccurrences ? `${r.occurrencesCount}/${r.maxOccurrences}` : r.occurrencesCount),
    },
    {
      title: t('finances.transactions.status'),
      dataIndex: 'status',
      render: (_, r) => <EnumTag meta={RECURRING_STATUS_META[r.status]} />,
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'right',
      render: (_, r) => (
        <Space>
          <Button size="small" onClick={() => openEdit(r)}>
            {t('common.edit')}
          </Button>
          {r.status !== 'finished' && (
            <Button size="small" onClick={() => handlePauseToggle(r)}>
              {r.status === 'active' ? t('finances.recurring.pause') : t('finances.recurring.resume')}
            </Button>
          )}
          <Popconfirm
            title={t('finances.recurring.deleteConfirm')}
            okText={t('common.delete')}
            cancelText={t('common.cancel')}
            onConfirm={() => handleDelete(r)}
          >
            <Button size="small" danger>
              {t('common.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Card>
      <Flex justify="space-between" align="center" wrap gap="small" className="mb-4">
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('nav.recurring')}
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('finances.recurring.new')}
        </Button>
      </Flex>

      <Table
        rowKey="id"
        loading={isLoading}
        dataSource={data}
        columns={columns}
        size="middle"
        pagination={{ pageSize: 25, showTotal: (total) => `${total} registros` }}
      />

      <RecurringTransactionFormModal
        open={modalOpen}
        recurring={editing}
        onClose={() => setModalOpen(false)}
      />
    </Card>
  )
}
