import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { PayStatementModal } from './PayStatementModal'

const accounts = [
  { id: 'a1', name: 'Conta', currency: 'BRL', type: 'checking', institution: null, description: null, color: null, icon: null, displayOrder: 0, archivedAt: null },
]

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function accountsHandler() {
  return http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: accounts }))
}

describe('PayStatementModal', () => {
  it('requires an account before paying', async () => {
    let paid = false
    server.use(
      accountsHandler(),
      http.post(`${FINANCES_BASE}/statements/s1/pay`, () => {
        paid = true
        return HttpResponse.json({ success: true, data: {} })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<PayStatementModal open statementId="s1" remainingAmount={500} onClose={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Select an account.')).toBeInTheDocument()
    expect(paid).toBe(false)
  })

  it('pays the statement with the pre-filled remaining amount', async () => {
    let body: Record<string, unknown> | undefined
    const onClose = vi.fn()
    server.use(
      accountsHandler(),
      http.post(`${FINANCES_BASE}/statements/s1/pay`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: {} })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<PayStatementModal open statementId="s1" remainingAmount={500} onClose={onClose} />)

    // amount comes pre-filled from remainingAmount
    expect((screen.getByRole('spinbutton') as HTMLInputElement).value).toContain('500')

    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByText('Conta (BRL)'))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({ accountId: 'a1', amount: 500 })
  })
})
