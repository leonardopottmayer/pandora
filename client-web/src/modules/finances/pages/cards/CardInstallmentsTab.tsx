import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Badge, Button, Input, InputNumber, Select, Table, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { FilterOutlined } from '@ant-design/icons'
import type { InstallmentPlanDto } from '../../models'
import { formatMoney } from '../../lib/format'
import { useCategoryNames } from '../../hooks/useCategories'

interface PlanFilters {
  text?: string
  categoryIds?: string[]
  totalMin?: number
  totalMax?: number
  remainingMin?: number
  remainingMax?: number
}

const AMOUNT_FIELDS = [
  ['totalMin', 'finances.cards.totalFrom'], ['totalMax', 'finances.cards.totalTo'],
  ['remainingMin', 'finances.cards.remainingFrom'], ['remainingMax', 'finances.cards.remainingTo'],
] as const

export function CardInstallmentsTab({ plans, loading, currency }: { plans: InstallmentPlanDto[]; loading: boolean; currency: string }) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const categoryNames = useCategoryNames()
  const [filters, setFilters] = useState<PlanFilters>({})
  const [moreOpen, setMoreOpen] = useState(false)
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)
  const moreCount = AMOUNT_FIELDS.filter(([key]) => filters[key] != null).length
  const anyFilter = !!(filters.text || filters.categoryIds?.length || moreCount)
  const categoryOptions = useMemo(() => [...new Set(plans.map((plan) => plan.systemCategoryId ?? plan.userCategoryId).filter((id): id is string => !!id))]
    .map((id) => ({ value: id, label: categoryNames.get(id) ?? id })), [plans, categoryNames])
  const filtered = useMemo(() => plans.filter((plan) => {
    if (filters.text && !plan.description.toLocaleLowerCase().includes(filters.text.toLocaleLowerCase())) return false
    const categoryId = plan.systemCategoryId ?? plan.userCategoryId
    if (filters.categoryIds?.length && (!categoryId || !filters.categoryIds.includes(categoryId))) return false
    if (filters.totalMin != null && plan.totalAmount < filters.totalMin) return false
    if (filters.totalMax != null && plan.totalAmount > filters.totalMax) return false
    if (filters.remainingMin != null && plan.remainingAmount < filters.remainingMin) return false
    return filters.remainingMax == null || plan.remainingAmount <= filters.remainingMax
  }), [plans, filters])

  const columns: ColumnsType<InstallmentPlanDto> = [
    {
      title: t('finances.transactions.description'), dataIndex: 'description',
      sorter: (a, b) => a.description.localeCompare(b.description),
      render: (description: string, plan) => {
        const categoryId = plan.systemCategoryId ?? plan.userCategoryId
        return <div className="flex min-w-0 flex-col gap-1">
          <span className="truncate" title={description}>{description}</span>
          <Typography.Text type="secondary" className="truncate text-xs">
            {categoryId ? categoryNames.get(categoryId) ?? '—' : '—'}
            <span className="tabular-nums md:hidden">{` · ${plan.paidInstallments}/${plan.installmentCount}`}</span>
          </Typography.Text>
        </div>
      },
    },
    {
      title: t('finances.installments.count'), key: 'count', width: 100, responsive: ['md'],
      sorter: (a, b) => a.paidInstallments - b.paidInstallments,
      render: (_, plan) => <span className="tabular-nums">{plan.paidInstallments}/{plan.installmentCount}</span>,
    },
    {
      title: t('finances.installments.total'), dataIndex: 'totalAmount', width: 136, align: 'right',
      sorter: (a, b) => a.totalAmount - b.totalAmount,
      render: (amount: number, plan) => <div className="flex flex-col items-end tabular-nums">
        <span>{formatMoney(amount, currency)}</span>
        <Typography.Text type="secondary" className="text-xs lg:hidden">
          {t('finances.cards.remainingValue', { amount: formatMoney(plan.remainingAmount, currency) })}
        </Typography.Text>
      </div>,
    },
    {
      title: t('finances.installments.remaining'), dataIndex: 'remainingAmount', width: 136, align: 'right', responsive: ['lg'],
      sorter: (a, b) => a.remainingAmount - b.remainingAmount,
      render: (amount: number) => <span className="tabular-nums">{formatMoney(amount, currency)}</span>,
    },
  ]

  return <div className="flex min-h-0 flex-col gap-4 lg:h-full">
    <div className="relative z-20 flex shrink-0 flex-wrap items-center gap-2">
      <div className="w-full sm:w-56">
        <Input
          allowClear
          aria-label={t('finances.cards.searchPlans')}
          placeholder={t('finances.cards.searchPlans')}
          value={filters.text ?? ''}
          onChange={(event) => setFilters((previous) => ({ ...previous, text: event.target.value }))}
        />
      </div>
      {categoryOptions.length > 0 && <Select
        mode="multiple"
        allowClear
        className="w-full sm:w-52"
        maxTagCount="responsive"
        showSearch={{ optionFilterProp: 'label' }}
        aria-label={t('finances.transactions.category')}
        placeholder={t('finances.transactions.category')}
        value={filters.categoryIds ?? []}
        options={categoryOptions}
        onChange={(categoryIds: string[]) => setFilters((previous) => ({ ...previous, categoryIds }))}
      />}
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
        {AMOUNT_FIELDS.map(([key, label]) => <label key={key} className="flex flex-col gap-1 text-sm">
          {t(label)}
          <InputNumber
            value={filters[key]}
            prefix={currency}
            aria-label={t(label)}
            style={{ width: '100%' }}
            onChange={(value) => setFilters((previous) => ({ ...previous, [key]: value ?? undefined }))}
          />
        </label>)}
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
          locale={{ emptyText: t('finances.cards.noPlans') }}
        />
      </div>
      <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>
        {t('finances.cards.planCount', { count: filtered.length })}
      </Typography.Text>
    </div>
  </div>
}
