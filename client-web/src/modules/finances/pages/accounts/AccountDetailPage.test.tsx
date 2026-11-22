import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { AccountDetailPage } from './AccountDetailPage'

const account = {
  id: 'a1', name: 'Conta Corrente', type: 'checking', currency: 'BRL',
  institution: null, description: null, color: null, icon: null, displayOrder: 0, archivedAt: null,
}

const transaction = {
  id: 'x1', accountId: 'a1', cardStatementId: null, cardId: null, paidStatementId: null,
  kind: 'expense', status: 'posted', amount: 50, currency: 'BRL', occurredOn: '2026-06-13',
  description: 'Mercado', payee: null, notes: null, systemCategoryId: null, userCategoryId: null,
  transferGroupId: null, fxRate: null, installmentPlanId: null, installmentNumber: null,
  origin: 'manual', postedAt: '2026-06-13T00:00:00Z', voidedAt: null, voidReason: null,
  descriptionKey: null, descriptionArgs: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function handlers() {
  return [
    http.get(`${FINANCES_BASE}/accounts/a1`, () => HttpResponse.json({ success: true, data: account })),
    http.get(`${FINANCES_BASE}/accounts/a1/balance`, () =>
      HttpResponse.json({ success: true, data: { accountId: 'a1', currency: 'BRL', posted: 150.5, projected: 200 } }),
    ),
    http.get(`${FINANCES_BASE}/accounts/a1/transactions`, () =>
      HttpResponse.json({ success: true, data: [transaction] }),
    ),
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/finances/accounts/:id" element={<AccountDetailPage />} />
    </Routes>,
    { route: '/finances/accounts/a1' },
  )
}

describe('AccountDetailPage', () => {
  it('renders the account, its balance and statement transactions', async () => {
    server.use(...handlers())
    renderPage()
    expect(await screen.findAllByText('Conta Corrente')).not.toHaveLength(0)
    expect(await screen.findByText('Mercado')).toBeInTheDocument()
    // What the projected balance still waits for.
    expect(screen.getByText(/R\$\s?49\.50 still to come in/)).toBeInTheDocument()
  })

  it('tags only pending transactions', async () => {
    // The first handler that matches answers, so the statement goes before the defaults.
    server.use(
      http.get(`${FINANCES_BASE}/accounts/a1/transactions`, () =>
        HttpResponse.json({
          success: true,
          data: [transaction, { ...transaction, id: 'x2', status: 'pending', description: 'Aluguel' }],
        }),
      ),
      ...handlers(),
    )
    renderPage()

    await screen.findByText('Aluguel')
    expect(screen.getAllByText('Pending')).toHaveLength(1)
    expect(screen.queryByText('Posted')).not.toBeInTheDocument()
  })

  it('asks for one month at a time, starting with this one', async () => {
    const asked: (string | null)[] = []
    server.use(
      http.get(`${FINANCES_BASE}/accounts/a1/transactions`, ({ request }) => {
        asked.push(new URL(request.url).searchParams.get('from'))
        return HttpResponse.json({ success: true, data: [] })
      }),
      ...handlers(),
    )
    const user = userEvent.setup()
    renderPage()

    const thisMonth = dayjs().startOf('month')
    await waitFor(() => expect(asked).toContain(thisMonth.format('YYYY-MM-DD')))
    expect(await screen.findByText('Nothing in this account this month.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Previous month' }))
    await waitFor(() => expect(asked).toContain(thisMonth.subtract(1, 'month').format('YYYY-MM-DD')))
  })
})
