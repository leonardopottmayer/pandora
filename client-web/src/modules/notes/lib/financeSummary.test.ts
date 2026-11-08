import { describe, expect, it } from 'vitest'
import { financeExpenses, financeMonth } from './financeSummary'

const expense = { kind: 'expense' as const, status: 'posted' as const, currency: 'BRL', amount: 10, systemCategoryId: null, userCategoryId: null }

describe('financeMonth', () => {
  it('resolves the current month in the account zone at a UTC month boundary', () => {
    const now = new Date('2026-12-01T01:30:00Z')
    expect(financeMonth(undefined, 'America/Sao_Paulo', now)).toEqual({ month: '2026-11', from: '2026-11-01', to: '2026-11-30' })
    expect(financeMonth(undefined, 'UTC', now)?.month).toBe('2026-12')
  })

  it('keeps fixed months as calendar dates, including leap years', () => {
    expect(financeMonth('2028-02', 'America/Sao_Paulo')).toEqual({ month: '2028-02', from: '2028-02-01', to: '2028-02-29' })
    expect(financeMonth('2026-04', 'Pacific/Auckland')?.to).toBe('2026-04-30')
  })

  it.each(['2026-13', '2026-00', '2026-2', '2026-02-01', '', 2026])('rejects an invalid month: %s', (month) => {
    expect(financeMonth(month, 'UTC')).toBeNull()
  })
})

describe('financeExpenses', () => {
  it('counts gross posted expenses, excluding refunds and all other transaction kinds/states', () => {
    expect(financeExpenses([
      expense, { ...expense, status: 'pending' }, { ...expense, status: 'void' },
      ...(['refund', 'income', 'transfer-out', 'card-statement-payment'] as const).map((kind) => ({ ...expense, kind })),
    ], [])).toEqual([{ currency: 'BRL', amount: 10, categories: [{ id: null, name: null, amount: 10 }] }])
  })

  it('keeps currencies and category identities separate, with nested historical names', () => {
    const groups = financeExpenses([
      { ...expense, systemCategoryId: 'child' }, { ...expense, userCategoryId: 'custom', amount: 5 },
      { ...expense, currency: 'USD', systemCategoryId: 'child', amount: 2 },
      { ...expense, userCategoryId: 'removed', amount: 3 }, expense,
    ], [{ id: 'root', name: 'Root', children: [{ id: 'child', name: 'Food', children: [] }] }, { id: 'custom', name: 'Food', children: [] }])
    expect(groups.map(({ currency, amount }) => ({ currency, amount }))).toEqual([{ currency: 'BRL', amount: 28 }, { currency: 'USD', amount: 2 }])
    expect(groups[0].categories).toEqual(expect.arrayContaining([
      { id: 'child', name: 'Food', amount: 10 }, { id: 'custom', name: 'Food', amount: 5 },
      { id: 'removed', name: null, amount: 3 }, { id: null, name: null, amount: 10 },
    ]))
  })
})
