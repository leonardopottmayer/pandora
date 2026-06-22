import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CardDto } from '../../models'
import { CardFormModal } from './CardFormModal'

const card: CardDto = {
  id: 'k1',
  name: 'Nubank',
  brand: 'Mastercard',
  lastFour: '1234',
  creditLimit: 5000,
  closingDay: 5,
  dueDay: 12,
  currency: 'BRL',
  defaultPaymentAccountId: null,
  archivedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

// AccountSelect inside the modal loads the account list.
function accountsHandler() {
  return http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] }))
}

describe('CardFormModal', () => {
  it('creates a card with the default currency and billing days', async () => {
    let body: Record<string, unknown> | undefined
    const onClose = vi.fn()
    server.use(
      accountsHandler(),
      http.post(`${FINANCES_BASE}/cards`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: card })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<CardFormModal open onClose={onClose} />)

    await user.type(screen.getAllByRole('textbox')[0], 'Nubank')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({ name: 'Nubank', currency: 'BRL', closingDay: 1, dueDay: 10 })
  })

  it('validates that the name is required', async () => {
    server.use(accountsHandler())
    const user = userEvent.setup()
    renderWithProviders(<CardFormModal open onClose={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Enter the card name.')).toBeInTheDocument()
  })

  it('updates an existing card via PUT', async () => {
    let putCalled = false
    const onClose = vi.fn()
    server.use(
      accountsHandler(),
      http.put(`${FINANCES_BASE}/cards/k1`, () => {
        putCalled = true
        return HttpResponse.json({ success: true, data: { ...card, name: 'Itau' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<CardFormModal open card={card} onClose={onClose} />)

    const name = screen.getAllByRole('textbox')[0] as HTMLInputElement
    expect(name.value).toBe('Nubank')
    await user.clear(name)
    await user.type(name, 'Itau')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(putCalled).toBe(true))
    expect(onClose).toHaveBeenCalled()
  })
})
