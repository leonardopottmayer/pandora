import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { AttachmentDto } from '../models'
import { AttachmentsPanel } from './AttachmentsPanel'

const boleto: AttachmentDto = {
  id: 'a1',
  transactionId: null,
  pendingTransactionId: 'p1',
  kind: 'bill',
  fileName: 'boleto-junho.pdf',
  contentType: 'application/pdf',
  sizeBytes: 120_000,
  url: '/api/v1/finances/attachments/a1',
  createdAt: '2026-10-03T12:00:00Z',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('AttachmentsPanel', () => {
  it("lists the owner's attachments with their kind", async () => {
    server.use(
      http.get(`${FINANCES_BASE}/attachments`, ({ request }) =>
        new URL(request.url).searchParams.get('pendingTransactionId') === 'p1'
          ? HttpResponse.json({ success: true, data: [boleto] })
          : HttpResponse.json({ success: true, data: [] }),
      ),
    )
    renderWithProviders(<AttachmentsPanel owner={{ pendingTransactionId: 'p1' }} />)

    expect(await screen.findByText('boleto-junho.pdf')).toBeInTheDocument()
    expect(screen.getByText('Bill')).toBeInTheDocument()
    expect(screen.getByText('117 KB')).toBeInTheDocument()
  })

  it('uploads a file with the chosen kind to the owner', async () => {
    let sent: string | null = null
    server.use(
      http.get(`${FINANCES_BASE}/attachments`, () => HttpResponse.json({ success: true, data: [] })),
      http.post(`${FINANCES_BASE}/attachments`, async ({ request }) => {
        // The raw multipart body: jsdom's FormData does not parse back through MSW.
        sent = await request.text()
        return HttpResponse.json({ success: true, data: { ...boleto, kind: 'receipt' } })
      }),
    )
    const user = userEvent.setup()
    const { container } = renderWithProviders(<AttachmentsPanel owner={{ transactionId: 't1' }} />)
    await screen.findByText(/No attachments yet/)

    const input = container.querySelector('input[type="file"]') as HTMLInputElement
    await user.upload(input, new File(['%PDF'], 'pix.pdf', { type: 'application/pdf' }))

    await waitFor(() => expect(sent).not.toBeNull())
    expect(sent).toMatch(/name="kind"\r\n\r\nreceipt/)
    expect(sent).toMatch(/name="transactionId"\r\n\r\nt1/)
  })

  it('removes an attachment after confirming', async () => {
    let deleted = false
    server.use(
      http.get(`${FINANCES_BASE}/attachments`, () => HttpResponse.json({ success: true, data: [boleto] })),
      http.delete(`${FINANCES_BASE}/attachments/a1`, () => {
        deleted = true
        return HttpResponse.json({ success: true, data: true })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AttachmentsPanel owner={{ pendingTransactionId: 'p1' }} />)

    await screen.findByText('boleto-junho.pdf')
    await user.click(screen.getByRole('button', { name: 'Delete' }))
    // The confirmation's own "Delete" is the last one on screen.
    await waitFor(() => expect(screen.getAllByRole('button', { name: 'Delete' })).toHaveLength(2))
    await user.click(screen.getAllByRole('button', { name: 'Delete' })[1])
    await waitFor(() => expect(deleted).toBe(true))
  })
})
