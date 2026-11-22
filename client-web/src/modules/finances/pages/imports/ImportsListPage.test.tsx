import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { ImportFileDto } from '../../models'
import { ImportsListPage } from './ImportsListPage'

function importFile(overrides: Partial<ImportFileDto> = {}): ImportFileDto {
  return {
    id: 'imp1',
    userId: 'u1',
    layoutId: null,
    accountId: 'a1',
    cardId: null,
    fileName: 'extrato.ofx',
    fileHash: 'h',
    fileSize: 10,
    correlationId: 'c',
    cutoffDate: null,
    status: 'parsing',
    totalRows: 0,
    parsedRows: 0,
    errorRows: 0,
    duplicateRows: 0,
    suggestionRows: 0,
    retryCount: 0,
    errorMessage: null,
    startedAt: null,
    completedAt: null,
    createdAt: '2026-06-13T00:00:00Z',
    ...overrides,
  }
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function auxHandlers() {
  return [
    http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [] })),
  ]
}

describe('ImportsListPage', () => {
  it('opens the existing import detail from a row', async () => {
    server.use(http.get(`${FINANCES_BASE}/imports`, () => HttpResponse.json({ success: true, data: [importFile({ status: 'completed' })] })), ...auxHandlers())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<Routes>
      <Route path="/finances/imports" element={<ImportsListPage />} />
      <Route path="/finances/imports/:id" element={<h1>Import details</h1>} />
    </Routes>, { route: '/finances/imports' })
    await user.click(await screen.findByText('extrato.ofx'))
    expect(screen.getByRole('heading', { name: 'Import details' })).toBeInTheDocument()
  })
  it('renders import files from the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/imports`, () =>
        HttpResponse.json({ success: true, data: [importFile()] }),
      ),
      ...auxHandlers(),
    )
    renderWithProviders(<ImportsListPage />)
    expect(await screen.findByText('extrato.ofx')).toBeInTheDocument()
  })

  it('aborts an in-progress import', async () => {
    let aborted = false
    server.use(
      http.get(`${FINANCES_BASE}/imports`, () =>
        HttpResponse.json({ success: true, data: [importFile({ status: 'parsing' })] }),
      ),
      http.post(`${FINANCES_BASE}/imports/imp1/abort`, () => {
        aborted = true
        return HttpResponse.json({ success: true, data: null })
      }),
      ...auxHandlers(),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<ImportsListPage />)

    await screen.findByText('extrato.ofx')
    await user.click(screen.getByLabelText('More actions for extrato.ofx'))
    await user.click(screen.getByRole('menuitem', { name: 'Abort' }))
    const confirmation = within(await screen.findByRole('dialog'))
    expect(confirmation.getByText('extrato.ofx')).toBeInTheDocument()
    expect(aborted).toBe(false)
    await user.click(confirmation.getByRole('button', { name: 'Abort' }))
    await waitFor(() => expect(aborted).toBe(true))
  })

  it('retries a failed import', async () => {
    let retried = false
    server.use(
      http.get(`${FINANCES_BASE}/imports`, () =>
        HttpResponse.json({ success: true, data: [importFile({ status: 'failed' })] }),
      ),
      http.post(`${FINANCES_BASE}/imports/imp1/retry`, () => {
        retried = true
        return HttpResponse.json({ success: true, data: null })
      }),
      ...auxHandlers(),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<ImportsListPage />)

    await screen.findByText('extrato.ofx')
    await user.click(screen.getByLabelText('More actions for extrato.ofx'))
    await user.click(screen.getByRole('menuitem', { name: 'Retry' }))
    await waitFor(() => expect(retried).toBe(true))
  })

  it('filters filenames and status, keeps destinations and counters visible, and hides terminal actions', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/imports`, () => HttpResponse.json({ success: true, data: [
        importFile({ status: 'completed', totalRows: 10, parsedRows: 9, suggestionRows: 7, duplicateRows: 2, errorRows: 1 }),
        importFile({ id: 'failed', fileName: 'failed.csv', status: 'failed', errorMessage: 'Could not read the header' }),
      ] })),
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [{ id: 'a1', name: 'Checking', currency: 'BRL' }] })),
      ...auxHandlers(),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<ImportsListPage />)
    await screen.findByText('extrato.ofx')
    expect(screen.getAllByText('Checking').length).toBeGreaterThan(0)
    expect(screen.getByText('Rows read: 9/10')).toBeInTheDocument()
    expect(screen.getByText('7 suggestions generated')).toBeInTheDocument()
    expect(screen.getByText('2 duplicates')).toBeInTheDocument()
    expect(screen.getByText('1 error')).toBeInTheDocument()
    await user.type(screen.getByPlaceholderText('Search files'), 'EXTRATO')
    expect(screen.queryByText('failed.csv')).not.toBeInTheDocument()
    await user.click(screen.getByLabelText('More actions for extrato.ofx'))
    expect(screen.queryByRole('menuitem', { name: 'Abort' })).not.toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: 'Retry' })).not.toBeInTheDocument()
    await user.keyboard('{Escape}')
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    await user.click(screen.getByRole('combobox', { name: 'Status' }))
    await user.click(screen.getByText('Failed', { selector: '.ant-select-item-option-content' }))
    expect(screen.queryByText('extrato.ofx')).not.toBeInTheDocument()
    expect(screen.getByText('failed.csv')).toBeInTheDocument()
    expect(screen.getByText('Could not read the header')).toBeInTheDocument()
    expect(screen.getByText('1 import')).toBeInTheDocument()
  })

  it('opens the upload dialog from the heading', async () => {
    server.use(http.get(`${FINANCES_BASE}/imports`, () => HttpResponse.json({ success: true, data: [] })), ...auxHandlers())
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<ImportsListPage />)
    await user.click(screen.getByRole('button', { name: /Upload file/ }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.getByText('Optional cutoff date')).toBeInTheDocument()
  })
})
