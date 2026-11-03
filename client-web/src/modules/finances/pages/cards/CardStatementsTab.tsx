import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Badge, Button, DatePicker, InputNumber, Select, Table, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { FilterOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { STATEMENT_STATUSES, type CardStatementDto, type StatementStatus } from '../../models'
import { STATEMENT_STATUS_META } from '../../lib/enums'
import { formatDate, formatMoney, formatReferenceMonth } from '../../lib/format'
import { EnumTag } from '../../components/EnumTag'

interface StatementFilters {
  statuses?: StatementStatus[]
  dueDateFrom?: string
  dueDateTo?: string
  totalMin?: number
  totalMax?: number
}

export function CardStatementsTab({ statements, loading, currency }: { statements: CardStatementDto[]; loading: boolean; currency: string }) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const [filters, setFilters] = useState<StatementFilters>({})
  const [moreOpen, setMoreOpen] = useState(false)
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)
  const moreCount = [filters.totalMin, filters.totalMax].filter((value) => value != null).length
  const anyFilter = !!(filters.statuses?.length || filters.dueDateFrom || filters.dueDateTo || moreCount)
  const filtered = useMemo(() => statements.filter((statement) => {
    if (filters.statuses?.length && !filters.statuses.includes(statement.status)) return false
    if (filters.dueDateFrom && statement.dueDate < filters.dueDateFrom) return false
    if (filters.dueDateTo && statement.dueDate > filters.dueDateTo) return false
    if (filters.totalMin != null && statement.totalAmount < filters.totalMin) return false
    return filters.totalMax == null || statement.totalAmount <= filters.totalMax
  }), [statements, filters])

  const columns: ColumnsType<CardStatementDto> = [
    {
      title: t('finances.statements.referenceMonth'), dataIndex: 'referenceMonth',
      sorter: (a, b) => a.referenceMonth.localeCompare(b.referenceMonth),
      defaultSortOrder: 'descend',
      render: (month: string, statement) => <div className="flex min-w-0 flex-col gap-1">
        <Link className="truncate" to={`/finances/statements/${statement.id}`}>{formatReferenceMonth(month)}</Link>
        <span className="flex flex-wrap items-center gap-1 text-xs">
          <EnumTag meta={STATEMENT_STATUS_META[statement.status]} />
          <span className="tabular-nums md:hidden" style={{ color: statement.status === 'overdue' ? token.colorError : token.colorTextSecondary }}>
            {formatDate(statement.dueDate)}
          </span>
        </span>
      </div>,
    },
    {
      title: t('finances.statements.dueDate'), dataIndex: 'dueDate', width: 110, responsive: ['md'],
      sorter: (a, b) => a.dueDate.localeCompare(b.dueDate),
      render: (date: string, statement) => <span className="tabular-nums" style={statement.status === 'overdue' ? { color: token.colorError } : undefined}>{formatDate(date)}</span>,
    },
    {
      title: t('finances.statements.total'), dataIndex: 'totalAmount', width: 136, align: 'right',
      sorter: (a, b) => a.totalAmount - b.totalAmount,
      render: (amount: number, statement) => <div className="flex flex-col items-end tabular-nums">
        <span>{formatMoney(amount, currency)}</span>
        <Typography.Text type="secondary" className="text-xs lg:hidden">
          {t('finances.cards.remainingValue', { amount: formatMoney(statement.remainingAmount, currency) })}
        </Typography.Text>
      </div>,
    },
    {
      title: t('finances.statements.remaining'), dataIndex: 'remainingAmount', width: 136, align: 'right', responsive: ['lg'],
      sorter: (a, b) => a.remainingAmount - b.remainingAmount,
      render: (amount: number) => <span className="tabular-nums">{formatMoney(amount, currency)}</span>,
    },
  ]

  return <div className="flex min-h-0 flex-col gap-4 lg:h-full">
    <div className="relative z-20 flex shrink-0 flex-wrap items-center gap-2">
      <Select
        mode="multiple"
        allowClear
        className="w-full sm:w-52"
        maxTagCount="responsive"
        aria-label={t('finances.statements.status')}
        placeholder={t('finances.statements.status')}
        value={filters.statuses ?? []}
        onChange={(statuses: StatementStatus[]) => setFilters((previous) => ({ ...previous, statuses }))}
        options={STATEMENT_STATUSES.map((status) => ({ value: status, label: t(STATEMENT_STATUS_META[status].labelKey) }))}
      />
      <DatePicker.RangePicker
        className="w-full sm:w-72"
        format="DD/MM/YYYY"
        allowEmpty={[true, true]}
        placeholder={[t('finances.filters.dueFrom'), t('finances.filters.dueTo')]}
        value={[filters.dueDateFrom ? dayjs(filters.dueDateFrom) : null, filters.dueDateTo ? dayjs(filters.dueDateTo) : null]}
        onChange={(dates) => setFilters((previous) => ({
          ...previous,
          dueDateFrom: dates?.[0]?.format('YYYY-MM-DD'),
          dueDateTo: dates?.[1]?.format('YYYY-MM-DD'),
        }))}
      />
      <Badge count={moreCount} size="small">
        <Button icon={<FilterOutlined />} aria-expanded={moreOpen} onClick={() => setMoreOpen((open) => !open)}>
          {t('finances.filters.more')}
        </Button>
      </Badge>
      {anyFilter && <Button type="link" onClick={() => { setFilters({}); setMoreOpen(false) }}>{t('finances.filters.clear')}</Button>}
      {moreOpen && <div
        className="absolute inset-x-0 top-full mt-2 grid grid-cols-1 gap-3 rounded-xl p-3 sm:grid-cols-2"
        style={{ background: token.colorBgElevated, border: `1px solid ${token.colorBorderSecondary}`, boxShadow: token.boxShadowSecondary }}
      >
        <label className="flex flex-col gap-1 text-sm">
          {t('finances.cards.totalFrom')}
          <InputNumber
            value={filters.totalMin}
            prefix={currency}
            aria-label={t('finances.cards.totalFrom')}
            style={{ width: '100%' }}
            onChange={(value) => setFilters((previous) => ({ ...previous, totalMin: value ?? undefined }))}
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          {t('finances.cards.totalTo')}
          <InputNumber
            value={filters.totalMax}
            prefix={currency}
            aria-label={t('finances.cards.totalTo')}
            style={{ width: '100%' }}
            onChange={(value) => setFilters((previous) => ({ ...previous, totalMax: value ?? undefined }))}
          />
        </label>
      </div>}
    </div>
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
      <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
        <Table
          rowKey="id"
          dataSource={filtered}
          loading={loading}
          columns={columns}
          size="middle"
          tableLayout="fixed"
          pagination={false}
          sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
          onRow={(statement) => ({ onClick: () => navigate(`/finances/statements/${statement.id}`), className: 'cursor-pointer' })}
          locale={{ emptyText: t('finances.cards.noStatements') }}
        />
      </div>
      <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>
        {t('finances.cards.statementCount', { count: filtered.length })}
      </Typography.Text>
    </div>
  </div>
}
