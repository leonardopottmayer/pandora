import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import {
  createCard,
  getCardAvailableLimit,
  getCardStatements,
  listCards,
} from './cards.service'
import type { CardDto } from '../models'

const card: CardDto = {
  id: 'k1',
  name: 'Nubank',
  brand: 'Mastercard',
  lastFour: '1234',
  creditLimit: 5000,
  closingDay: 5,
  dueDay: 12,
  currency: 'BRL',
  defaultPaymentAccountId: null,
  archivedAt: null,
}

describe('cards.service', () => {
  it('lists cards', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [card] })),
    )
    const result = await listCards()
    expect(result[0].name).toBe('Nubank')
  })

  it('creates a card', async () => {
    let body: unknown
    server.use(
      http.post(`${FINANCES_BASE}/cards`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: card })
      }),
    )
    const created = await createCard({
      name: 'Nubank',
      closingDay: 5,
      dueDay: 12,
      currency: 'BRL',
    })
    expect(created.id).toBe('k1')
    expect(body).toMatchObject({ name: 'Nubank', closingDay: 5, dueDay: 12 })
  })

  it('reads statements and available limit', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/cards/k1/statements`, () =>
        HttpResponse.json({ success: true, data: [] }),
      ),
      http.get(`${FINANCES_BASE}/cards/k1/available-limit`, () =>
        HttpResponse.json({ success: true, data: { cardId: 'k1', creditLimit: 5000, availableLimit: 4200 } }),
      ),
    )
    expect(await getCardStatements('k1')).toEqual([])
    expect((await getCardAvailableLimit('k1')).availableLimit).toBe(4200)
  })
})
