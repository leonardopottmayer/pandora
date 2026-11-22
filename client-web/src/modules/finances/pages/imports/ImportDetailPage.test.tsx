import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { ImportFileDto, ImportRowDto } from '../../models'
import { ImportDetailPage } from './ImportDetailPage'

vi.mock('../../components/TransactionDetailDrawer', () => ({
  TransactionDetailDrawer: ({ transactionId }: { transactionId: string }) => <div role="dialog">Transaction {transactionId}</div>,
}))

function file(overrides: Partial<ImportFileDto> = {}): ImportFileDto {
  return {
    id: 'imp1', userId: 'u1', layoutId: null, accountId: 'a1', cardId: null,
    fileName: 'extrato.ofx', fileHash: 'h', fileSize: 10, correlationId: 'c', cutoffDate: null,
    status: 'completed', totalRows: 1, parsedRows: 1, errorRows: 0, duplicateRows: 0,
    suggestionRows: 1, retryCount: 0, errorMessage: null, startedAt: null, completedAt: null,
    createdAt: '2026-06-13T00:00:00Z', ...overrides,
  }
}

const row: ImportRowDto = {
  id: 'r1', importFileId: 'imp1', rowIndex: 1, rawData: 'PAGAMENTO MERCADO 50,00',
  parsedPayload: null, externalId: null, dedupKey: null, dedupStatus: 'new',
  matchedTransactionId: null, matchedPendingTransactionId: null, installmentNumber: null,
  installmentCount: null, matchedInstallmentPlanId: null, pendingTransactionId: null,
  status: 'suggestion-created', errorMessage: null, createdAt: '2026-06-13T00:00:00Z',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/finances/imports/:id" element={<ImportDetailPage />} />
    </Routes>,
    { route: '/finances/imports/imp1' },
  )
}

