import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import {
  approvePendingTransaction,
  approvePendingTransactionBatch,
  listPendingTransactions,
  rejectPendingTransaction,
  updatePendingTransaction,
} from './pendingTransactions.service'
import type { PendingTransactionDto } from '../models'

const pending: PendingTransactionDto = {
  id: 'p1',
  source: 'recurrence',
  recurringTransactionId: 'r1',
  accountId: 'a1',
  cardId: null,
  kind: 'expense',
  amount: 1500,
  currency: 'BRL',
  occurredOn: '2026-07-01',
  description: 'Aluguel mensal',
  payee: null,
  notes: null,
  systemCategoryId: null,
  userCategoryId: null,
  suggestedStatementId: null,
  originalPayload: '{}',
  status: 'pending',
  decidedAt: null,
  decidedBy: null,
  rejectionReason: null,
  transactionId: null,
  createdAt: '2026-07-01T00:00:00Z',
  updatedAt: null,
}

const BASE = `${FINANCES_BASE}/pending-transactions`

describe('pendingTransactions.service', () => {
  it('lists pending transactions forwarding filters', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(BASE, ({ request }) => {
        seenUrl = new URL(request.url)
        return HttpResponse.json({ success: true, data: [pending] })
      }),
    )
    const result = await listPendingTransactions({ accountId: 'a1', take: 50 })
    expect(result).toHaveLength(1)
    expect(seenUrl?.searchParams.get('accountId')).toBe('a1')
  })

  it('updates a pending transaction', async () => {
    server.use(
      http.put(`${BASE}/p1`, () =>
        HttpResponse.json({ success: true, data: { ...pending, amount: 99 } }),
      ),
    )
    const updated = await updatePendingTransaction('p1', {
      kind: 'expense',
      amount: 99,
      occurredOn: '2026-07-01',
      description: 'Aluguel mensal',
    })
    expect(updated.amount).toBe(99)
  })

  it('approves a pending transaction returning the created transaction', async () => {
    server.use(
      http.post(`${BASE}/p1/approve`, () =>
        HttpResponse.json({ success: true, data: { id: 'tx1', status: 'posted' } }),
      ),
    )
    const tx = await approvePendingTransaction('p1')
    expect(tx.id).toBe('tx1')
  })

  it('rejects a pending transaction forwarding the reason', async () => {
    let body: unknown
    server.use(
      http.post(`${BASE}/p1/reject`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { ...pending, status: 'rejected' } })
      }),
    )
    const rejected = await rejectPendingTransaction('p1', 'Nao quero')
    expect(rejected.status).toBe('rejected')
    expect((body as { reason: string }).reason).toBe('Nao quero')
  })

  it('approves a batch returning the count', async () => {
    server.use(
      http.post(`${BASE}/approve-batch`, () => HttpResponse.json({ success: true, data: 2 })),
    )
    const count = await approvePendingTransactionBatch(['p1', 'p2'])
    expect(count).toBe(2)
  })
})
