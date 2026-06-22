import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CardStatementDetailDto } from '../../models'
import { StatementDetailPage } from './StatementDetailPage'

function detail(status: CardStatementDetailDto['statement']['status']): CardStatementDetailDto {
  return {
    statement: {
      id: 's1',
      cardId: 'c1',
      referenceMonth: '2026-06',
      closingDate: '2026-06-05',
      dueDate: '2026-06-12',
      status,
      totalAmount: 1000,
      paidAmount: 0,
      remainingAmount: 1000,
      closedAt: null,
      paidAt: null,
      overdueAt: null,
    },
    transactions: [
      {
        id: 'x1', accountId: null, cardStatementId: 's1', cardId: 'c1', paidStatementId: null,
        kind: 'expense', status: 'posted', amount: 200, currency: 'BRL', occurredOn: '2026-06-03',
        description: 'Compra A', payee: null, notes: null, systemCategoryId: null, userCategoryId: null,
        transferGroupId: null, fxRate: null, installmentPlanId: null, installmentNumber: null,
        origin: 'manual', postedAt: '2026-06-03T00:00:00Z', voidedAt: null, voidReason: null,
        descriptionKey: null, descriptionArgs: null,
      },
    ],
  }
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/finances/statements/:id" element={<StatementDetailPage />} />
    </Routes>,
    { route: '/finances/statements/s1' },
  )
}

function auxHandlers() {
  return [
    http.get(`${FINANCES_BASE}/cards/c1`, () =>
      HttpResponse.json({ success: true, data: { id: 'c1', name: 'Nubank', brand: null, lastFour: null, creditLimit: null, closingDay: 5, dueDay: 12, currency: 'BRL', defaultPaymentAccountId: null, archivedAt: null } }),
    ),
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('StatementDetailPage', () => {
  it('renders the statement and its transactions', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('open') })),
      ...auxHandlers(),
    )
    renderPage()
    expect(await screen.findByText('Compra A')).toBeInTheDocument()
    // The card name resolves in the breadcrumb.
    expect(await screen.findByText('Nubank')).toBeInTheDocument()
  })

  it('closes an open statement', async () => {
    let closed = false
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('open') })),
      http.post(`${FINANCES_BASE}/statements/s1/close`, () => {
        closed = true
        return HttpResponse.json({ success: true, data: detail('closed').statement })
      }),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra A')
    await user.click(screen.getByRole('button', { name: 'Close statement' }))
    await waitFor(() => expect(closed).toBe(true))
  })

  it('filters transactions by the text search', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('open') })),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra A')
    await user.type(screen.getByPlaceholderText('Search'), 'inexistente')
    await waitFor(() => expect(screen.queryByText('Compra A')).not.toBeInTheDocument())
  })
})
