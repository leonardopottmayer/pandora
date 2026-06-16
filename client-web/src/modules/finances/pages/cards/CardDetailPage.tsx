import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import {
  Breadcrumb, Card, Col, DatePicker, Input, InputNumber, Row, Select, Space, Statistic, Table, Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import {
  STATEMENT_STATUSES,
  type CardStatementDto,
  type InstallmentPlanDto,
  type StatementStatus,
} from '../../models'
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

interface StmtFilters {
  statuses?: StatementStatus[]
  dueDateFrom?: string
  dueDateTo?: string
  totalMin?: number
  totalMax?: number
}

interface PlanFilters {
  text?: string
  categoryIds?: string[]
  totalMin?: number
  totalMax?: number
  remainingMin?: number
  remainingMax?: number
}

export function CardDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams<{ id: string }>()

  const { data: card, isLoading } = useCard(id)
  const { data: limit } = useCardAvailableLimit(id)
  const { data: statements, isLoading: loadingStatements } = useCardStatements(id)
  const { data: plans, isLoading: loadingPlans } = useCardInstallmentPlans(id)
  const categoryNames = useCategoryNames()

  const [stmtFilters, setStmtFilters] = useState<StmtFilters>({})
  const [planFilters, setPlanFilters] = useState<PlanFilters>({})

  const currency = card?.currency ?? 'BRL'

  function updStmt<K extends keyof StmtFilters>(key: K, value: StmtFilters[K]) {
    setStmtFilters((f) => {
      const next = { ...f, [key]: value }
      if (value === undefined || (Array.isArray(value) && value.length === 0)) delete next[key]
      return next
    })
  }

  function updPlan<K extends keyof PlanFilters>(key: K, value: PlanFilters[K]) {
    setPlanFilters((f) => {
      const next = { ...f, [key]: value }
      if (value === undefined || (Array.isArray(value) && value.length === 0)) delete next[key]
      return next
    })
  }

  const filteredStatements = useMemo(() => {
    const f = stmtFilters
    return (statements ?? []).filter((s) => {
      if (f.statuses?.length && !f.statuses.includes(s.status)) return false
      if (f.dueDateFrom && s.dueDate < f.dueDateFrom) return false
      if (f.dueDateTo && s.dueDate > f.dueDateTo) return false
      if (f.totalMin != null && s.totalAmount < f.totalMin) return false
      if (f.totalMax != null && s.totalAmount > f.totalMax) return false
      return true
    })
  }, [statements, stmtFilters])

  const planCategoryOptions = useMemo(() => {
    const seen = new Set<string>()
    const opts: { value: string; label: string }[] = []
    for (const p of plans ?? []) {
      const catId = p.systemCategoryId ?? p.userCategoryId
      if (catId && !seen.has(catId)) {
        seen.add(catId)
        opts.push({ value: catId, label: categoryNames.get(catId) ?? catId })
      }
    }
    return opts
  }, [plans, categoryNames])

  const filteredPlans = useMemo(() => {
    const f = planFilters
    return (plans ?? []).filter((p) => {
      if (f.text && !p.description.toLowerCase().includes(f.text.toLowerCase())) return false
      if (f.categoryIds?.length) {
        const catId = p.systemCategoryId ?? p.userCategoryId
        if (!catId || !f.categoryIds.includes(catId)) return false
      }
      if (f.totalMin != null && p.totalAmount < f.totalMin) return false
      if (f.totalMax != null && p.totalAmount > f.totalMax) return false
      if (f.remainingMin != null && p.remainingAmount < f.remainingMin) return false
      if (f.remainingMax != null && p.remainingAmount > f.remainingMax) return false
      return true
    })
  }, [plans, planFilters])

  const statementColumns: ColumnsType<CardStatementDto> = [
    {
      title: t('finances.statements.referenceMonth'),
      dataIndex: 'referenceMonth',
      render: (value: string, s) => (
        <Link to={`/finances/statements/${s.id}`}>{formatReferenceMonth(value)}</Link>
      ),
      sorter: { compare: (a, b) => a.referenceMonth.localeCompare(b.referenceMonth), multiple: 1 },
    },
    {
      title: t('finances.statements.dueDate'),
      dataIndex: 'dueDate',
      render: (value: string) => formatDate(value),
      sorter: { compare: (a, b) => a.dueDate.localeCompare(b.dueDate), multiple: 2 },
    },
    {
      title: t('finances.statements.status'),
      dataIndex: 'status',
      render: (_, s) => <EnumTag meta={STATEMENT_STATUS_META[s.status]} />,
      sorter: { compare: (a, b) => a.status.localeCompare(b.status), multiple: 3 },
    },
    {
      title: t('finances.statements.total'),
      dataIndex: 'totalAmount',
      align: 'right',
      render: (value: number) => formatMoney(value, currency),
      sorter: { compare: (a, b) => a.totalAmount - b.totalAmount, multiple: 4 },
    },
    {
      title: t('finances.statements.remaining'),
      dataIndex: 'remainingAmount',
      align: 'right',
      render: (value: number) => formatMoney(value, currency),
      sorter: { compare: (a, b) => a.remainingAmount - b.remainingAmount, multiple: 5 },
    },
  ]

  const planColumns: ColumnsType<InstallmentPlanDto> = [
    {
      title: t('finances.transactions.description'),
      dataIndex: 'description',
      sorter: { compare: (a, b) => a.description.localeCompare(b.description), multiple: 1 },
    },
    {
      title: t('finances.transactions.category'),
      key: 'category',
      render: (_, p) => {
        const categoryId = p.systemCategoryId ?? p.userCategoryId
        return categoryId ? categoryNames.get(categoryId) ?? '—' : '—'
      },
      sorter: {
        compare: (a, b) => {
          const idA = a.systemCategoryId ?? a.userCategoryId
          const idB = b.systemCategoryId ?? b.userCategoryId
          return (idA ? (categoryNames.get(idA) ?? '') : '').localeCompare(
            idB ? (categoryNames.get(idB) ?? '') : '',
          )
        },
        multiple: 2,
      },
    },
    {
      title: t('finances.installments.count'),
      key: 'count',
      render: (_, p) => `${p.paidInstallments}/${p.installmentCount}`,
      sorter: { compare: (a, b) => a.paidInstallments - b.paidInstallments, multiple: 3 },
    },
    {
      title: t('finances.installments.total'),
      dataIndex: 'totalAmount',
      align: 'right',
      render: (value: number) => formatMoney(value, currency),
      sorter: { compare: (a, b) => a.totalAmount - b.totalAmount, multiple: 4 },
    },
    {
      title: t('finances.installments.remaining'),
      dataIndex: 'remainingAmount',
      align: 'right',
      render: (value: number) => formatMoney(value, currency),
      sorter: { compare: (a, b) => a.remainingAmount - b.remainingAmount, multiple: 5 },
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
        <Row gutter={[8, 8]} className="mb-4">
          <Col xs={24} sm={12} lg={8}>
            <Select
              mode="multiple"
              allowClear
              style={{ width: '100%' }}
              placeholder={t('finances.statements.status')}
              onChange={(v: StatementStatus[]) => updStmt('statuses', v.length ? v : undefined)}
              options={STATEMENT_STATUSES.map((s) => ({
                value: s,
                label: t(STATEMENT_STATUS_META[s].labelKey),
              }))}
            />
          </Col>
          <Col xs={24} sm={12} lg={8}>
            <DatePicker.RangePicker
              style={{ width: '100%' }}
              format="YYYY-MM-DD"
              placeholder={[
                t('finances.statements.dueDate') + ' ' + t('finances.filters.from'),
                t('finances.statements.dueDate') + ' ' + t('finances.filters.to'),
              ]}
              onChange={(_, s) => {
                updStmt('dueDateFrom', s[0] || undefined)
                updStmt('dueDateTo', s[1] || undefined)
              }}
            />
          </Col>
          <Col xs={24} sm={12} lg={8}>
            <Space.Compact style={{ width: '100%' }}>
              <InputNumber
                style={{ width: '50%' }}
                placeholder={t('finances.statements.total') + ' ' + t('finances.filters.from')}
                onChange={(v) => updStmt('totalMin', v ?? undefined)}
              />
              <InputNumber
                style={{ width: '50%' }}
                placeholder={t('finances.statements.total') + ' ' + t('finances.filters.to')}
                onChange={(v) => updStmt('totalMax', v ?? undefined)}
              />
            </Space.Compact>
          </Col>
        </Row>
        <Table
          rowKey="id"
          loading={loadingStatements}
          dataSource={filteredStatements}
          columns={statementColumns}
          size="middle"
          pagination={false}
        />
      </Card>

      <Card title={t('finances.installments.title')}>
        <Row gutter={[8, 8]} className="mb-4">
          <Col xs={24} sm={12} lg={8}>
            <Input
              allowClear
              placeholder={t('common.search')}
              onChange={(e) => updPlan('text', e.target.value || undefined)}
            />
          </Col>
          {planCategoryOptions.length > 0 && (
            <Col xs={24} sm={12} lg={8}>
              <Select
                mode="multiple"
                allowClear
                style={{ width: '100%' }}
                placeholder={t('finances.transactions.category')}
                optionFilterProp="label"
                onChange={(v: string[]) => updPlan('categoryIds', v.length ? v : undefined)}
                options={planCategoryOptions}
              />
            </Col>
          )}
          <Col xs={24} sm={12} lg={8}>
            <Space.Compact style={{ width: '100%' }}>
              <InputNumber
                style={{ width: '50%' }}
                placeholder={t('finances.installments.total') + ' ' + t('finances.filters.from')}
                onChange={(v) => updPlan('totalMin', v ?? undefined)}
              />
              <InputNumber
                style={{ width: '50%' }}
                placeholder={t('finances.installments.total') + ' ' + t('finances.filters.to')}
                onChange={(v) => updPlan('totalMax', v ?? undefined)}
              />
            </Space.Compact>
          </Col>
          <Col xs={24} sm={12} lg={8}>
            <Space.Compact style={{ width: '100%' }}>
              <InputNumber
                style={{ width: '50%' }}
                placeholder={t('finances.installments.remaining') + ' ' + t('finances.filters.from')}
                onChange={(v) => updPlan('remainingMin', v ?? undefined)}
              />
              <InputNumber
                style={{ width: '50%' }}
                placeholder={t('finances.installments.remaining') + ' ' + t('finances.filters.to')}
                onChange={(v) => updPlan('remainingMax', v ?? undefined)}
              />
            </Space.Compact>
          </Col>
        </Row>
        <Table
          rowKey="id"
          loading={loadingPlans}
          dataSource={filteredPlans}
          columns={planColumns}
          size="middle"
          pagination={false}
        />
      </Card>
    </div>
  )
}
