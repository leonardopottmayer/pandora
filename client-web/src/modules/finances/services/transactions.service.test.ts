import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import {
  createTransaction,
  createTransfer,
  getTransaction,
  listTransactions,
  postTransaction,
  voidTransaction,
} from './transactions.service'
import type { TransactionDto } from '../models'

const tx: TransactionDto = {
  id: 'x1',
  accountId: 'a1',
  cardStatementId: null,
  cardId: null,
  paidStatementId: null,
  kind: 'expense',
  status: 'posted',
  amount: 50,
  currency: 'BRL',
  occurredOn: '2026-06-13',
  description: 'Mercado',
  payee: null,
  notes: null,
  systemCategoryId: null,
  userCategoryId: null,
  transferGroupId: null,
  fxRate: null,
  installmentPlanId: null,
  installmentNumber: null,
  origin: 'manual',
  postedAt: '2026-06-13T00:00:00Z',
  voidedAt: null,
  voidReason: null,
  descriptionKey: null,
  descriptionArgs: null,
}

describe('transactions.service', () => {
  it('lists transactions forwarding filters', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, ({ request }) => {
        seenUrl = new URL(request.url)
        return HttpResponse.json({ success: true, data: [tx] })
      }),
    )
    const result = await listTransactions({ accountId: 'a1', status: 'posted', take: 10 })
    expect(result).toHaveLength(1)
    expect(seenUrl?.searchParams.get('accountId')).toBe('a1')
    expect(seenUrl?.searchParams.get('status')).toBe('posted')
  })

  it('fetches a single transaction by id', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/transactions/x1`, () =>
        HttpResponse.json({ success: true, data: tx }),
      ),
    )
    const result = await getTransaction('x1')
    expect(result.id).toBe('x1')
  })

  it('creates a single transaction', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/transactions`, () => HttpResponse.json({ success: true, data: tx })),
    )
    const created = await createTransaction({
      accountId: 'a1',
      kind: 'expense',
      amount: 50,
      occurredOn: '2026-06-13',
      description: 'Mercado',
    })
    expect(Array.isArray(created)).toBe(false)
  })

  it('creates a transfer returning two legs', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/transactions/transfer`, () =>
        HttpResponse.json({
          success: true,
          data: [tx, { ...tx, id: 'x2', kind: 'transfer-in' }],
        }),
      ),
    )
    const legs = await createTransfer({
      fromAccountId: 'a1',
      toAccountId: 'a2',
      amountOut: 50,
      occurredOn: '2026-06-13',
      description: 'Transferencia',
    })
    expect(legs).toHaveLength(2)
  })

  it('posts and voids via dedicated endpoints', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/transactions/x1/post`, () =>
        HttpResponse.json({ success: true, data: { ...tx, status: 'posted' } }),
      ),
      http.post(`${FINANCES_BASE}/transactions/x1/void`, () =>
        HttpResponse.json({ success: true, data: { ...tx, status: 'void' } }),
      ),
    )
    expect((await postTransaction('x1')).status).toBe('posted')
    expect((await voidTransaction('x1')).status).toBe('void')
  })
})
