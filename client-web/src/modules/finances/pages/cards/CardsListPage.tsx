import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Card, Checkbox, Flex, Popconfirm, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CardDto } from '../../models'
import { formatMoney } from '../../lib/format'
import { useCards, useDeleteCard, useSetCardArchived } from '../../hooks/useCards'
import { CardFormModal } from './CardFormModal'

export function CardsListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [includeArchived, setIncludeArchived] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<CardDto | null>(null)

  const { data, isLoading } = useCards({ includeArchived })
  const deleteMutation = useDeleteCard()
  const archiveMutation = useSetCardArchived()

  async function handleArchive(card: CardDto) {
    const archived = !card.archivedAt
    try {
      await archiveMutation.mutateAsync({ id: card.id, archived })
      message.success(archived ? t('finances.cards.archived') : t('finances.cards.unarchived'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.cards.saveError')))
    }
  }

  async function handleDelete(card: CardDto) {
    try {
      await deleteMutation.mutateAsync(card.id)
      message.success(t('finances.cards.deleted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.cards.deleteError')))
    }
  }

  const columns: ColumnsType<CardDto> = [
    {
      title: t('finances.cards.name'),
      dataIndex: 'name',
      render: (name: string, card) => <Link to={`/finances/cards/${card.id}`}>{name}</Link>,
    },
    { title: t('finances.cards.brand'), dataIndex: 'brand', render: (v: string | null) => v ?? '—' },
    {
      title: t('finances.cards.lastFour'),
      dataIndex: 'lastFour',
      render: (v: string | null) => (v ? `····${v}` : '—'),
    },
    {
      title: t('finances.cards.creditLimit'),
      dataIndex: 'creditLimit',
      render: (v: number | null, card) => (v != null ? formatMoney(v, card.currency) : '—'),
    },
    {
      title: t('finances.cards.cycle'),
      key: 'cycle',
      render: (_, card) => t('finances.cards.cycleValue', { closing: card.closingDay, due: card.dueDay }),
    },
    {
      title: t('finances.accounts.status'),
      dataIndex: 'archivedAt',
      render: (archivedAt: string | null) =>
        archivedAt ? (
          <Tag>{t('finances.accounts.archivedStatus')}</Tag>
        ) : (
          <Tag color="green">{t('finances.accounts.activeStatus')}</Tag>
        ),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'right',
      render: (_, card) => (
        <Space>
          <Button
            size="small"
            onClick={() => {
              setEditing(card)
              setModalOpen(true)
            }}
          >
            {t('common.edit')}
          </Button>
          <Button size="small" onClick={() => handleArchive(card)}>
            {card.archivedAt ? t('finances.accounts.unarchive') : t('finances.accounts.archive')}
          </Button>
          <Popconfirm
            title={t('finances.cards.deleteConfirm')}
            okText={t('common.delete')}
            cancelText={t('common.cancel')}
            onConfirm={() => handleDelete(card)}
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
          {t('nav.cards')}
        </Typography.Title>
        <Space>
          <Checkbox checked={includeArchived} onChange={(e) => setIncludeArchived(e.target.checked)}>
            {t('finances.accounts.showArchived')}
          </Checkbox>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              setEditing(null)
              setModalOpen(true)
            }}
          >
            {t('finances.cards.new')}
          </Button>
        </Space>
      </Flex>

      <Table rowKey="id" loading={isLoading} dataSource={data} columns={columns} pagination={false} />

      <CardFormModal open={modalOpen} card={editing} onClose={() => setModalOpen(false)} />
    </Card>
  )
}
