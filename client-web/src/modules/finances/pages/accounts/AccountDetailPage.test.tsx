import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
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

describe('AccountDetailPage', () => {
  it('renders the account, its balance and statement transactions', async () => {
    server.use(...handlers())
    renderWithProviders(
      <Routes>
        <Route path="/finances/accounts/:id" element={<AccountDetailPage />} />
      </Routes>,
      { route: '/finances/accounts/a1' },
    )
    expect(await screen.findAllByText('Conta Corrente')).not.toHaveLength(0)
    expect(await screen.findByText('Mercado')).toBeInTheDocument()
  })
})
