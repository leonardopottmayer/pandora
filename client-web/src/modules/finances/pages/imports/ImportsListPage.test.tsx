import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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
    const user = userEvent.setup()
    renderWithProviders(<ImportsListPage />)

    await screen.findByText('extrato.ofx')
    // The abort action is the only danger button in the row (icon-only).
    const dangerButton = document.querySelector('.ant-btn-dangerous') as HTMLElement
    await user.click(dangerButton)
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
    const user = userEvent.setup()
    renderWithProviders(<ImportsListPage />)

    await screen.findByText('extrato.ofx')
    // Retry is the first (non-danger) action button in the row.
    const buttons = document.querySelectorAll('tbody .ant-btn')
    await user.click(buttons[0] as HTMLElement)
    await waitFor(() => expect(retried).toBe(true))
  })
})
