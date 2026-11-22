import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { AdminUserDto } from '../../models'
import { UsersPage } from './UsersPage'

const BASE = `${TEST_API_BASE}/api/v1.0/admin`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const user = (id: string, name: string, awaitingApproval: boolean): AdminUserDto => ({
  id,
  name,
  username: name.toLowerCase(),
  email: `${name.toLowerCase()}@mail.test`,
  activated: true,
  disabled: false,
  awaitingApproval,
  mfaEnabled: false,
  lastSignInAt: null,
  createdAt: '2026-11-01T00:00:00Z',
  roleIds: [],
})

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('UsersPage', () => {
  it('lists who is waiting first and approves the selected ones', async () => {
    let sent: unknown = null
    server.use(
      http.get(`${BASE}/users`, () => ok([user('u1', 'Ana', false), user('u2', 'Bia', true)])),
      http.get(`${BASE}/roles`, () => ok([])),
      http.post(`${BASE}/users/approve`, async ({ request }) => {
        sent = await request.json()
        return ok(1)
      }),
    )
    renderWithProviders(<UsersPage />)

    expect(await screen.findByText('1 account is waiting for your approval.')).toBeInTheDocument()
    const rows = screen.getAllByRole('row').slice(1)
    expect(within(rows[0]).getByText('Bia')).toBeInTheDocument()
    expect(within(rows[0]).getByText('Awaiting approval')).toBeInTheDocument()

    await userEvent.click(within(rows[0]).getByRole('checkbox'))
    await userEvent.click(screen.getByRole('button', { name: 'Approve 1' }))

    await waitFor(() => expect(sent).toEqual({ userIds: ['u2'] }))
  })
})