describe('ImportDetailPage', () => {
  it('shows the parsed description and zero amount, keeping original data and IDs in the row drawer', async () => {
    const parsed = { Description: 'Groceries', OccurredOn: '2026-06-12', Amount: 0, Currency: 'BRL', IsCredit: false }
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1`, () => HttpResponse.json({ success: true, data: file() })),
      http.get(`${FINANCES_BASE}/imports/imp1/rows`, () => HttpResponse.json({ success: true, data: [
        { ...row, parsedPayload: JSON.stringify(parsed), externalId: 'original-reference', installmentNumber: 3, installmentCount: 12 },
      ] })),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderPage()
    await screen.findByText('Groceries', {}, { timeout: 3000 })
    expect(screen.queryByText(row.rawData)).not.toBeInTheDocument()
    expect(screen.getByText(/R\$0\.00/)).toBeInTheDocument()
    expect(screen.getByText('Installment 3/12')).toBeInTheDocument()
    expect(screen.getAllByRole('columnheader')).toHaveLength(2)
    await user.click(screen.getByRole('button', { name: 'Groceries' }))
    const drawer = within(screen.getByRole('dialog'))
    expect(drawer.getByText(row.rawData)).toBeInTheDocument()
    expect(drawer.getByText('original-reference')).toBeInTheDocument()
    expect(drawer.getByText(/"Description": "Groceries"/).textContent).toBe(JSON.stringify(parsed, null, 2))
  })

  it('filters descriptions, row status and duplicate classification and clears only those filters', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1`, () => HttpResponse.json({ success: true, data: file() })),
      http.get(`${FINANCES_BASE}/imports/imp1/rows`, () => HttpResponse.json({ success: true, data: [
        { ...row, parsedPayload: '{"Description":"Groceries"}' },
        { ...row, id: 'r2', rowIndex: 2, parsedPayload: '{"Description":"Fuel"}', dedupStatus: 'matched', pendingTransactionId: 'p2' },
        { ...row, id: 'r3', rowIndex: 3, rawData: 'Invalid row', status: 'error', errorMessage: 'Invalid amount' },
      ] })),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderPage()
    await screen.findByText('Groceries', {}, { timeout: 3000 })
    await user.type(screen.getByPlaceholderText('Search descriptions or references'), 'GROCERIES')
    expect(screen.queryByText('Fuel')).not.toBeInTheDocument()
    expect(screen.getByText('1 row')).toBeInTheDocument()
    await user.click(screen.getByText('Clear filters'))
    await user.click(screen.getByLabelText('Row status'))
    await user.click(screen.getByText('Error', { selector: '.ant-select-item-option-content' }))
    expect(screen.getByText('Invalid amount')).toBeInTheDocument()
    expect(screen.queryByText('Groceries')).not.toBeInTheDocument()
    await user.click(screen.getByText('Clear filters'))
    await user.click(screen.getByLabelText('Duplicate check'))
    await user.click(screen.getByText('Matched', { selector: '.ant-select-item-option-content' }))
    expect(screen.getByText('Fuel')).toBeInTheDocument()
    await user.click(screen.getByLabelText('More actions for row 2'))
    expect(screen.queryByRole('menuitem', { name: 'Link' })).not.toBeInTheDocument()
  })

  it('links a suggestion through the existing transaction picker and refreshes its classification', async () => {
    let linked = false
    let search: string | null = null
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1`, () => HttpResponse.json({ success: true, data: file() })),
      http.get(`${FINANCES_BASE}/imports/imp1/rows`, () => HttpResponse.json({ success: true, data: [
        { ...row, pendingTransactionId: 'p1', parsedPayload: '{"Description":"Groceries"}', dedupStatus: linked ? 'matched' : 'suspected' },
      ] })),
      http.get(`${FINANCES_BASE}/transactions`, ({ request }) => {
        const query = new URL(request.url).searchParams
        search = query.get('text')
        expect(query.get('accountId')).toBe('a1')
        return HttpResponse.json({ success: true, data: [{ id: 'tx1', occurredOn: '2026-06-12', description: 'Existing groceries', amount: 10, currency: 'BRL', kind: 'expense' }] })
      }),
      http.post(`${FINANCES_BASE}/pending-transactions/p1/link`, async ({ request }) => {
        expect(await request.json()).toEqual({ transactionId: 'tx1' })
        linked = true
        return HttpResponse.json({ success: true, data: { id: 'p1' } })
      }),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderPage()
    await screen.findByText('Groceries', {}, { timeout: 3000 })
    expect(search).toBeNull()
    await user.click(screen.getByLabelText('More actions for row 1'))
    await user.click(screen.getByRole('menuitem', { name: 'Link' }))
    const picker = within(await screen.findByRole('dialog'))
    await user.click(await picker.findByText('Existing groceries'))
    expect(search).toBe('Groceries')
    await user.click(picker.getByRole('button', { name: 'Link' }))
    await waitFor(() => expect(linked).toBe(true))
    expect(await screen.findByText('Matched')).toBeInTheDocument()
  })

  it('passes the matched transaction ID to the existing drawer', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1`, () => HttpResponse.json({ success: true, data: file() })),
      http.get(`${FINANCES_BASE}/imports/imp1/rows`, () => HttpResponse.json({ success: true, data: [{ ...row, matchedTransactionId: 'tx1', dedupStatus: 'certain' }] })),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderPage()
    await screen.findByText(row.rawData)
    await user.click(screen.getByLabelText('More actions for row 1'))
    await user.click(screen.getByRole('menuitem', { name: 'View transaction' }))
    expect(await screen.findByRole('dialog')).toHaveTextContent('Transaction tx1')
  })

  it('confirms aborting a non-terminal import with the filename', async () => {
    let aborted = false
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1`, () => HttpResponse.json({ success: true, data: file({ status: 'parsing' }) })),
      http.get(`${FINANCES_BASE}/imports/imp1/rows`, () => HttpResponse.json({ success: true, data: [] })),
      http.post(`${FINANCES_BASE}/imports/imp1/abort`, () => { aborted = true; return HttpResponse.json({ success: true, data: null }) }),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderPage()
    await screen.findByText('extrato.ofx')
    await user.click(screen.getByRole('button', { name: 'Abort' }))
    const confirmation = within(await screen.findByRole('dialog'))
    expect(confirmation.getByText('extrato.ofx')).toBeInTheDocument()
    expect(aborted).toBe(false)
    await user.click(confirmation.getByRole('button', { name: 'Abort' }))
    await waitFor(() => expect(aborted).toBe(true))
  })
  it('renders the file summary and its rows', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1`, () => HttpResponse.json({ success: true, data: file() })),
      http.get(`${FINANCES_BASE}/imports/imp1/rows`, () => HttpResponse.json({ success: true, data: [row] })),
    )
    renderPage()
    expect(await screen.findByText('extrato.ofx')).toBeInTheDocument()
    expect(await screen.findByText('PAGAMENTO MERCADO 50,00')).toBeInTheDocument()
  })

  it('retries a failed import file', async () => {
    let retried = false
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1`, () => HttpResponse.json({ success: true, data: file({ status: 'failed' }) })),
      http.get(`${FINANCES_BASE}/imports/imp1/rows`, () => HttpResponse.json({ success: true, data: [] })),
      http.post(`${FINANCES_BASE}/imports/imp1/retry`, () => {
        retried = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderPage()
    await screen.findByText('extrato.ofx')
    await user.click(screen.getByRole('button', { name: /Retry/ }))
    await waitFor(() => expect(retried).toBe(true))
  })
})
