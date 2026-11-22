import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CardStatementDetailDto } from '../../models'
import { StatementDetailPage } from './StatementDetailPage'

function detail(status: CardStatementDetailDto['statement']['status']): CardStatementDetailDto {
  return {
    statement: {
      id: 's1',
      cardId: 'c1',
      referenceMonth: '2026-06',
      closingDate: '2026-06-05',
      dueDate: '2026-06-12',
      status,
      totalAmount: 1000,
      paidAmount: 0,
      remainingAmount: 1000,
      closedAt: null,
      paidAt: null,
      overdueAt: null,
    },
    transactions: [
      {
        id: 'x1', accountId: null, cardStatementId: 's1', cardId: 'c1', paidStatementId: null,
        kind: 'expense', status: 'posted', amount: 200, currency: 'BRL', occurredOn: '2026-06-03',
        description: 'Compra A', payee: null, notes: null, systemCategoryId: null, userCategoryId: null,
        transferGroupId: null, fxRate: null, installmentPlanId: null, installmentNumber: null,
        origin: 'manual', postedAt: '2026-06-03T00:00:00Z', voidedAt: null, voidReason: null,
        descriptionKey: null, descriptionArgs: null,
        statementReferenceMonth: null, statementDueDate: null, attachmentCount: 0,
      },
    ],
  }
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/finances/statements/:id" element={<StatementDetailPage />} />
    </Routes>,
    { route: '/finances/statements/s1' },
  )
}

