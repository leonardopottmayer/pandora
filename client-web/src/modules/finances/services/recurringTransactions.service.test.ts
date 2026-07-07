import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import {
  createRecurringTransaction,
  deleteRecurringTransaction,
  listRecurringTransactions,
  pauseRecurringTransaction,
  resumeRecurringTransaction,
  updateRecurringTransaction,
} from './recurringTransactions.service'
import type { RecurringTransactionDto } from '../models'

const recurring: RecurringTransactionDto = {
  id: 'r1',
  name: 'Aluguel',
  accountId: 'a1',
  cardId: null,
  kind: 'expense',
  amount: 1500,
  amountIsEstimate: false,
  description: 'Aluguel mensal',
  payee: null,
  systemCategoryId: null,
  userCategoryId: null,
  frequency: 'monthly',
  interval: 1,
  dayOfMonth: null,
  weekday: null,
  startDate: '2026-07-01',
  endDate: null,
  maxOccurrences: null,
  status: 'active',
  autoPost: false,
  autoGenerate: true,
  nextOccurrenceOn: '2026-07-01',
  occurrencesCount: 0,
  createdAt: '2026-06-16T00:00:00Z',
  updatedAt: null,
}

const BASE = `${FINANCES_BASE}/recurring-transactions`

describe('recurringTransactions.service', () => {
  it('lists recurring transactions', async () => {
    server.use(http.get(BASE, () => HttpResponse.json({ success: true, data: [recurring] })))
    const result = await listRecurringTransactions()
    expect(result).toHaveLength(1)
    expect(result[0].name).toBe('Aluguel')
  })

  it('creates a recurring transaction', async () => {
    let body: unknown
    server.use(
      http.post(BASE, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: recurring })
      }),
    )
    const created = await createRecurringTransaction({
      name: 'Aluguel',
      accountId: 'a1',
      kind: 'expense',
      amount: 1500,
      amountIsEstimate: false,
      description: 'Aluguel mensal',
      frequency: 'monthly',
      interval: 1,
      startDate: '2026-07-01',
      autoPost: false,
      autoGenerate: true,
    })
    expect(created.id).toBe('r1')
    expect((body as { frequency: string }).frequency).toBe('monthly')
  })

  it('updates a recurring transaction', async () => {
    server.use(
      http.put(`${BASE}/r1`, () =>
        HttpResponse.json({ success: true, data: { ...recurring, name: 'Aluguel novo' } }),
      ),
    )
    const updated = await updateRecurringTransaction('r1', {
      name: 'Aluguel novo',
      amountIsEstimate: false,
      description: 'Aluguel mensal',
      autoPost: false,
      autoGenerate: true,
    })
    expect(updated.name).toBe('Aluguel novo')
  })

  it('pauses and resumes via dedicated endpoints', async () => {
    server.use(
      http.post(`${BASE}/r1/pause`, () =>
        HttpResponse.json({ success: true, data: { ...recurring, status: 'paused' } }),
      ),
      http.post(`${BASE}/r1/resume`, () =>
        HttpResponse.json({ success: true, data: { ...recurring, status: 'active' } }),
      ),
    )
    expect((await pauseRecurringTransaction('r1')).status).toBe('paused')
    expect((await resumeRecurringTransaction('r1')).status).toBe('active')
  })

  it('deletes a recurring transaction', async () => {
    let called = false
    server.use(
      http.delete(`${BASE}/r1`, () => {
        called = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await deleteRecurringTransaction('r1')
    expect(called).toBe(true)
  })
})
