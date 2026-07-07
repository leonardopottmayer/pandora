import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { PendingTransactionDto } from '../../models'
import { InboxPage } from './InboxPage'

function pending(overrides: Partial<PendingTransactionDto> = {}): PendingTransactionDto {
  return {
    id: 'p1',
    source: 'recurrence',
    recurringTransactionId: 'r1',
    accountId: 'a1',
    cardId: null,
    kind: 'expense',
    amount: 49.9,
    currency: 'BRL',
    occurredOn: '2026-06-13',
    description: 'Netflix',
    payee: null,
    notes: null,
    systemCategoryId: null,
    userCategoryId: null,
    suggestedStatementId: null,
    originalPayload: '{}',
    status: 'pending',
    decidedAt: null,
    decidedBy: null,
    rejectionReason: null,
    transactionId: null,
    importRowId: null,
    dedupStatus: null,
    duplicateOfTransactionId: null,
    createdAt: '2026-06-13T00:00:00Z',
    updatedAt: null,
    ...overrides,
  }
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

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

describe('InboxPage', () => {
  it('renders pending suggestions', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/pending-transactions`, () =>
        HttpResponse.json({ success: true, data: [pending()] }),
      ),
      ...auxHandlers(),
    )
    renderWithProviders(<InboxPage />)
    expect(await screen.findByText('Netflix')).toBeInTheDocument()
  })

  it('approves a single suggestion', async () => {
    let approved = false
    server.use(
      http.get(`${FINANCES_BASE}/pending-transactions`, () =>
        HttpResponse.json({ success: true, data: [pending()] }),
      ),
      http.post(`${FINANCES_BASE}/pending-transactions/p1/approve`, () => {
        approved = true
        return HttpResponse.json({ success: true, data: { id: 'tx1' } })
      }),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderWithProviders(<InboxPage />)

    await screen.findByText('Netflix')
    await user.click(screen.getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(approved).toBe(true))
  })

  it('rejects a suggestion through the reject modal', async () => {
    let rejected = false
    server.use(
      http.get(`${FINANCES_BASE}/pending-transactions`, () =>
        HttpResponse.json({ success: true, data: [pending()] }),
      ),
      http.post(`${FINANCES_BASE}/pending-transactions/p1/reject`, () => {
        rejected = true
        return HttpResponse.json({ success: true, data: { ...pending(), status: 'rejected' } })
      }),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderWithProviders(<InboxPage />)

    await screen.findByText('Netflix')
    await user.click(screen.getByRole('button', { name: 'Reject' }))
    const dialog = await screen.findByRole('dialog')
    // The modal's OK button confirms the rejection.
    await user.click(within(dialog).getByRole('button', { name: 'Reject' }))
    await waitFor(() => expect(rejected).toBe(true))
  })
})
