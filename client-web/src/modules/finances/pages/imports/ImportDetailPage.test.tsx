import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { ImportFileDto, ImportRowDto } from '../../models'
import { ImportDetailPage } from './ImportDetailPage'

function file(overrides: Partial<ImportFileDto> = {}): ImportFileDto {
  return {
    id: 'imp1', userId: 'u1', layoutId: null, accountId: 'a1', cardId: null,
    fileName: 'extrato.ofx', fileHash: 'h', fileSize: 10, correlationId: 'c',
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
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('extrato.ofx')
    await user.click(screen.getByRole('button', { name: /Retry/ }))
    await waitFor(() => expect(retried).toBe(true))
  })
})
