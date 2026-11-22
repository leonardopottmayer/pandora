import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Alert, DatePicker, Spin, Typography } from 'antd'
import dayjs from 'dayjs'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { useAllTransactions } from '@/modules/finances/hooks/useTransactions'
import { useSystemCategories, useUserCategories } from '@/modules/finances/hooks/useCategories'
import { formatMoney, formatReferenceMonth } from '@/modules/finances/lib/format'
import { financeExpenses, financeMonth } from '../../lib/financeSummary'

/** A month is a filter, never a stored snapshot of expenses or category names. */
export function FinanceExpenses({ month, editable, update }: { month: unknown; editable: boolean; update: (props: Record<string, unknown>) => void }) {
  const { t } = useTranslation()
  const { timeZone } = usePreferences()
  const period = financeMonth(month, timeZone)
  return <>
    {editable && <DatePicker picker="month" aria-label={t('notes.financeBlock.month')} format="MM/YYYY"
      value={period && month != null ? dayjs(`${period.month}-01`) : null}
      placeholder={t('notes.financeBlock.currentMonth')}
      onChange={(value) => update(value ? { mode: 'expenses', month: value.format('YYYY-MM') } : { mode: 'expenses' })} />}
    {!period ? <Typography.Text type="secondary">{t('notes.financeBlock.invalidMonth')}</Typography.Text> : <>
      <Typography.Text strong>{formatReferenceMonth(period.month)}</Typography.Text>
      <Typography.Text type="secondary" className="text-xs">{t('notes.financeBlock.expenseHint')}</Typography.Text>
      <MonthExpenses from={period.from} to={period.to} />
      <Link to="/finances/transactions">{t('notes.financeBlock.open')}</Link>
    </>}
  </>
}

function MonthExpenses({ from, to }: { from: string; to: string }) {
  const { t } = useTranslation()
  const transactions = useAllTransactions({ from, to, kind: 'expense', status: 'posted' })
  const system = useSystemCategories({ includeInactive: true })
  const user = useUserCategories(true)
  const error = transactions.error ?? system.error ?? user.error
  if (error) return <Alert type="error" title={toErrorMessage(error, t('notes.financeBlock.error'))} />
  if (transactions.isLoading || system.isLoading || user.isLoading) return <Spin size="small" />
  const groups = financeExpenses(transactions.data ?? [], [...(system.data ?? []), ...(user.data ?? [])])
  if (!groups.length) return <Typography.Text type="secondary">{t('notes.financeBlock.noExpenses')}</Typography.Text>
  return <div className="flex flex-col gap-4">{groups.map((group) => (
    <div key={group.currency}>
      <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
        <strong>{group.currency}</strong><strong className="tabular-nums">{formatMoney(group.amount, group.currency)}</strong>
      </div>
      <ul className="m-0 flex list-none flex-col gap-1 p-0">{group.categories.map((row) => (
        <li key={row.id ?? 'uncategorized'} className="flex items-baseline justify-between gap-3">
          <span className="min-w-0 break-words">{row.name ?? t(row.id ? 'notes.financeBlock.missingCategory' : 'notes.financeBlock.uncategorized')}</span>
          <span className="shrink-0 tabular-nums">{formatMoney(row.amount, group.currency)}</span>
        </li>
      ))}</ul>
    </div>
  ))}</div>
}
