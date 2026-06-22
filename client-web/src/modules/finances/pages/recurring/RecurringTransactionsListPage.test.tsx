import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { RecurringTransactionDto } from '../../models'
import { RecurringTransactionsListPage } from './RecurringTransactionsListPage'

const recurring: RecurringTransactionDto = {
  id: 'r1',
  name: 'Netflix',
  accountId: 'a1',
  cardId: null,
  kind: 'expense',
  amount: 49.9,
  amountIsEstimate: false,
  description: 'Assinatura',
  payee: null,
  systemCategoryId: null,
  userCategoryId: null,
  frequency: 'monthly',
  interval: 1,
  dayOfMonth: 10,
  weekday: null,
  startDate: '2026-01-10',
  endDate: null,
  maxOccurrences: null,
  status: 'active',
  autoPost: false,
  autoGenerate: false,
  nextOccurrenceOn: '2026-07-10',
  occurrencesCount: 6,
  createdAt: '2026-01-10T00:00:00Z',
  updatedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

/** Handlers for the auxiliary lookups the page loads (accounts/cards/categories). */
function auxHandlers() {
  return [
    http.get(`${FINANCES_BASE}/accounts`, () =>
      HttpResponse.json({ success: true, data: [{ id: 'a1', name: 'Conta', currency: 'BRL', type: 'checking', institution: null, description: null, color: null, icon: null, displayOrder: 0, archivedAt: null }] }),
    ),
    http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('RecurringTransactionsListPage', () => {
  it('renders recurring transactions from the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/recurring-transactions`, () =>
        HttpResponse.json({ success: true, data: [recurring] }),
      ),
      ...auxHandlers(),
    )
    renderWithProviders(<RecurringTransactionsListPage />)
    expect(await screen.findByText('Netflix')).toBeInTheDocument()
  })

  it('pauses an active recurring transaction', async () => {
    let pauseCalled = false
    server.use(
      http.get(`${FINANCES_BASE}/recurring-transactions`, () =>
        HttpResponse.json({ success: true, data: [recurring] }),
      ),
      http.post(`${FINANCES_BASE}/recurring-transactions/r1/pause`, () => {
        pauseCalled = true
        return HttpResponse.json({ success: true, data: { ...recurring, status: 'paused' } })
      }),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderWithProviders(<RecurringTransactionsListPage />)

    await screen.findByText('Netflix')
    await user.click(screen.getByRole('button', { name: 'Pause' }))
    await waitFor(() => expect(pauseCalled).toBe(true))
  })

  it('opens the generate-occurrence modal', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/recurring-transactions`, () =>
        HttpResponse.json({ success: true, data: [recurring] }),
      ),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderWithProviders(<RecurringTransactionsListPage />)

    await screen.findByText('Netflix')
    await user.click(screen.getByRole('button', { name: /Generate/ }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })
})
