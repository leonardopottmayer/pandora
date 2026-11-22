import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { TransferFormModal } from './TransferFormModal'

const accounts = [
  { id: 'a1', name: 'Conta', currency: 'BRL', type: 'checking', institution: null, description: null, color: null, icon: null, displayOrder: 0, archivedAt: null },
  { id: 'a2', name: 'Poupança', currency: 'BRL', type: 'savings', institution: null, description: null, color: null, icon: null, displayOrder: 0, archivedAt: null },
]

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function accountsHandler() {
  return http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: accounts }))
}

describe('TransferFormModal', () => {
  it('keeps the optional received amount when collapsed and sends the original request shape', async () => {
    let body: Record<string, unknown> | undefined
    const onClose = vi.fn()
    server.use(
      accountsHandler(),
      http.post(`${FINANCES_BASE}/transactions/transfer`, async ({ request }) => {
        body = await request.json() as Record<string, unknown>
        return HttpResponse.json({ success: true, data: [] })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TransferFormModal open onClose={onClose} />)
    await user.click(screen.getAllByRole('combobox')[0])
    await user.click(await screen.findByText('Conta (BRL)'))
    await user.click(screen.getAllByRole('combobox')[1])
    await user.click((await screen.findAllByText(`${accounts[1].name} (BRL)`, { selector: '.ant-select-item-option-content' })).at(-1)!)
    await user.type(screen.getByRole('spinbutton', { name: 'Amount sent' }), '100')
    await user.type(screen.getByRole('textbox', { name: 'Date' }), '14/06/2026')
    await user.tab()
    await user.type(screen.getByRole('textbox', { name: 'Description' }), 'Between accounts')
    const optional = screen.getByText('Amount received', { selector: 'summary' })
    await user.click(optional)
    await user.type(screen.getByRole('spinbutton', { name: 'Amount received' }), '100')
    expect(screen.getByText('Fill in only for transfers between different currencies.')).toBeInTheDocument()
    await user.click(optional)
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toEqual({ fromAccountId: 'a1', toAccountId: 'a2', amountOut: 100, amountIn: 100, fxRate: null, occurredOn: '2026-06-14', description: 'Between accounts', notes: null })
  })

  it('shows required-field validation and does not call the API when empty', async () => {
    let posted = false
    server.use(
      accountsHandler(),
      http.post(`${FINANCES_BASE}/transactions/transfer`, () => {
        posted = true
        return HttpResponse.json({ success: true, data: [] })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TransferFormModal open onClose={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Enter the description.')).toBeInTheDocument()
    expect(screen.getAllByText('Select an account.').length).toBeGreaterThan(0)
    expect(posted).toBe(false)
  })

  it('selects a source account from the dropdown', async () => {
    server.use(accountsHandler())
    const user = userEvent.setup()
    renderWithProviders(<TransferFormModal open onClose={vi.fn()} />)

    const combos = screen.getAllByRole('combobox')
    await user.click(combos[0])
    await user.click(await screen.findByText('Conta (BRL)'))
    // The chosen account is reflected in the source select.
    expect(screen.getAllByTitle('Conta (BRL)').length).toBeGreaterThan(0)
  })
})
