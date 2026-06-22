import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TransactionDto } from '../models'
import { TransactionDetailModal } from './TransactionDetailModal'

const tx: TransactionDto = {
  id: 'x1',
  accountId: 'a1',
  cardStatementId: null,
  cardId: null,
  paidStatementId: null,
  kind: 'expense',
  status: 'posted',
  amount: 50,
  currency: 'BRL',
  occurredOn: '2026-06-13',
  description: 'Mercado',
  payee: 'Padaria',
  notes: null,
  systemCategoryId: null,
  userCategoryId: null,
  transferGroupId: null,
  fxRate: null,
  installmentPlanId: null,
  installmentNumber: null,
  origin: 'manual',
  postedAt: '2026-06-13T00:00:00Z',
  voidedAt: null,
  voidReason: null,
  descriptionKey: null,
  descriptionArgs: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function nameHandlers() {
  return [
    http.get(`${FINANCES_BASE}/accounts`, () =>
      HttpResponse.json({ success: true, data: [{ id: 'a1', name: 'Conta', currency: 'BRL', type: 'checking', institution: null, description: null, color: null, icon: null, displayOrder: 0, archivedAt: null }] }),
    ),
    http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('TransactionDetailModal', () => {
  it('loads and displays the transaction details', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/transactions/x1`, () =>
        HttpResponse.json({ success: true, data: tx }),
      ),
      ...nameHandlers(),
    )
    renderWithProviders(<TransactionDetailModal open transactionId="x1" onClose={() => {}} />)

    expect(await screen.findByText('Mercado')).toBeInTheDocument()
    // origin resolves to the account name
    expect(await screen.findByText('Conta')).toBeInTheDocument()
    // payee row is shown only when present
    expect(screen.getByText('Padaria')).toBeInTheDocument()
  })

  it('does not fetch while closed', () => {
    let fetched = false
    server.use(
      http.get(`${FINANCES_BASE}/transactions/x1`, () => {
        fetched = true
        return HttpResponse.json({ success: true, data: tx })
      }),
      ...nameHandlers(),
    )
    renderWithProviders(<TransactionDetailModal open={false} transactionId="x1" onClose={() => {}} />)
    expect(fetched).toBe(false)
  })
})
