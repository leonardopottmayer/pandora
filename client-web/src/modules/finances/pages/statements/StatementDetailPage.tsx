import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { App, Breadcrumb, Button, Card, Col, Row, Space, Statistic, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TransactionDto } from '../../models'
import { STATEMENT_STATUS_META, kindDirection, transactionKindLabelKey } from '../../lib/enums'
import { formatDate, formatMoney, formatReferenceMonth } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { EnumTag } from '../../components/EnumTag'
import { useCard } from '../../hooks/useCards'
import { useCategoryNames } from '../../hooks/useCategories'
import { useCloseStatement, useStatement } from '../../hooks/useStatements'
import { PayStatementModal } from './PayStatementModal'

export function StatementDetailPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { id = '' } = useParams<{ id: string }>()
  const [payOpen, setPayOpen] = useState(false)

  const { data, isLoading } = useStatement(id)
  const closeMutation = useCloseStatement()
  const categoryNames = useCategoryNames()

  const statement = data?.statement
  const { data: card } = useCard(statement?.cardId ?? '')
  // A fatura não carrega a moeda; deriva da primeira transação (mesma do cartão).
  const currency = data?.transactions?.[0]?.currency ?? 'BRL'

  async function handleClose() {
    try {
      await closeMutation.mutateAsync(id)
      message.success(t('finances.statements.closed'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.statements.saveError')))
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
      title: t('finances.transactions.amount'),
      dataIndex: 'amount',
      align: 'right',
      render: (_, tx) => (
        <CurrencyAmount amount={tx.amount} currency={tx.currency} direction={kindDirection(tx.kind)} />
      ),
    },
  ]

  const canClose = statement?.status === 'open'
  const canPay = statement != null && statement.status !== 'paid'

  return (
    <div className="flex flex-col gap-4">
      <Breadcrumb
        items={[
          { title: <Link to="/finances/cards">{t('nav.cards')}</Link> },
          card
            ? { title: <Link to={`/finances/cards/${card.id}`}>{card.name}</Link> }
            : { title: '…' },
          { title: statement ? formatReferenceMonth(statement.referenceMonth) : '…' },
        ]}
      />

      <Card loading={isLoading}>
        <Typography.Title level={4} style={{ marginTop: 0 }}>
          {statement ? formatReferenceMonth(statement.referenceMonth) : '…'}
        </Typography.Title>
        {statement && (
          <>
            <Space className="mb-4">
              <EnumTag meta={STATEMENT_STATUS_META[statement.status]} />
              {canClose && (
                <Button onClick={handleClose} loading={closeMutation.isPending}>
                  {t('finances.statements.close')}
                </Button>
              )}
              {canPay && (
                <Button type="primary" onClick={() => setPayOpen(true)}>
                  {t('finances.statements.pay')}
                </Button>
              )}
            </Space>
            <Row gutter={16}>
              <Col xs={8}>
                <Statistic
                  title={t('finances.statements.total')}
                  value={formatMoney(statement.totalAmount, currency)}
                />
              </Col>
              <Col xs={8}>
                <Statistic
                  title={t('finances.statements.paidAmount')}
                  value={formatMoney(statement.paidAmount, currency)}
                />
              </Col>
              <Col xs={8}>
                <Statistic
                  title={t('finances.statements.remaining')}
                  value={formatMoney(statement.remainingAmount, currency)}
                />
              </Col>
            </Row>
          </>
        )}
      </Card>

      <Card title={t('finances.statements.transactions')}>
        <Table
          rowKey="id"
          loading={isLoading}
          dataSource={data?.transactions}
          columns={columns}
          size="middle"
          pagination={{ pageSize: 20 }}
        />
      </Card>

      <PayStatementModal
        open={payOpen}
        statementId={id}
        remainingAmount={statement?.remainingAmount}
        onClose={() => setPayOpen(false)}
      />
    </div>
  )
}
