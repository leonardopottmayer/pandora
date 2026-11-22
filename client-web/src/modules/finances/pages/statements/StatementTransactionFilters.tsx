import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Badge, Button, DatePicker, Input, InputNumber, Select, theme } from 'antd'
import { FilterOutlined, SearchOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { TRANSACTION_KINDS, TRANSACTION_STATUSES, type TransactionKind, type TransactionStatus } from '../../models'
import { TRANSACTION_STATUS_META, transactionKindLabelKey } from '../../lib/enums'

export interface StatementFilters {
  text?: string
  statuses?: TransactionStatus[]
  kinds?: TransactionKind[]
  categoryIds?: string[]
  occurredFrom?: string
  occurredTo?: string
  amountMin?: number
  amountMax?: number
}

interface StatementTransactionFiltersProps {
  value: StatementFilters
  onChange: (value: StatementFilters) => void
  categories: { value: string; label: string }[]
  currency: string
}

export function StatementTransactionFilters({ value, onChange, categories, currency }: StatementTransactionFiltersProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [moreOpen, setMoreOpen] = useState(false)
  const moreCount = [!!value.kinds?.length, !!value.categoryIds?.length, value.amountMin != null, value.amountMax != null].filter(Boolean).length
  const anyFilter = !!(value.text || value.occurredFrom || value.occurredTo || value.statuses?.length || moreCount)
  function update<K extends keyof StatementFilters>(key: K, next: StatementFilters[K]) {
    onChange({ ...value, [key]: next })
  }

  return <div className="relative z-20 shrink-0">
    <div className="flex flex-wrap items-center gap-2">
      <div className="w-full sm:w-64"><Input allowClear prefix={<SearchOutlined />} placeholder={t('common.search')} value={value.text ?? ''} onChange={(event) => update('text', event.target.value || undefined)} /></div>
      <DatePicker.RangePicker className="w-full sm:w-72" format="DD/MM/YYYY" allowEmpty={[true, true]}
        placeholder={[t('finances.filters.periodFrom'), t('finances.filters.periodTo')]}
        value={[value.occurredFrom ? dayjs(value.occurredFrom) : null, value.occurredTo ? dayjs(value.occurredTo) : null]}
        onChange={(dates) => onChange({ ...value, occurredFrom: dates?.[0]?.format('YYYY-MM-DD'), occurredTo: dates?.[1]?.format('YYYY-MM-DD') })} />
      <Select mode="multiple" allowClear maxTagCount="responsive" className="w-full sm:w-52" aria-label={t('finances.transactions.status')} placeholder={t('finances.transactions.status')}
        value={value.statuses ?? []} onChange={(statuses: TransactionStatus[]) => update('statuses', statuses)}
        options={TRANSACTION_STATUSES.map((status) => ({ value: status, label: t(TRANSACTION_STATUS_META[status].labelKey) }))} />
      <Badge count={moreCount} size="small"><Button icon={<FilterOutlined aria-hidden />} aria-expanded={moreOpen} onClick={() => setMoreOpen((open) => !open)}>{t('finances.filters.more')}</Button></Badge>
      {anyFilter && <Button type="link" onClick={() => { onChange({}); setMoreOpen(false) }}>{t('finances.filters.clear')}</Button>}
    </div>
    {moreOpen && <div className="absolute inset-x-0 top-full mt-2 grid grid-cols-1 gap-3 rounded-xl p-3 sm:grid-cols-2"
      style={{ background: token.colorBgElevated, border: `1px solid ${token.colorBorderSecondary}`, boxShadow: token.boxShadowSecondary }}>
      <label className="flex min-w-0 flex-col gap-1 text-sm">
        {t('finances.transactions.kind')}
        <Select mode="multiple" allowClear maxTagCount="responsive" aria-label={t('finances.transactions.kind')} value={value.kinds ?? []}
          onChange={(kinds: TransactionKind[]) => update('kinds', kinds)} options={TRANSACTION_KINDS.map((kind) => ({ value: kind, label: t(transactionKindLabelKey(kind)) }))} />
      </label>
      {categories.length > 0 && <label className="flex min-w-0 flex-col gap-1 text-sm">
        {t('finances.transactions.category')}
        <Select mode="multiple" allowClear maxTagCount="responsive" showSearch={{ optionFilterProp: 'label' }} aria-label={t('finances.transactions.category')} value={value.categoryIds ?? []}
          onChange={(ids: string[]) => update('categoryIds', ids)} options={categories} />
      </label>}
      <label className="flex min-w-0 flex-col gap-1 text-sm">
        {t('finances.filters.amountFrom')}
        <InputNumber aria-label={t('finances.filters.amountFrom')} prefix={currency} style={{ width: '100%' }} value={value.amountMin} onChange={(amount) => update('amountMin', amount ?? undefined)} />
      </label>
      <label className="flex min-w-0 flex-col gap-1 text-sm">
        {t('finances.filters.amountTo')}
        <InputNumber aria-label={t('finances.filters.amountTo')} prefix={currency} style={{ width: '100%' }} value={value.amountMax} onChange={(amount) => update('amountMax', amount ?? undefined)} />
      </label>
    </div>}
  </div>
}
