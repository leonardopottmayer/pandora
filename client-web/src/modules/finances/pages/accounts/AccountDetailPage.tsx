import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { Breadcrumb, Card, Col, Row, Statistic, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { TransactionDto } from '../../models'
import { TRANSACTION_STATUS_META, kindDirection } from '../../lib/enums'
import { formatDate, formatMoney } from '../../lib/format'
import { CurrencyAmount } from '../../components/CurrencyAmount'
import { EnumTag } from '../../components/EnumTag'
import { transactionKindLabelKey } from '../../lib/enums'
import { useAccount, useAccountBalance, useAccountTransactions } from '../../hooks/useAccounts'
import { useCategoryNames } from '../../hooks/useCategories'

export function AccountDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams<{ id: string }>()

  const { data: account, isLoading: loadingAccount } = useAccount(id)
  const { data: balance } = useAccountBalance(id)
  const { data: transactions, isLoading: loadingTx } = useAccountTransactions(id, { take: 100 })
  const categoryNames = useCategoryNames()

  const currency = account?.currency ?? balance?.currency ?? 'BRL'

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
  ]

  return (
    <div className="flex flex-col gap-4">
      <Breadcrumb
        items={[
          { title: <Link to="/finances/accounts">{t('nav.accounts')}</Link> },
          { title: account?.name ?? '…' },
        ]}
      />

      <Card loading={loadingAccount}>
        <Typography.Title level={4} style={{ marginTop: 0 }}>
          {account?.name}
        </Typography.Title>
        <Row gutter={16}>
          <Col xs={12} md={8}>
            <Statistic
              title={t('finances.accounts.postedBalance')}
              value={balance ? formatMoney(balance.posted, currency) : '—'}
            />
          </Col>
          <Col xs={12} md={8}>
            <Statistic
              title={t('finances.accounts.projectedBalance')}
              value={balance ? formatMoney(balance.projected, currency) : '—'}
            />
          </Col>
        </Row>
      </Card>

      <Card title={t('finances.accounts.statement')}>
        <Table
          rowKey="id"
          loading={loadingTx}
          dataSource={transactions}
          columns={columns}
          size="middle"
          pagination={{ pageSize: 20 }}
        />
      </Card>
    </div>
  )
}
