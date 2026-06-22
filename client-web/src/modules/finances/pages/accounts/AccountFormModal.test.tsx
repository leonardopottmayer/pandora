import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { AccountDto } from '../../models'
import { AccountFormModal } from './AccountFormModal'

const account: AccountDto = {
  id: 'a1',
  name: 'Conta',
  type: 'checking',
  currency: 'BRL',
  institution: null,
  description: null,
  color: null,
  icon: null,
  displayOrder: 0,
  archivedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('AccountFormModal', () => {
  it('creates an account with the default type and currency', async () => {
    let body: Record<string, unknown> | undefined
    const onClose = vi.fn()
    server.use(
      http.post(`${FINANCES_BASE}/accounts`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: account })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AccountFormModal open onClose={onClose} />)

    await user.type(screen.getAllByRole('textbox')[0], 'Conta')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toMatchObject({ name: 'Conta', type: 'checking', currency: 'BRL' })
  })

  it('validates that the name is required', async () => {
    const user = userEvent.setup()
    renderWithProviders(<AccountFormModal open onClose={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Enter the account name.')).toBeInTheDocument()
  })

  it('updates an existing account via PUT (currency locked)', async () => {
    let putCalled = false
    const onClose = vi.fn()
    server.use(
      http.put(`${FINANCES_BASE}/accounts/a1`, () => {
        putCalled = true
        return HttpResponse.json({ success: true, data: { ...account, name: 'Renomeada' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AccountFormModal open account={account} onClose={onClose} />)

    const name = screen.getAllByRole('textbox')[0] as HTMLInputElement
    expect(name.value).toBe('Conta')
    await user.clear(name)
    await user.type(name, 'Renomeada')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(putCalled).toBe(true))
    expect(onClose).toHaveBeenCalled()
  })
})
