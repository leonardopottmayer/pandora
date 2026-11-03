import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Switch, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { EllipsisOutlined, PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CardDto } from '../../models'
import { formatMoney } from '../../lib/format'
import { useCards, useDeleteCard, useSetCardArchived } from '../../hooks/useCards'
import { CardFormModal } from './CardFormModal'

export function CardsListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [includeArchived, setIncludeArchived] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<CardDto | null>(null)
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)

  const { data: cards = [], isLoading } = useCards({ includeArchived })
  const deleteMutation = useDeleteCard()
  const archiveMutation = useSetCardArchived()

  function openForm(card: CardDto | null) {
    setEditing(card)
    setModalOpen(true)
  }

  async function handleArchive(card: CardDto) {
    const archived = !card.archivedAt
    try {
      await archiveMutation.mutateAsync({ id: card.id, archived })
      message.success(archived ? t('finances.cards.archived') : t('finances.cards.unarchived'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.cards.saveError')))
    }
  }

  function confirmArchive(card: CardDto) {
    if (card.archivedAt) {
      void handleArchive(card)
      return
    }
    modal.confirm({
      title: t('finances.cards.archiveConfirm'),
      content: card.name,
      okText: t('finances.accounts.archive'),
      cancelText: t('common.cancel'),
      onOk: () => handleArchive(card),
    })
  }

  function confirmDelete(card: CardDto) {
    modal.confirm({
      title: t('finances.cards.deleteConfirm'),
      content: card.name,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          await deleteMutation.mutateAsync(card.id)
          message.success(t('finances.cards.deleted'))
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.cards.deleteError')))
        }
      },
    })
  }

  const columns: ColumnsType<CardDto> = [
    {
      title: t('finances.cards.name'),
      dataIndex: 'name',
      render: (name: string, card) => (
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex min-w-0 items-center gap-2">
            <Link
              className="min-w-0 truncate"
              title={name}
              to={`/finances/cards/${card.id}`}
              style={card.archivedAt ? { color: token.colorTextTertiary } : undefined}
            >{name}</Link>
            {card.archivedAt && <Tag>{t('finances.accounts.archivedStatus')}</Tag>}
          </div>
          <Typography.Text type="secondary" className="truncate text-xs">
            {[card.brand, card.lastFour ? `····${card.lastFour}` : null].filter(Boolean).join(' · ')}
          </Typography.Text>
          <Typography.Text type="secondary" className="text-xs md:hidden">
            {t('finances.cards.cycleValue', { closing: card.closingDay, due: card.dueDay })}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: t('finances.cards.cycle'),
      key: 'cycle',
      width: 200,
      responsive: ['md'],
      render: (_, card) => <span className="tabular-nums">
        {t('finances.cards.cycleValue', { closing: card.closingDay, due: card.dueDay })}
      </span>,
    },
    {
      title: t('finances.cards.creditLimit'),
      dataIndex: 'creditLimit',
      width: 124,
      align: 'right',
      render: (value: number | null, card) => value != null
        ? <span className="tabular-nums">{formatMoney(value, card.currency)}</span>
        : <Typography.Text type="secondary">—</Typography.Text>,
    },
    {
      key: 'actions',
      width: 48,
      align: 'right',
      render: (_, card) => (
        <span onClick={(event) => event.stopPropagation()}>
          <Dropdown
            trigger={['click']}
            placement="bottomRight"
            menu={{ items: [
              { key: 'edit', label: t('common.edit'), onClick: () => openForm(card) },
              {
                key: 'archive',
                label: card.archivedAt ? t('finances.accounts.unarchive') : t('finances.accounts.archive'),
                onClick: () => confirmArchive(card),
              },
              { type: 'divider' },
              { key: 'delete', danger: true, label: t('common.delete'), onClick: () => confirmDelete(card) },
            ] }}
          >
            <Button
              type="text"
              size="small"
              icon={<EllipsisOutlined />}
              aria-label={t('finances.cards.moreActions', { name: card.name })}
            />
          </Dropdown>
        </span>
      ),
    },
  ]

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.cards')} description={t('finances.cards.subtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
          {t('finances.cards.new')}
        </Button>
      </div>
      <label className="flex w-fit shrink-0 cursor-pointer items-center gap-2 text-sm" style={{ color: token.colorTextSecondary }}>
        <Switch size="small" checked={includeArchived} onChange={setIncludeArchived} />
        {t('finances.accounts.showArchived')}
      </label>
      <div
        className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl"
        style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
      >
        <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
          <Table
            rowKey="id"
            loading={isLoading}
            dataSource={cards}
            columns={columns}
            tableLayout="fixed"
            size="middle"
            sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
            onRow={(card) => ({ onClick: () => navigate(`/finances/cards/${card.id}`), className: 'cursor-pointer' })}
            locale={{ emptyText: t('finances.cards.empty') }}
            pagination={false}
          />
        </div>
        {cards.length > 0 && <Typography.Text
          type="secondary"
          className="shrink-0 px-4 py-2 text-xs tabular-nums"
          style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}
        >{t('finances.cards.count', { count: cards.length })}</Typography.Text>}
      </div>
      <CardFormModal open={modalOpen} card={editing} onClose={() => setModalOpen(false)} />
    </div>
  )
}
