import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import {
  archiveCard,
  createCard,
  deleteCard,
  getCard,
  getCardAvailableLimit,
  getCardInstallmentPlans,
  getCardStatements,
  listCards,
  setCardTags,
  unarchiveCard,
  updateCard,
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

  it('forwards includeArchived and tags when listing', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/cards`, ({ request }) => {
        seenUrl = new URL(request.url)
        return HttpResponse.json({ success: true, data: [card] })
      }),
    )
    await listCards({ includeArchived: true, tags: ['t1', 't2'] })
    expect(seenUrl?.searchParams.get('includeArchived')).toBe('true')
    // axios serialises arrays as tags[]=t1&tags[]=t2; assert both values are present.
    expect(seenUrl?.search).toContain('t1')
    expect(seenUrl?.search).toContain('t2')
  })

  it('fetches a single card', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/cards/k1`, () => HttpResponse.json({ success: true, data: card })),
    )
    expect((await getCard('k1')).id).toBe('k1')
  })

  it('updates a card via PUT', async () => {
    server.use(
      http.put(`${FINANCES_BASE}/cards/k1`, () =>
        HttpResponse.json({ success: true, data: { ...card, name: 'Renomeado' } }),
      ),
    )
    expect((await updateCard('k1', { name: 'Renomeado', closingDay: 5, dueDay: 12 } as never)).name).toBe('Renomeado')
  })

  it('deletes a card', async () => {
    let called = false
    server.use(
      http.delete(`${FINANCES_BASE}/cards/k1`, () => {
        called = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await deleteCard('k1')
    expect(called).toBe(true)
  })

  it('archives and unarchives a card', async () => {
    server.use(
      http.post(`${FINANCES_BASE}/cards/k1/archive`, () =>
        HttpResponse.json({ success: true, data: { ...card, archivedAt: '2026-06-13T00:00:00Z' } }),
      ),
      http.post(`${FINANCES_BASE}/cards/k1/unarchive`, () =>
        HttpResponse.json({ success: true, data: { ...card, archivedAt: null } }),
      ),
    )
    expect((await archiveCard('k1')).archivedAt).not.toBeNull()
    expect((await unarchiveCard('k1')).archivedAt).toBeNull()
  })

  it('reads installment plans', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/cards/k1/installment-plans`, () =>
        HttpResponse.json({ success: true, data: [] }),
      ),
    )
    expect(await getCardInstallmentPlans('k1')).toEqual([])
  })

  it('sets the card tags via PUT', async () => {
    let body: unknown
    server.use(
      http.put(`${FINANCES_BASE}/cards/k1/tags`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await setCardTags('k1', { tagIds: ['t1'] })
    expect(body).toEqual({ tagIds: ['t1'] })
  })
})
