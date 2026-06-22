import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { ApiResponseError } from '@/lib/api/envelope'
import {
  archiveAccount,
  createAccount,
  deleteAccount,
  getAccount,
  getAccountBalance,
  getAccountTransactions,
  listAccounts,
  setAccountTags,
  unarchiveAccount,
  updateAccount,
} from './accounts.service'
import type { AccountDto } from '../models'

const sampleAccount: AccountDto = {
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

describe('accounts.service', () => {
  it('lists accounts and forwards includeArchived', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, ({ request }) => {
        seenUrl = new URL(request.url)
        return HttpResponse.json({ success: true, data: [sampleAccount] })
      }),
    )

    const result = await listAccounts({ includeArchived: true })
    expect(result).toHaveLength(1)
    expect(result[0].name).toBe('Conta Corrente')
    expect(seenUrl?.searchParams.get('includeArchived')).toBe('true')
  })

  it('posts the create payload and unwraps the created account', async () => {
    let body: unknown
    server.use(
      http.post(`${FINANCES_BASE}/accounts`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: sampleAccount })
      }),
    )

    const created = await createAccount({
      name: 'Conta Corrente',
      type: 'checking',
      currency: 'BRL',
      displayOrder: 0,
    })
    expect(created.id).toBe('a1')
    expect(body).toMatchObject({ name: 'Conta Corrente', currency: 'BRL' })
  })

  it('updates via PUT', async () => {
    server.use(
      http.put(`${FINANCES_BASE}/accounts/a1`, () =>
        HttpResponse.json({ success: true, data: { ...sampleAccount, name: 'Renomeada' } }),
      ),
    )
    const updated = await updateAccount('a1', {
      name: 'Renomeada',
      type: 'checking',
      displayOrder: 0,
    })
    expect(updated.name).toBe('Renomeada')
  })

  it('archives via POST', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/accounts/a1/archive`, () =>
        HttpResponse.json({ success: true, data: { ...sampleAccount, archivedAt: '2026-06-13T00:00:00Z' } }),
      ),
    )
    const archived = await archiveAccount('a1')
    expect(archived.archivedAt).not.toBeNull()
  })

  it('reads the balance', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/accounts/a1/balance`, () =>
        HttpResponse.json({
          success: true,
          data: { accountId: 'a1', currency: 'BRL', posted: 150.5, projected: 200 },
        }),
      ),
    )
    const balance = await getAccountBalance('a1')
    expect(balance.posted).toBe(150.5)
    expect(balance.projected).toBe(200)
  })

  it('rejects with ApiResponseError on backend error', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/accounts`, () =>
        HttpResponse.json(
          { success: false, errorCode: 'ACCOUNT_NAME_ALREADY_EXISTS', errorMessage: 'Duplicada', fieldErrors: null, traceId: null },
          { status: 400 },
        ),
      ),
    )
    await expect(
      createAccount({ name: 'x', type: 'checking', currency: 'BRL', displayOrder: 0 }),
    ).rejects.toBeInstanceOf(ApiResponseError)
  })

  it('fetches a single account', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/accounts/a1`, () =>
        HttpResponse.json({ success: true, data: sampleAccount }),
      ),
    )
    expect((await getAccount('a1')).id).toBe('a1')
  })

  it('lists account transactions forwarding filters', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/accounts/a1/transactions`, ({ request }) => {
        seenUrl = new URL(request.url)
        return HttpResponse.json({ success: true, data: [] })
      }),
    )
    await getAccountTransactions('a1', { from: '2026-06-01', to: '2026-06-30', status: 'posted', take: 50 })
    expect(seenUrl?.searchParams.get('from')).toBe('2026-06-01')
    expect(seenUrl?.searchParams.get('status')).toBe('posted')
    expect(seenUrl?.searchParams.get('take')).toBe('50')
  })

  it('deletes an account', async () => {
    let called = false
    server.use(
      http.delete(`${FINANCES_BASE}/accounts/a1`, () => {
        called = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await deleteAccount('a1')
    expect(called).toBe(true)
  })

  it('unarchives via POST', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/accounts/a1/unarchive`, () =>
        HttpResponse.json({ success: true, data: { ...sampleAccount, archivedAt: null } }),
      ),
    )
    expect((await unarchiveAccount('a1')).archivedAt).toBeNull()
  })

  it('sets account tags via PUT', async () => {
    let body: unknown
    server.use(
      http.put(`${FINANCES_BASE}/accounts/a1/tags`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await setAccountTags('a1', { tagIds: ['t1', 't2'] })
    expect(body).toEqual({ tagIds: ['t1', 't2'] })
  })
})
