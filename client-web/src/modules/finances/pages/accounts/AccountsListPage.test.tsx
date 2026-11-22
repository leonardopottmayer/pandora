import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { AccountDto } from '../../models'
import { AccountsListPage } from './AccountsListPage'

const account: AccountDto = {
  id: 'a1',
  name: 'Conta Corrente',
  type: 'checking',
  currency: 'BRL',
  institution: 'Banco X',
  bankCode: null,
  description: null,
  color: null,
  icon: null,
  displayOrder: 0,
  archivedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

const balances: Record<string, { posted: number; projected: number; currency: string }> = {
  a1: { posted: 1000, projected: 800, currency: 'BRL' },
  a2: { posted: 250.5, projected: 250.5, currency: 'BRL' },
  a3: { posted: 40, projected: 40, currency: 'USD' },
  old: { posted: 5000, projected: 5000, currency: 'BRL' },
}

function serveBalances() {
  server.use(
    http.get(`${FINANCES_BASE}/accounts/:id/balance`, ({ params }) =>
      HttpResponse.json({ success: true, data: { accountId: params.id, ...balances[params.id as string] } }),
    ),
  )
}

describe('AccountsListPage', () => {
  it('renders accounts returned by the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () =>
        HttpResponse.json({ success: true, data: [account] }),
      ),
    )
    serveBalances()
    renderWithProviders(<AccountsListPage />)
    expect(await screen.findByText('Conta Corrente')).toBeInTheDocument()
    expect(screen.getByText('Banco X')).toBeInTheDocument()
  })

  it('shows each balance, the projected one when it differs, and a total per currency without archived accounts', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () =>
        HttpResponse.json({
          success: true,
          data: [
            account,
            { ...account, id: 'a2', name: 'Poupança' },
            { ...account, id: 'a3', name: 'Wise', currency: 'USD' },
            { ...account, id: 'old', name: 'Old bank', archivedAt: '2026-01-01T00:00:00Z' },
          ],
        }),
      ),
    )
    serveBalances()
    renderWithProviders(<AccountsListPage />)

    expect(await screen.findByText(/projected R\$\s?800\.00/)).toBeInTheDocument()
    expect(screen.getByText('Total in BRL').nextSibling).toHaveTextContent(/R\$\s?1,250\.50/)
    expect(screen.getByText('Total in USD').nextSibling).toHaveTextContent(/\$40\.00/)
  })

  it('opens the create modal', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] })),
    )
    const user = userEvent.setup()
    renderWithProviders(<AccountsListPage />)

    await user.click(await screen.findByRole('button', { name: /New account/ }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('New account')).toBeInTheDocument()
  })

  it('archives an account and refetches', async () => {
    let archiveCalled = false
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () =>
        HttpResponse.json({ success: true, data: [account] }),
      ),
      http.post(`${FINANCES_BASE}/accounts/a1/archive`, () => {
        archiveCalled = true
        return HttpResponse.json({ success: true, data: { ...account, archivedAt: '2026-06-13T00:00:00Z' } })
      }),
    )
    serveBalances()
    const user = userEvent.setup()
    renderWithProviders(<AccountsListPage />)

    await screen.findByText('Conta Corrente')
    await user.click(screen.getByRole('button', { name: 'Options for Conta Corrente' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Archive' }))
    await waitFor(() => expect(archiveCalled).toBe(true))
  })
})
