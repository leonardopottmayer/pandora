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
  description: null,
  color: null,
  icon: null,
  displayOrder: 0,
  archivedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('AccountsListPage', () => {
  it('renders accounts returned by the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () =>
        HttpResponse.json({ success: true, data: [account] }),
      ),
    )
    renderWithProviders(<AccountsListPage />)
    expect(await screen.findByText('Conta Corrente')).toBeInTheDocument()
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
    const user = userEvent.setup()
    renderWithProviders(<AccountsListPage />)

    await screen.findByText('Conta Corrente')
    await user.click(screen.getByRole('button', { name: 'Archive' }))
    await waitFor(() => expect(archiveCalled).toBe(true))
  })
})
