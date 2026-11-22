import { useState } from 'react'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { FINANCES_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { createTestQueryClient, renderWithProviders } from '@/test/utils'
import * as access from '@/modules/identity/hooks/useAccess'
import { financeKeys } from '@/modules/finances/hooks/queryKeys'
import { usePostTransaction } from '@/modules/finances/hooks/useTransactions'
import { VisualEditorContext, type VisualEditorContextValue } from './visualContext'
import { FinanceBlock } from './FinanceBlock'

const context: VisualEditorContextValue = { pageId: 'page', editable: true, pagesById: new Map(), foldersById: new Map(), onOpenPage: vi.fn(), pickMedia: async () => null }
const account = { id: 'bank', name: 'Current bank', currency: 'BRL', archivedAt: null }
const card = { id: 'visa', name: 'Current card', currency: 'USD', archivedAt: null }
const expense = { id: 'tx', kind: 'expense', status: 'posted', currency: 'BRL', amount: 10, systemCategoryId: 'food', userCategoryId: null }
const update = vi.fn()

function renderBlock(props: Record<string, unknown>, options: { editable?: boolean; publicView?: boolean; client?: ReturnType<typeof createTestQueryClient> } = {}) {
  return renderWithProviders(<VisualEditorContext.Provider value={{ ...context, publicView: options.publicView }}>
    <FinanceBlock props={props} editable={options.editable ?? false} update={update} />
  </VisualEditorContext.Provider>, { queryClient: options.client })
}

beforeAll(async () => { await i18n.changeLanguage('en') })
afterEach(() => { vi.restoreAllMocks(); update.mockClear() })

describe('FinanceBlock', () => {
  it('reads current balances in the response currency and refreshes without writing the note', async () => {
    let reads = 0
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json([account])),
      http.get(`${FINANCES_BASE}/accounts/bank/balance`, () => HttpResponse.json({ accountId: 'bank', currency: 'USD', posted: ++reads === 1 ? 12 : 15, projected: 20 })),
    )
    renderBlock({ accountId: 'bank' })
    await screen.findByText('$12.00')
    expect(screen.getByText('$20.00')).toBeInTheDocument()
    expect(screen.getByText('Current bank')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open in Finances' })).toHaveAttribute('href', '/finances/accounts/bank')
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Refresh Finances' }))
    await screen.findByText('$15.00')
    expect(update).not.toHaveBeenCalled()
  })

  it.each([0, null])('distinguishes a zero limit from an undefined limit: %s', async (limit) => {
    server.use(
      http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json([card])),
      http.get(`${FINANCES_BASE}/cards/visa/available-limit`, () => HttpResponse.json({ cardId: 'visa', creditLimit: 100, availableLimit: limit })),
    )
    renderBlock({ mode: 'card', cardId: 'visa' })
    await screen.findByText('$100.00')
    expect(screen.getByText(limit === null ? 'Not defined' : '$0.00')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open in Finances' })).toHaveAttribute('href', '/finances/cards/visa')
  })

  it.each(['account', 'card', 'expenses'])('keeps %s private with a populated owner cache', (mode) => {
    const client = createTestQueryClient()
    client.setQueryDefaults(financeKeys.all, { gcTime: Infinity, staleTime: Infinity })
    client.setQueryData(financeKeys.accountList(), [account])
    client.setQueryData(financeKeys.accountBalance('bank'), { currency: 'USD', posted: 999, projected: 999 })
    client.setQueryData(financeKeys.cardList(), [card])
    client.setQueryData(financeKeys.cardAvailableLimit('visa'), { creditLimit: 999, availableLimit: 999 })
    let reads = 0
    server.use(http.get(`${FINANCES_BASE}/*`, () => { reads++; return HttpResponse.json([]) }))
    renderBlock({ mode, accountId: 'bank', cardId: 'visa' }, { client, publicView: true, editable: true })
    expect(screen.getByText('Private content')).toBeInTheDocument()
    expect(screen.queryByText('$999.00')).not.toBeInTheDocument()
    expect(screen.queryByText('Current bank')).not.toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(reads).toBe(0)
  })

  it.each([
    ['account', 'feature'], ['account', 'finances.accounts.read'], ['card', 'finances.cards.read'],
    ['expenses', 'finances.transactions.read'], ['expenses', 'finances.categories.read'],
  ])('does not query %s without %s', (mode, denied) => {
    if (denied === 'feature') vi.spyOn(access, 'useFeature').mockReturnValue(false)
    else vi.spyOn(access, 'usePermission').mockImplementation((key) => key !== denied)
    let reads = 0
    server.use(http.get(`${FINANCES_BASE}/*`, () => { reads++; return HttpResponse.json([]) }))
    renderBlock({ mode, accountId: 'bank', cardId: 'visa' })
    expect(screen.getByText('Financial content is unavailable with your current access.')).toBeInTheDocument()
    expect(reads).toBe(0)
  })

  it.each(['account', 'card'])('never queries figures for unconfigured, missing or archived %s sources', async (mode) => {
    let reads = 0
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json([{ ...account, archivedAt: '2026-11-01' }])),
      http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json([{ ...card, archivedAt: '2026-11-01' }])),
      http.get(`${FINANCES_BASE}/*`, () => { reads++; return HttpResponse.json({}) }),
    )
    const { rerender } = renderBlock({ mode, accountId: 'bank', cardId: 'visa' })
    await screen.findByText(mode === 'account' ? 'This account is unavailable. Choose another while editing the note.' : 'This card is unavailable. Choose another while editing the note.')
    for (const id of [null, 'foreign']) {
      rerender(<VisualEditorContext.Provider value={context}><FinanceBlock props={{ mode, accountId: id, cardId: id }} editable={false} update={update} /></VisualEditorContext.Provider>)
      await screen.findByText(id ? /is unavailable\. Choose another/ : mode === 'account' ? 'Choose an account.' : 'Choose a card.')
    }
    expect(reads).toBe(0)
  })

  it('saves only the selected source id, and clears old configuration when changing mode', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json([account])),
      http.get(`${FINANCES_BASE}/accounts/bank/balance`, () => HttpResponse.json({ currency: 'USD', posted: 12, projected: 20 })),
      http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json([card])),
    )
    function Editable() {
      const [props, setProps] = useState<Record<string, unknown>>({})
      return <FinanceBlock props={props} editable update={(next) => { update(next); setProps(next) }} />
    }
    renderWithProviders(<VisualEditorContext.Provider value={context}><Editable /></VisualEditorContext.Provider>)
    await userEvent.click(await screen.findByRole('combobox', { name: 'Account' }))
    await userEvent.click(await screen.findByText('Current bank'))
    await screen.findByText('$12.00')
    await userEvent.click(screen.getByRole('combobox', { name: 'Financial summary' }))
    await userEvent.click(await screen.findByText('Card limits'))
    await screen.findByRole('combobox', { name: 'Card' })
    expect(update.mock.calls.map(([value]) => value)).toEqual([{ mode: 'account', accountId: 'bank' }, { mode: 'card' }])
  })

  it('loads every page of posted monthly expenses, separating currencies and resolving historical categories', async () => {
    const requests: URLSearchParams[] = []
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, ({ request }) => {
        const params = new URL(request.url).searchParams
        requests.push(params)
        return HttpResponse.json(params.get('skip') === '0' ? Array.from({ length: 200 }, (_, i) => ({ ...expense, id: String(i), amount: 1 })) : [
          { ...expense, currency: 'USD', amount: 5, systemCategoryId: null }, { ...expense, amount: 2, systemCategoryId: 'removed' },
        ])
      }),
      http.get(`${FINANCES_BASE}/categories/system`, ({ request }) => {
        expect(new URL(request.url).searchParams.get('includeInactive')).toBe('true')
        return HttpResponse.json([{ id: 'root', name: 'Root', children: [{ id: 'food', name: 'Historical food', children: [] }] }])
      }),
      http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json([])),
    )
    renderBlock({ mode: 'expenses', month: '2028-02' })
    await screen.findByText('Historical food')
    expect(screen.getByText('R$202.00')).toBeInTheDocument()
    expect(screen.getAllByText('$5.00')).toHaveLength(2)
    expect(screen.getByText('Category unavailable')).toBeInTheDocument()
    expect(screen.getByText('Uncategorized')).toBeInTheDocument()
    expect(requests.map((params) => Object.fromEntries(params))).toEqual([0, 200].map((skip) => ({ from: '2028-02-01', to: '2028-02-29', kind: 'expense', status: 'posted', skip: String(skip), take: '200' })))
    expect(update).not.toHaveBeenCalled()
  })

  it('does not query an invalid expense month', () => {
    let reads = 0
    server.use(http.get(`${FINANCES_BASE}/*`, () => { reads++; return HttpResponse.json([]) }))
    renderBlock({ mode: 'expenses', month: '2026-13' })
    expect(screen.getByText('Invalid month. Select a month while editing the note.')).toBeInTheDocument()
    expect(reads).toBe(0)
  })

  it('saves a fixed month or the current-month default without storing results', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, () => HttpResponse.json([])),
      http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json([])),
      http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json([])),
    )
    function Editable() {
      const [props, setProps] = useState<Record<string, unknown>>({ mode: 'expenses' })
      return <FinanceBlock props={props} editable update={(next) => { update(next); setProps(next) }} />
    }
    renderWithProviders(<VisualEditorContext.Provider value={context}><Editable /></VisualEditorContext.Provider>)
    await screen.findByText('No posted expenses in this month.')
    const picker = screen.getByRole('textbox', { name: 'Expense month' })
    await userEvent.type(picker, '02/2028{Enter}')
    await waitFor(() => expect(update).toHaveBeenCalledWith({ mode: 'expenses', month: '2028-02' }))
    await userEvent.click(screen.getByRole('button', { name: 'close-circle' }))
    await waitFor(() => expect(update).toHaveBeenCalledWith({ mode: 'expenses' }))
  })

  it('hides stale values on error and retries through Refresh', async () => {
    let fail = true
    const client = createTestQueryClient()
    client.setQueryData(financeKeys.accountBalance('bank'), { currency: 'USD', posted: 999, projected: 999 })
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json([account])),
      http.get(`${FINANCES_BASE}/accounts/bank/balance`, () => fail ? HttpResponse.json({ success: false, errorMessage: 'Unavailable balance' }, { status: 500 }) : HttpResponse.json({ currency: 'USD', posted: 12, projected: 20 })),
    )
    renderBlock({ accountId: 'bank' }, { client })
    await screen.findByText('Unavailable balance')
    expect(screen.queryByText('$999.00')).not.toBeInTheDocument()
    fail = false
    await userEvent.click(screen.getByRole('button', { name: 'Refresh Finances' }))
    await screen.findByText('$12.00')
    expect(update).not.toHaveBeenCalled()
  })

  it('does not show a partial total if categories fail to load', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, () => HttpResponse.json([expense])),
      http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: false, errorMessage: 'Unavailable categories' }, { status: 500 })),
      http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json([])),
    )
    renderBlock({ mode: 'expenses', month: '2026-11' })
    await screen.findByText('Unavailable categories')
    expect(screen.queryByText('R$10.00')).not.toBeInTheDocument()
    expect(screen.queryByText('Uncategorized')).not.toBeInTheDocument()
  })

  it('updates balances after a normal Finance transaction mutation', async () => {
    let posted = false
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json([account])),
      http.get(`${FINANCES_BASE}/accounts/bank/balance`, () => HttpResponse.json({ currency: 'USD', posted: posted ? 15 : 12, projected: 20 })),
      http.post(`${FINANCES_BASE}/transactions/tx/post`, () => { posted = true; return HttpResponse.json(expense) }),
    )
    function PostInFinance() {
      const post = usePostTransaction()
      return <button onClick={() => post.mutate('tx')}>Post in Finances</button>
    }
    renderWithProviders(<VisualEditorContext.Provider value={context}>
      <FinanceBlock props={{ accountId: 'bank' }} editable={false} update={update} /><PostInFinance />
    </VisualEditorContext.Provider>)
    await screen.findByText('$12.00')
    await userEvent.click(screen.getByRole('button', { name: 'Post in Finances' }))
    await waitFor(() => expect(screen.getByText('$15.00')).toBeInTheDocument())
    expect(update).not.toHaveBeenCalled()
  })
})
