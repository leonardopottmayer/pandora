import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { PendingTransactionDto } from '../../models'
import { PendingTransactionEditModal } from './PendingTransactionEditModal'

const pending: PendingTransactionDto = {
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
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function categoryHandlers() {
  return [
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('PendingTransactionEditModal', () => {
  it('pre-fills the suggestion and saves the edits', async () => {
    let body: Record<string, unknown> | undefined
    const onClose = vi.fn()
    server.use(
      ...categoryHandlers(),
      http.put(`${FINANCES_BASE}/pending-transactions/p1`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: { ...pending, description: 'Netflix Premium' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<PendingTransactionEditModal open pending={pending} onClose={onClose} />)

    const description = screen.getByDisplayValue('Netflix') as HTMLInputElement
    await user.clear(description)
    await user.type(description, 'Netflix Premium')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({
      kind: 'expense',
      description: 'Netflix Premium',
      occurredOn: '2026-06-13',
    })
  })

  it('does nothing on submit when no suggestion is provided', () => {
    renderWithProviders(<PendingTransactionEditModal open pending={null} onClose={vi.fn()} />)
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
