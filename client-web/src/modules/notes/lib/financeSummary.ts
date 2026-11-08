import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import timezone from 'dayjs/plugin/timezone'
import type { TransactionDto } from '@/modules/finances/models'

dayjs.extend(utc)
dayjs.extend(timezone)

/** A calendar month in the account zone, expressed as DateOnly boundaries for Finances. */
export function financeMonth(month: unknown, timeZone: string, now = new Date()): { month: string; from: string; to: string } | null {
  const value = month == null ? dayjs(now).tz(timeZone).format('YYYY-MM') : month
  if (typeof value !== 'string' || !/^\d{4}-(0[1-9]|1[0-2])$/.test(value)) return null
  const first = dayjs(`${value}-01`)
  if (!first.isValid() || first.format('YYYY-MM') !== value) return null
  return { month: value, from: first.format('YYYY-MM-DD'), to: first.endOf('month').format('YYYY-MM-DD') }
}

interface CategoryNode { id: string; name: string; children: CategoryNode[] }
interface CategoryExpense { id: string | null; name: string | null; amount: number }
export interface CurrencyExpenses { currency: string; amount: number; categories: CategoryExpense[] }
type ExpenseInput = Pick<TransactionDto, 'kind' | 'status' | 'currency' | 'amount' | 'systemCategoryId' | 'userCategoryId'>

/** Gross posted expenses only; currencies and category identities never get combined. */
export function financeExpenses(transactions: readonly ExpenseInput[], categories: readonly CategoryNode[]): CurrencyExpenses[] {
  const names = new Map<string, string>()
  function visit(nodes: readonly CategoryNode[]) {
    for (const node of nodes) {
      names.set(node.id, node.name)
      visit(node.children)
    }
  }
  visit(categories)
  const currencies = new Map<string, Map<string | null, CategoryExpense>>()
  for (const tx of transactions) {
    if (tx.kind !== 'expense' || tx.status !== 'posted') continue
    const byCategory = currencies.get(tx.currency) ?? new Map<string | null, CategoryExpense>()
    const id = tx.systemCategoryId ?? tx.userCategoryId
    const row = byCategory.get(id) ?? { id, name: id ? names.get(id) ?? null : null, amount: 0 }
    row.amount += tx.amount
    byCategory.set(id, row)
    currencies.set(tx.currency, byCategory)
  }
  return [...currencies].sort(([a], [b]) => a.localeCompare(b)).map(([currency, rows]) => ({
    currency,
    amount: [...rows.values()].reduce((sum, row) => sum + row.amount, 0),
    categories: [...rows.values()].sort((a, b) => b.amount - a.amount),
  }))
}
