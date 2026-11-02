import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { RecurringTransactionDto } from '../../models'
import { RecurringTransactionFormModal } from './RecurringTransactionFormModal'

const recurring: RecurringTransactionDto = {
  id: 'r1', name: 'Netflix', accountId: 'a1', cardId: null, kind: 'expense', amount: 49.9,
  amountIsEstimate: false, description: 'Assinatura', payee: null, systemCategoryId: null,
  userCategoryId: null, frequency: 'monthly', interval: 1, dayOfMonth: 10, weekday: null,
  startDate: '2026-01-10', endDate: null, maxOccurrences: null, status: 'active', autoPost: false,
  autoGenerate: true, nextOccurrenceOn: '2026-07-10', occurrencesCount: 6,
  createdAt: '2026-01-10T00:00:00Z', updatedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function lookupHandlers() {
  return [
    http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('RecurringTransactionFormModal', () => {
  it('validates required fields when creating', async () => {
    let posted = false
    server.use(
      ...lookupHandlers(),
      http.post(`${FINANCES_BASE}/recurring-transactions`, () => {
        posted = true
        return HttpResponse.json({ success: true, data: recurring })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<RecurringTransactionFormModal open onClose={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Enter the recurrence name.')).toBeInTheDocument()
    expect(posted).toBe(false)
  })

  it('fills the description from the name until it is typed in', async () => {
    server.use(...lookupHandlers())
    const user = userEvent.setup()
    renderWithProviders(<RecurringTransactionFormModal open onClose={vi.fn()} />)

    await user.type(screen.getByRole('textbox', { name: 'Name' }), 'Aluguel')
    const description = screen.getByRole('textbox', { name: 'Description' })
    expect(description).toHaveValue('Aluguel')

    await user.clear(description)
    await user.type(description, 'Aluguel do apê')
    await user.type(screen.getByRole('textbox', { name: 'Name' }), ' 2')
    expect(description).toHaveValue('Aluguel do apê')
  })

  it('previews the next dates, and ends one way at a time', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      ...lookupHandlers(),
      http.put(`${FINANCES_BASE}/recurring-transactions/r1`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: recurring })
      }),
    )
    const onClose = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(
      <RecurringTransactionFormModal open recurring={{ ...recurring, endDate: '2026-12-31' }} onClose={onClose} />,
    )

    // From the next occurrence, monthly, up to the end date.
    expect(await screen.findByText(/Next: 7\/10\/2026, 8\/10\/2026, 9\/10\/2026/)).toBeInTheDocument()

    await user.click(screen.getByText('After a number of times'))
    await user.type(await screen.findByRole('spinbutton', { name: /Max occurrences/ }), '12')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({ endDate: null, maxOccurrences: 12 })
  })

  it('updates an existing recurrence (immutable fields pre-filled) via PUT', async () => {
    let body: Record<string, unknown> | undefined
    const onClose = vi.fn()
    server.use(
      ...lookupHandlers(),
      http.put(`${FINANCES_BASE}/recurring-transactions/r1`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: { ...recurring, name: 'Netflix Premium' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<RecurringTransactionFormModal open recurring={recurring} onClose={onClose} />)

    const name = screen.getByDisplayValue('Netflix') as HTMLInputElement
    await user.clear(name)
    await user.type(name, 'Netflix Premium')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({ name: 'Netflix Premium', description: 'Assinatura' })
  })
})