function auxHandlers() {
  return [
    http.get(`${FINANCES_BASE}/cards/c1`, () =>
      HttpResponse.json({ success: true, data: { id: 'c1', name: 'Nubank', brand: null, lastFour: null, creditLimit: null, closingDay: 5, dueDay: 12, currency: 'BRL', accountId: 'a1', archivedAt: null } }),
    ),
    http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('StatementDetailPage', () => {
  it('uses the card currency for totals even without transactions', async () => {
    const response = detail('open')
    response.transactions = []
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: response })),
      http.get(`${FINANCES_BASE}/cards/c1`, () => HttpResponse.json({ success: true, data: { id: 'c1', name: 'USD card', currency: 'USD' } })),
      ...auxHandlers(),
    )
    renderPage()
    expect(await screen.findAllByText('$1,000.00')).toHaveLength(2)
    expect(screen.getByText('0 transactions')).toBeInTheDocument()
  })

  it('keeps secondary filters when hidden and clears every controlled field', async () => {
    const response = detail('open')
    response.transactions.push({ ...response.transactions[0], id: 'x2', description: 'Compra B', amount: 50 })
    server.use(http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: response })), ...auxHandlers())
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra B')
    expect(screen.queryByLabelText('Amount from')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'More filters' }))
    await user.type(screen.getByLabelText('Amount from'), '100')
    expect(screen.queryByText('Compra B')).not.toBeInTheDocument()
    expect(screen.getByText('1 transaction')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'More filters' }))
    expect(screen.queryByLabelText('Amount from')).not.toBeInTheDocument()
    expect(screen.queryByText('Compra B')).not.toBeInTheDocument()
    await user.type(screen.getByPlaceholderText('Search'), 'Compra A')
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    expect(screen.getByPlaceholderText('Search')).toHaveValue('')
    expect(screen.getByText('Compra B')).toBeInTheDocument()
    expect(screen.getByText('2 transactions')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'More filters' }))
    expect(screen.getByLabelText('Amount from')).toHaveValue('')
  })

  it('opens the existing payment form with the remaining amount', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('closed') })),
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] })),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra A')
    await user.click(screen.getByRole('button', { name: 'Pay statement' }))
    const dialog = within(await screen.findByRole('dialog'))
    expect(Number((dialog.getByRole('spinbutton') as HTMLInputElement).value)).toBe(1000)
    expect(dialog.getByText('Payment account')).toBeInTheDocument()
  })

  it('loads statement attachments only after opening their drawer', async () => {
    const owners: string[] = []
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('open') })),
      http.get(`${FINANCES_BASE}/attachments`, ({ request }) => {
        owners.push(new URL(request.url).searchParams.get('cardStatementId') ?? 'queue')
        return HttpResponse.json({ success: true, data: [] })
      }),
      http.get(`${FINANCES_BASE}/transactions`, () => HttpResponse.json({ success: true, data: [] })),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra A')
    expect(owners).toHaveLength(0)
    await user.click(screen.getByRole('button', { name: 'Attachments' }))
    expect(await screen.findByRole('dialog')).toHaveTextContent('Attachments')
    await waitFor(() => expect(owners).toContain('s1'))
  })

  it('reopens a closed statement', async () => {
    let reopened = false
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('closed') })),
      http.post(`${FINANCES_BASE}/statements/s1/reopen`, () => { reopened = true; return HttpResponse.json({ success: true, data: detail('open').statement }) }),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra A')
    expect(screen.queryByRole('button', { name: 'Close statement' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Reopen statement' }))
    await waitFor(() => expect(reopened).toBe(true))
  })

  it('requires confirmation before settling without cash', async () => {
    let settled = false
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('closed') })),
      http.post(`${FINANCES_BASE}/statements/s1/settle`, () => { settled = true; return HttpResponse.json({ success: true, data: detail('paid').statement }) }),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra A')
    await user.click(screen.getByRole('button', { name: 'Settle without cash' }))
    expect(settled).toBe(false)
    const confirmation = within(await screen.findByRole('tooltip'))
    expect(confirmation.getByText(/without debiting any account/)).toBeInTheDocument()
    await user.click(confirmation.getByRole('button', { name: 'Settle without cash' }))
    await waitFor(() => expect(settled).toBe(true))
  })

  it('hides lifecycle actions on a paid statement with no remaining balance', async () => {
    const response = detail('paid')
    response.statement.remainingAmount = 0
    server.use(http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: response })), ...auxHandlers())
    renderPage()
    await screen.findByText('Compra A')
    for (const name of ['Pay statement', 'Close statement', 'Reopen statement', 'Settle without cash']) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument()
    }
  })

  it('renders the statement and its transactions', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('open') })),
      ...auxHandlers(),
    )
    renderPage()
    expect(await screen.findByText('Compra A')).toBeInTheDocument()
    // The card name resolves in the breadcrumb.
    expect(await screen.findByText('Nubank')).toBeInTheDocument()
  })

  it('closes an open statement', async () => {
    let closed = false
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('open') })),
      http.post(`${FINANCES_BASE}/statements/s1/close`, () => {
        closed = true
        return HttpResponse.json({ success: true, data: detail('closed').statement })
      }),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra A')
    await user.click(screen.getByRole('button', { name: 'Close statement' }))
    await waitFor(() => expect(closed).toBe(true))
  })

  it('filters transactions by the text search', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: detail('open') })),
      ...auxHandlers(),
    )
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Compra A')
    await user.type(screen.getByPlaceholderText('Search'), 'inexistente')
    await waitFor(() => expect(screen.queryByText('Compra A')).not.toBeInTheDocument())
  })

  it('keeps date, category, installment and state visible on a narrow screen without separate wide columns', async () => {
    const response = detail('open')
    response.transactions[0] = {
      ...response.transactions[0],
      description: 'A long purchase description that must fit inside the table',
      installmentPlanId: 'plan1', installmentNumber: 3, systemCategoryId: 'cat1',
    }
    server.use(
      http.get(`${FINANCES_BASE}/statements/s1`, () => HttpResponse.json({ success: true, data: response })),
      http.get(`${FINANCES_BASE}/categories/system`, () => HttpResponse.json({ success: true, data: [{ id: 'cat1', name: 'Shopping', children: [] }] })),
      ...auxHandlers(),
    )
    renderPage()
    const description = await screen.findByText(response.transactions[0].description)
    expect(description).toHaveAttribute('title', response.transactions[0].description)
    expect(screen.getByText('Installment 3')).toBeInTheDocument()
    expect(screen.getByText('Shopping')).toBeInTheDocument()
    expect(screen.getByText('Posted')).toBeInTheDocument()
    expect(screen.queryByRole('columnheader', { name: 'Date' })).not.toBeInTheDocument()
    expect(screen.getAllByRole('columnheader')).toHaveLength(2)
  })
})
