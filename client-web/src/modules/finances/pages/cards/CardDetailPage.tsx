import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { Breadcrumb, Button, Empty, Skeleton, Tabs, Tag, theme } from 'antd'
import { EditOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { formatMoney } from '../../lib/format'
import { StatTile } from '../../components/StatTile'
import { useCard, useCardAvailableLimit, useCardInstallmentPlans, useCardStatements } from '../../hooks/useCards'
import { CardFormModal } from './CardFormModal'
import { CardStatementsTab } from './CardStatementsTab'
import { CardInstallmentsTab } from './CardInstallmentsTab'

export function CardDetailPage() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const { id = '' } = useParams<{ id: string }>()
  const [editing, setEditing] = useState(false)
  const { data: card, isLoading } = useCard(id)
  const { data: limit } = useCardAvailableLimit(id)
  const { data: statements, isLoading: loadingStatements } = useCardStatements(id)
  const { data: plans, isLoading: loadingPlans } = useCardInstallmentPlans(id)

  if (isLoading) return <Skeleton active />
  if (!card) return <Empty description={t('finances.cards.notFound')}><Link to="/finances/cards">{t('nav.cards')}</Link></Empty>

  const money = (amount: number | null | undefined) => amount == null ? '—' : (
    <span className="tabular-nums" style={amount < 0 ? { color: token.colorError } : undefined}>
      {formatMoney(amount, card.currency)}
    </span>
  )
  const description = [
    card.brand, card.lastFour ? `····${card.lastFour}` : null,
    t('finances.cards.cycleValue', { closing: card.closingDay, due: card.dueDay }),
  ].filter(Boolean).join(' · ')

  return (
    <div className="flex min-w-0 flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <Breadcrumb className="shrink-0" items={[
        { title: <Link to="/finances/cards">{t('nav.cards')}</Link> }, { title: card.name },
      ]} />
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1 break-words">
          <PageHeading title={card.name} description={description} />
          {card.archivedAt && <Tag className="mt-2">{t('finances.accounts.archivedStatus')}</Tag>}
        </div>
        <Button icon={<EditOutlined />} onClick={() => setEditing(true)}>{t('finances.cards.editTitle')}</Button>
      </div>
      <div className="flex shrink-0 flex-wrap gap-3">
        <StatTile label={t('finances.cards.creditLimit')} value={money(card.creditLimit)} />
        <StatTile label={t('finances.cards.availableLimit')} value={money(limit?.availableLimit)} />
      </div>
      <Tabs
        className="min-h-0 flex-1 [&_.ant-tabs-content]:lg:h-full"
        defaultActiveKey="statements"
        destroyOnHidden={false}
        animated={false}
        items={[
          {
            key: 'statements', label: t('finances.statements.title'), className: 'lg:h-full',
            children: <CardStatementsTab statements={statements ?? []} loading={loadingStatements} currency={card.currency} />,
          },
          {
            key: 'installments', label: t('finances.installments.title'), className: 'lg:h-full',
            children: <CardInstallmentsTab plans={plans ?? []} loading={loadingPlans} currency={card.currency} />,
          },
        ]}
      />
      <CardFormModal open={editing} card={card} onClose={() => setEditing(false)} />
    </div>
  )
}
