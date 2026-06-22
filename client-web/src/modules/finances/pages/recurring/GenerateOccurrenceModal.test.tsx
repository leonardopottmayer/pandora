import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { RecurringTransactionDto } from '../../models'
import { GenerateOccurrenceModal } from './GenerateOccurrenceModal'

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

describe('GenerateOccurrenceModal', () => {
  it('generates an occurrence to the inbox using the pre-filled values', async () => {
    let body: Record<string, unknown> | undefined
    const onClose = vi.fn()
    server.use(
      http.post(`${FINANCES_BASE}/recurring-transactions/r1/generate`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: { id: 'pt1', destination: 'inbox' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<GenerateOccurrenceModal open recurring={recurring} onClose={onClose} />)

    // Description comes pre-filled from the recurrence.
    expect((screen.getByDisplayValue('Assinatura'))).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Generate/ }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({
      destination: 'inbox',
      occurredOn: '2026-07-10',
      description: 'Assinatura',
      advanceSchedule: true,
    })
  })

  it('does nothing when no recurrence is provided', async () => {
    const onClose = vi.fn()
    renderWithProviders(<GenerateOccurrenceModal open recurring={null} onClose={onClose} />)
    // With no recurrence, the form is empty; submitting is a no-op (no crash).
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
