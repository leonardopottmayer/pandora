import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { TransactionFormModal } from './TransactionFormModal'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

// AccountSelect, CardSelect and CategorySelect inside the modal load lookups.
function lookupHandlers() {
  return [
    http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('TransactionFormModal', () => {
  it('validates required fields and does not call the API when empty', async () => {
    let posted = false
    server.use(
      ...lookupHandlers(),
      http.post(`${FINANCES_BASE}/transactions`, () => {
        posted = true
        return HttpResponse.json({ success: true, data: {} })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TransactionFormModal open onClose={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Select an account.')).toBeInTheDocument()
    expect(await screen.findByText('Enter the description.')).toBeInTheDocument()
    expect(posted).toBe(false)
  })

  it('switches to a card target, revealing the installments field', async () => {
    server.use(...lookupHandlers())
    // antd hides the native radio input (pointer-events:none); click the label.
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<TransactionFormModal open onClose={vi.fn()} />)

    // Account target: no installments field initially.
    expect(screen.queryByText('Installments')).not.toBeInTheDocument()

    // The target radio group has [Account, Card]; pick the Card option.
    const radios = screen.getAllByRole('radio')
    await user.click(radios[1])
    // Card target shows the installments field.
    expect(await screen.findByText('Installments')).toBeInTheDocument()
  })
})
