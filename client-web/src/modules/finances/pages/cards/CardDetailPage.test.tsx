import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { CardDetailPage } from './CardDetailPage'

const card = {
  id: 'c1', name: 'Nubank', brand: 'Mastercard', lastFour: '1234', creditLimit: 5000,
  closingDay: 5, dueDay: 12, currency: 'BRL', defaultPaymentAccountId: null, archivedAt: null,
}

const statement = {
  id: 's1', cardId: 'c1', referenceMonth: '2026-06', closingDate: '2026-06-05', dueDate: '2026-06-12',
  status: 'open', totalAmount: 1000, paidAmount: 0, remainingAmount: 1000, closedAt: null, paidAt: null, overdueAt: null,
}

const plan = {
  id: 'p1', cardId: 'c1', origin: 'manual', description: 'Geladeira 12x', installmentCount: 12,
  totalAmount: 1200, totalIsEstimate: false, firstReferenceMonth: '2026-06', remainingAmount: 1000,
  paidInstallments: 2, systemCategoryId: null, userCategoryId: null, installments: [],
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function handlers() {
  return [
    http.get(`${FINANCES_BASE}/cards/c1`, () => HttpResponse.json({ success: true, data: card })),
    http.get(`${FINANCES_BASE}/cards/c1/available-limit`, () =>
      HttpResponse.json({ success: true, data: { cardId: 'c1', creditLimit: 5000, availableLimit: 4200 } }),
    ),
    http.get(`${FINANCES_BASE}/cards/c1/statements`, () =>
      HttpResponse.json({ success: true, data: [statement] }),
    ),
    http.get(`${FINANCES_BASE}/cards/c1/installment-plans`, () =>
      HttpResponse.json({ success: true, data: [plan] }),
    ),
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('CardDetailPage', () => {
  it('renders the card with its statements and installment plans', async () => {
    server.use(...handlers())
    renderWithProviders(
      <Routes>
        <Route path="/finances/cards/:id" element={<CardDetailPage />} />
      </Routes>,
      { route: '/finances/cards/c1' },
    )
    expect(await screen.findAllByText('Nubank')).not.toHaveLength(0)
    // installment plan row
    expect(await screen.findByText('Geladeira 12x')).toBeInTheDocument()
    // statement row count "paid/total" → "2/12" for the plan
    expect(screen.getByText('2/12')).toBeInTheDocument()
  })
})
