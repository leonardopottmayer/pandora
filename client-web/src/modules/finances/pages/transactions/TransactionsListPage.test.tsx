import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TransactionDto } from '../../models'
import { TransactionsListPage } from './TransactionsListPage'

const pendingTx: TransactionDto = {
  id: 'x1',
  accountId: 'a1',
  cardStatementId: null,
  cardId: null,
  paidStatementId: null,
  kind: 'expense',
  status: 'pending',
  amount: 50,
  currency: 'BRL',
  occurredOn: '2026-06-13',
  description: 'Mercado',
  payee: null,
  notes: null,
  systemCategoryId: null,
  userCategoryId: null,
  transferGroupId: null,
  fxRate: null,
  installmentPlanId: null,
  installmentNumber: null,
  origin: 'manual',
  postedAt: null,
  voidedAt: null,
  voidReason: null,
  descriptionKey: null,
  descriptionArgs: null,
  statementReferenceMonth: null,
  statementDueDate: null,
  attachmentCount: 0,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function stubAccounts() {
  server.use(
    http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] })),
  )
}

describe('TransactionsListPage', () => {
  it('renders transactions from the API', async () => {
    stubAccounts()
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, () =>
        HttpResponse.json({ success: true, data: [pendingTx] }),
      ),
    )
    renderWithProviders(<TransactionsListPage />)
    expect(await screen.findByText('Mercado')).toBeInTheDocument()
  })

  it('totals what came in and went out, leaving out transfers and voided ones', async () => {
    stubAccounts()
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, () =>
        HttpResponse.json({
          success: true,
          data: [
            pendingTx,
            { ...pendingTx, id: 'x2', kind: 'income', amount: 100, description: 'Salário' },
            { ...pendingTx, id: 'x3', kind: 'transfer-out', amount: 30, description: 'Para poupança' },
            { ...pendingTx, id: 'x4', status: 'void', amount: 999, description: 'Errado' },
          ],
        }),
      ),
    )
    renderWithProviders(<TransactionsListPage />)

    await screen.findByText('Salário')
    expect(screen.getByText('In · BRL').nextSibling).toHaveTextContent(/R\$\s?100\.00/)
    expect(screen.getByText('Out · BRL').nextSibling).toHaveTextContent(/R\$\s?50\.00/)
    expect(screen.getByText('Result · BRL').nextSibling).toHaveTextContent(/R\$\s?50\.00/)
  })

  it('asks for this month, page after page until one comes back short', async () => {
    stubAccounts()
    const asked: { from: string | null; skip: string | null; take: string | null }[] = []
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, ({ request }) => {
        const params = new URL(request.url).searchParams
        asked.push({ from: params.get('from'), skip: params.get('skip'), take: params.get('take') })
        const page = params.get('skip') === '0' ? 200 : 3
        return HttpResponse.json({
          success: true,
          data: Array.from({ length: page }, (_, i) => ({ ...pendingTx, id: `p${params.get('skip')}-${i}` })),
        })
      }),
    )
    renderWithProviders(<TransactionsListPage />)

    expect(await screen.findByText('203 transactions')).toBeInTheDocument()
    const from = dayjs().startOf('month').format('YYYY-MM-DD')
    expect(asked).toEqual([
      { from, skip: '0', take: '200' },
      { from, skip: '200', take: '200' },
    ])
  })

  it('keeps the less used filters folded', async () => {
    stubAccounts()
    server.use(http.get(`${FINANCES_BASE}/transactions`, () => HttpResponse.json({ success: true, data: [] })))
    const user = userEvent.setup()
    renderWithProviders(<TransactionsListPage />)

    expect(screen.queryByPlaceholderText('Due from')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /More filters/ }))
    expect(screen.getByPlaceholderText('Due from')).toBeInTheDocument()
  })

  it('opens a transaction from its row', async () => {
    stubAccounts()
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, () => HttpResponse.json({ success: true, data: [pendingTx] })),
      http.get(`${FINANCES_BASE}/transactions/x1`, () => HttpResponse.json({ success: true, data: pendingTx })),
    )
    const user = userEvent.setup()
    renderWithProviders(<TransactionsListPage />)

    await user.click(await screen.findByText('Mercado'))
    expect(await screen.findByText('Transaction details')).toBeInTheDocument()
    expect(await screen.findByDisplayValue('Mercado')).toBeInTheDocument()
  })

  it('posts a pending transaction', async () => {
    let postCalled = false
    stubAccounts()
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, () =>
        HttpResponse.json({ success: true, data: [pendingTx] }),
      ),
      http.post(`${FINANCES_BASE}/transactions/x1/post`, () => {
        postCalled = true
        return HttpResponse.json({ success: true, data: { ...pendingTx, status: 'posted' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TransactionsListPage />)

    await screen.findByText('Mercado')
    await user.click(screen.getByRole('button', { name: 'Options for Mercado' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Post' }))
    await waitFor(() => expect(postCalled).toBe(true))
  })
})
