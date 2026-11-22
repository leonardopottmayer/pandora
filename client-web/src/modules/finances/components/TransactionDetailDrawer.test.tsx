import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TransactionDto } from '../models'
import { TransactionDetailDrawer } from './TransactionDetailDrawer'

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
  statementReferenceMonth: null,
  statementDueDate: null,
  attachmentCount: 0,
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

describe('TransactionDetailDrawer', () => {
  it('loads the transaction into its form, with where it happened', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/transactions/x1`, () =>
        HttpResponse.json({ success: true, data: tx }),
      ),
      ...nameHandlers(),
    )
    renderWithProviders(<TransactionDetailDrawer transactionId="x1" onClose={() => {}} />)

    expect(await screen.findByDisplayValue('Mercado')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Padaria')).toBeInTheDocument()
    // The place resolves to the account name.
    expect(await screen.findByText(/Conta/)).toBeInTheDocument()
  })

  it('saves a corrected description and payee', async () => {
    const bodies: unknown[] = []
    server.use(
      http.get(`${FINANCES_BASE}/transactions/x1`, () => HttpResponse.json({ success: true, data: tx })),
      http.put(`${FINANCES_BASE}/transactions/x1`, async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json({ success: true, data: tx })
      }),
      ...nameHandlers(),
    )
    const user = userEvent.setup()
    renderWithProviders(<TransactionDetailDrawer transactionId="x1" onClose={() => {}} />)

    const description = await screen.findByDisplayValue('Mercado')
    await user.clear(description)
    await user.type(description, 'Supermercado')
    await user.clear(screen.getByDisplayValue('Padaria'))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(bodies).toEqual([
        { description: 'Supermercado', payee: null, notes: null, systemCategoryId: null, userCategoryId: null },
      ]),
    )
  })

  it('offers unvoiding a voided transaction instead of voiding it', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/transactions/x1`, () =>
        HttpResponse.json({ success: true, data: { ...tx, status: 'void' } }),
      ),
      ...nameHandlers(),
    )
    renderWithProviders(<TransactionDetailDrawer transactionId="x1" onClose={() => {}} />)

    expect(await screen.findByRole('button', { name: 'Unvoid' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Void' })).not.toBeInTheDocument()
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
    renderWithProviders(<TransactionDetailDrawer transactionId={null} onClose={() => {}} />)
    expect(fetched).toBe(false)
  })
})
