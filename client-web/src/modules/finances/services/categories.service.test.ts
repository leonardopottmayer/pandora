import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import {
  createUserCategory,
  listSystemCategories,
  listUserCategories,
  setUserCategoryActive,
} from './categories.service'

describe('categories.service', () => {
  it('lists system categories forwarding nature', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/categories/system`, ({ request }) => {
        seenUrl = new URL(request.url)
        return HttpResponse.json({
          success: true,
          data: [
            { id: 's1', code: 'food', name: 'Food', nature: 'expense', color: null, icon: null, displayOrder: 0, isOther: false, isActive: true, children: [] },
          ],
        })
      }),
    )
    const result = await listSystemCategories({ nature: 'expense' })
    expect(result[0].code).toBe('food')
    expect(seenUrl?.searchParams.get('nature')).toBe('expense')
  })

  it('lists user categories', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/categories`, () => HttpResponse.json({ success: true, data: [] })),
    )
    expect(await listUserCategories()).toEqual([])
  })

  it('creates a user category', async () => {
    let body: unknown
    server.use(
      http.post(`${FINANCES_BASE}/categories`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({
          success: true,
          data: { id: 'c1', name: 'Mercado', nature: 'expense', parentCategoryId: null, color: null, icon: null, displayOrder: 0, isActive: true, children: [] },
        })
      }),
    )
    const created = await createUserCategory({ name: 'Mercado', nature: 'expense', displayOrder: 0 })
    expect(created.id).toBe('c1')
    expect(body).toMatchObject({ name: 'Mercado', nature: 'expense' })
  })

  it('deactivates a user category via the deactivate action', async () => {
    let path = ''
    server.use(
      http.post(`${FINANCES_BASE}/categories/c1/deactivate`, ({ request }) => {
        path = new URL(request.url).pathname
        return HttpResponse.json({
          success: true,
          data: { id: 'c1', name: 'Mercado', nature: 'expense', parentCategoryId: null, color: null, icon: null, displayOrder: 0, isActive: false, children: [] },
        })
      }),
    )
    const result = await setUserCategoryActive('c1', false)
    expect(result.isActive).toBe(false)
    expect(path).toContain('/deactivate')
  })
})
