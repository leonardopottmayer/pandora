import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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
    await user.click(screen.getByRole('button', { name: 'Post' }))
    await waitFor(() => expect(postCalled).toBe(true))
  })
})
