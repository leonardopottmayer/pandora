import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Dropdown, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { EllipsisOutlined, PlusOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CardDto, CardNumberDto } from '../../models'
import { cardNumberLabel } from '../../lib/cardNumbers'
import { useDeleteCardNumber } from '../../hooks/useCards'
import { CardNumberFormModal } from './CardNumberFormModal'

/** The card's physical and virtual numbers. They all bill on the card's statements. */
export function CardNumbersTab({ card }: { card: CardDto }) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const [editing, setEditing] = useState<CardNumberDto | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const remove = useDeleteCardNumber()

  function openForm(number: CardNumberDto | null) {
    setEditing(number)
    setFormOpen(true)
  }

  function confirmRemove(number: CardNumberDto) {
    modal.confirm({
      title: t('finances.cards.removeNumberConfirm'),
      content: <div className="flex flex-col gap-2">
        <span>{cardNumberLabel(number, t)}</span>
        <Typography.Text type="secondary">{t('finances.cards.removeNumberHint')}</Typography.Text>
      </div>,
      okText: t('finances.cards.removeNumber'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const deleted = await remove.mutateAsync({ cardId: card.id, numberId: number.id })
          message.success(deleted ? t('finances.cards.numberRemoved') : t('finances.cards.numberArchived'))
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.cards.numberSaveError')))
        }
      },
    })
  }

  const columns: ColumnsType<CardNumberDto> = [
    {
      title: t('finances.cards.numbersTab'),
      key: 'number',
      render: (_, number) => (
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex min-w-0 items-center gap-2">
            <span className="truncate" style={number.archivedAt ? { color: token.colorTextTertiary } : undefined}>
              {number.nickname ?? t(`finances.cards.numberKinds.${number.kind}`)}
            </span>
            {number.archivedAt && <Tag>{t('finances.cards.numberArchivedStatus')}</Tag>}
          </div>
          <Typography.Text type="secondary" className="truncate text-xs tabular-nums">
            {`${t(`finances.cards.numberKinds.${number.kind}`)} · ····${number.lastFour}`}
          </Typography.Text>
        </div>
      ),
    },
    {
      key: 'actions',
      width: 48,
      align: 'right',
      // An archived number stays only for the transactions that used it: nothing left to do with it.
      render: (_, number) => !number.archivedAt && !card.archivedAt && (
        <span onClick={(event) => event.stopPropagation()}>
          <Dropdown
            trigger={['click']}
            placement="bottomRight"
            menu={{ items: [
              { key: 'edit', label: t('common.edit'), onClick: () => openForm(number) },
              { type: 'divider' },
              { key: 'remove', danger: true, label: t('finances.cards.removeNumber'), onClick: () => confirmRemove(number) },
            ] }}
          >
            <Button
              type="text"
              size="small"
              icon={<EllipsisOutlined />}
              aria-label={t('finances.cards.numberMoreActions', { name: cardNumberLabel(number, t) })}
            />
          </Dropdown>
        </span>
      ),
    },
  ]

  return <div className="flex min-h-0 flex-col gap-4 lg:h-full">
    {!card.archivedAt && (
      <div className="flex shrink-0">
        <Button icon={<PlusOutlined />} onClick={() => openForm(null)}>{t('finances.cards.newNumber')}</Button>
      </div>
    )}
    {/* A card has a handful of numbers: the panel grows with them and the page scrolls, no inner scroller. */}
    <div
      className="overflow-hidden rounded-xl"
      style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}
    >
      <Table<CardNumberDto>
        rowKey="id"
        size="middle"
        tableLayout="fixed"
        showHeader={false}
        pagination={false}
        columns={columns}
        dataSource={card.numbers}
        onRow={(number) => number.archivedAt || card.archivedAt ? {} : { onClick: () => openForm(number), className: 'cursor-pointer' }}
        locale={{ emptyText: t('finances.cards.numbersEmpty') }}
      />
    </div>
    <CardNumberFormModal open={formOpen} cardId={card.id} number={editing} onClose={() => setFormOpen(false)} />
  </div>
}
