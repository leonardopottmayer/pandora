import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { CardDetailPage } from './CardDetailPage'

const card = {
  id: 'c1', name: 'Nubank', brand: 'Mastercard', lastFour: '1234', creditLimit: 5000,
  closingDay: 5, dueDay: 12, currency: 'BRL', accountId: 'a1', archivedAt: null,
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
    http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [{ id: 'a1', name: 'Account', currency: 'BRL' }] })),
  ]
}

describe('CardDetailPage', () => {
  it('renders the card with its statements and installment plans', async () => {
    const user = userEvent.setup()
    server.use(...handlers())
    renderWithProviders(
      <Routes>
        <Route path="/finances/cards/:id" element={<CardDetailPage />} />
      </Routes>,
      { route: '/finances/cards/c1' },
    )
    expect(await screen.findAllByText('Nubank')).not.toHaveLength(0)
    expect(screen.getByRole('tab', { name: 'Statements' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.queryByText('Geladeira 12x')).not.toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Installment plans' }))
    expect(await screen.findByText('Geladeira 12x')).toBeInTheDocument()
    expect(screen.getByText(/2\/12/)).toBeInTheDocument()
  })

  it('preserves each tab\'s filters and clears only the active tab, including zero bounds', async () => {
    server.use(...handlers())
    const user = userEvent.setup()
    renderWithProviders(<Routes><Route path="/finances/cards/:id" element={<CardDetailPage />} /></Routes>, { route: '/finances/cards/c1' })
    await screen.findByRole('tab', { name: 'Statements' })
    await user.click(screen.getByRole('button', { name: /More filters/ }))
    await user.type(screen.getByRole('spinbutton', { name: 'Total to' }), '0')
    expect(await screen.findByText('No statements match these filters.')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Installment plans' }))
    await user.type(await screen.findByRole('textbox', { name: 'Search installment plans' }), 'missing')
    expect(await screen.findByText('No installment plans match these filters.')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Statements' }))
    let panel = screen.getByRole('tabpanel', { name: 'Statements' })
    expect(within(panel).getByRole('spinbutton', { name: 'Total to' })).toHaveValue('0')
    await user.click(within(panel).getByRole('button', { name: 'Clear filters' }))
    expect(within(panel).getByText('1 statement')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Installment plans' }))
    panel = screen.getByRole('tabpanel', { name: 'Installment plans' })
    expect(within(panel).getByRole('textbox', { name: 'Search installment plans' })).toHaveValue('missing')
    await user.click(within(panel).getByRole('button', { name: 'Clear filters' }))
    expect(within(panel).getByText('Geladeira 12x')).toBeInTheDocument()
  })

  it('opens the existing edit form from the card heading', async () => {
    server.use(...handlers())
    const user = userEvent.setup()
    renderWithProviders(<Routes><Route path="/finances/cards/:id" element={<CardDetailPage />} /></Routes>, { route: '/finances/cards/c1' })
    await user.click(await screen.findByRole('button', { name: /Edit card/ }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Edit card')).toBeInTheDocument()
    expect(within(dialog).getByRole('textbox', { name: 'Name' })).toHaveValue('Nubank')
  })

  it('opens the selected statement from its row', async () => {
    server.use(...handlers())
    const user = userEvent.setup()
    renderWithProviders(<Routes>
      <Route path="/finances/cards/:id" element={<CardDetailPage />} />
      <Route path="/finances/statements/:id" element={<div>Statement detail</div>} />
    </Routes>, { route: '/finances/cards/c1' })
    const reference = await screen.findByRole('link', { name: /Jun 2026/ })
    await user.click(reference.closest('tr')!)
    expect(await screen.findByText('Statement detail')).toBeInTheDocument()
  })

  it('filters statement due dates displayed as day/month/year', async () => {
    server.use(...handlers())
    const user = userEvent.setup()
    renderWithProviders(<Routes><Route path="/finances/cards/:id" element={<CardDetailPage />} /></Routes>, { route: '/finances/cards/c1' })
    await screen.findByRole('tab', { name: 'Statements' })
    await user.type(screen.getByPlaceholderText('Due from'), '13/06/2026{Enter}')
    await user.type(screen.getByPlaceholderText('Due to'), '30/06/2026{Enter}')
    await user.tab()
    expect(await screen.findByText('No statements match these filters.')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('Due from')).toHaveValue('13/06/2026')
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    expect(screen.getByPlaceholderText('Due from')).toHaveValue('')
    expect(screen.getByText('1 statement')).toBeInTheDocument()
  })

  it('allows a zero remaining-amount bound to find fully paid installment plans', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/cards/c1/installment-plans`, () => HttpResponse.json({ success: true, data: [plan, { ...plan, id: 'paid', description: 'Paid plan', remainingAmount: 0, paidInstallments: 12 }] })),
      ...handlers(),
    )
    const user = userEvent.setup()
    renderWithProviders(<Routes><Route path="/finances/cards/:id" element={<CardDetailPage />} /></Routes>, { route: '/finances/cards/c1' })
    await user.click(await screen.findByRole('tab', { name: 'Installment plans' }))
    await user.click(screen.getByRole('button', { name: /More filters/ }))
    await user.type(screen.getByRole('spinbutton', { name: 'Remaining to' }), '0')
    expect(screen.getByText('Paid plan')).toBeInTheDocument()
    expect(screen.queryByText('Geladeira 12x')).not.toBeInTheDocument()
    expect(screen.getByText('1 installment plan')).toBeInTheDocument()
  })
})
