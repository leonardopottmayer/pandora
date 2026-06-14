import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { Breadcrumb, Card, Col, Row, Statistic, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { CardStatementDto, InstallmentPlanDto } from '../../models'
import { STATEMENT_STATUS_META } from '../../lib/enums'
import { formatDate, formatMoney, formatReferenceMonth } from '../../lib/format'
import { EnumTag } from '../../components/EnumTag'
import {
  useCard,
  useCardAvailableLimit,
  useCardInstallmentPlans,
  useCardStatements,
} from '../../hooks/useCards'
import { useCategoryNames } from '../../hooks/useCategories'

export function CardDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams<{ id: string }>()

  const { data: card, isLoading } = useCard(id)
  const { data: limit } = useCardAvailableLimit(id)
  const { data: statements, isLoading: loadingStatements } = useCardStatements(id)
  const { data: plans, isLoading: loadingPlans } = useCardInstallmentPlans(id)
  const categoryNames = useCategoryNames()

  const currency = card?.currency ?? 'BRL'

  const statementColumns: ColumnsType<CardStatementDto> = [
    {
      title: t('finances.statements.referenceMonth'),
      dataIndex: 'referenceMonth',
      render: (value: string, s) => (
        <Link to={`/finances/statements/${s.id}`}>{formatReferenceMonth(value)}</Link>
      ),
    },
    {
      title: t('finances.statements.dueDate'),
      dataIndex: 'dueDate',
      render: (value: string) => formatDate(value),
    },
    {
      title: t('finances.statements.status'),
      dataIndex: 'status',
      render: (_, s) => <EnumTag meta={STATEMENT_STATUS_META[s.status]} />,
    },
    {
      title: t('finances.statements.total'),
      dataIndex: 'totalAmount',
      align: 'right',
      render: (value: number) => formatMoney(value, currency),
    },
    {
      title: t('finances.statements.remaining'),
      dataIndex: 'remainingAmount',
      align: 'right',
      render: (value: number) => formatMoney(value, currency),
    },
  ]

  const planColumns: ColumnsType<InstallmentPlanDto> = [
    { title: t('finances.transactions.description'), dataIndex: 'description' },
    {
      title: t('finances.transactions.category'),
      key: 'category',
      render: (_, p) => {
        const categoryId = p.systemCategoryId ?? p.userCategoryId
        return categoryId ? categoryNames.get(categoryId) ?? '—' : '—'
      },
    },
    {
      title: t('finances.installments.count'),
      key: 'count',
      render: (_, p) => `${p.paidInstallments}/${p.installmentCount}`,
    },
    {
      title: t('finances.installments.total'),
      dataIndex: 'totalAmount',
      align: 'right',
      render: (value: number) => formatMoney(value, currency),
    },
    {
      title: t('finances.installments.remaining'),
      dataIndex: 'remainingAmount',
      align: 'right',
      render: (value: number) => formatMoney(value, currency),
    },
  ]

  return (
    <div className="flex flex-col gap-4">
      <Breadcrumb
        items={[
          { title: <Link to="/finances/cards">{t('nav.cards')}</Link> },
          { title: card?.name ?? '…' },
        ]}
      />

      <Card loading={isLoading}>
        <Typography.Title level={4} style={{ marginTop: 0 }}>
          {card?.name}
        </Typography.Title>
        <Row gutter={16}>
          <Col xs={12} md={8}>
            <Statistic
              title={t('finances.cards.creditLimit')}
              value={card?.creditLimit != null ? formatMoney(card.creditLimit, currency) : '—'}
            />
          </Col>
          <Col xs={12} md={8}>
            <Statistic
              title={t('finances.cards.availableLimit')}
              value={limit?.availableLimit != null ? formatMoney(limit.availableLimit, currency) : '—'}
            />
          </Col>
        </Row>
      </Card>

      <Card title={t('finances.statements.title')}>
        <Table
          rowKey="id"
          loading={loadingStatements}
          dataSource={statements}
          columns={statementColumns}
          size="middle"
          pagination={false}
        />
      </Card>

      <Card title={t('finances.installments.title')}>
        <Table
          rowKey="id"
          loading={loadingPlans}
          dataSource={plans}
          columns={planColumns}
          size="middle"
          pagination={false}
        />
      </Card>
    </div>
  )
}
