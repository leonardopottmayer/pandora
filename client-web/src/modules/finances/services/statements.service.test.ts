import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { closeStatement, getStatement, payStatement } from './statements.service'
import type { CardStatementDto } from '../models'

const statement: CardStatementDto = {
  id: 'st1',
  cardId: 'k1',
  referenceMonth: '2026-06',
  closingDate: '2026-06-05',
  dueDate: '2026-06-12',
  status: 'open',
  totalAmount: 300,
  paidAmount: 0,
  remainingAmount: 300,
  closedAt: null,
  paidAt: null,
  overdueAt: null,
}

describe('statements.service', () => {
  it('gets a statement with its transactions', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/statements/st1`, () =>
        HttpResponse.json({ success: true, data: { statement, transactions: [] } }),
      ),
    )
    const detail = await getStatement('st1')
    expect(detail.statement.id).toBe('st1')
    expect(detail.transactions).toEqual([])
  })

  it('pays a statement forwarding the payload', async () => {
    let body: unknown
    server.use(
      http.post(`${FINANCES_BASE}/statements/st1/pay`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { ...statement, status: 'paid', paidAmount: 300, remainingAmount: 0 } })
      }),
    )
    const result = await payStatement('st1', { accountId: 'a1', amount: 300 })
    expect(result.status).toBe('paid')
    expect(body).toMatchObject({ accountId: 'a1', amount: 300 })
  })

  it('closes a statement', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/statements/st1/close`, () =>
        HttpResponse.json({ success: true, data: { ...statement, status: 'closed' } }),
      ),
    )
    expect((await closeStatement('st1')).status).toBe('closed')
  })
})
