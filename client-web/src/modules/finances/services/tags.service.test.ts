import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { createTag, deleteTag, linkTag, listTags, unlinkTag } from './tags.service'

describe('tags.service', () => {
  it('lists tags', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/tags`, () =>
        HttpResponse.json({ success: true, data: [{ id: 't1', name: 'Viagem', color: '#f00' }] }),
      ),
    )
    const tags = await listTags()
    expect(tags[0].name).toBe('Viagem')
  })

  it('creates a tag', async () => {
    let body: unknown
    server.use(
      http.post(`${FINANCES_BASE}/tags`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { id: 't2', name: 'Casa', color: null } })
      }),
    )
    const created = await createTag({ name: 'Casa', color: null })
    expect(created.id).toBe('t2')
    expect(body).toMatchObject({ name: 'Casa' })
  })

  it('deletes a tag', async () => {
    let called = false
    server.use(
      http.delete(`${FINANCES_BASE}/tags/t1`, () => {
        called = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await deleteTag('t1')
    expect(called).toBe(true)
  })

  it('links a tag to an entity', async () => {
    let body: unknown
    server.use(
      http.post(`${FINANCES_BASE}/tags/t1/links`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({
          success: true,
          data: { id: 'l1', tagId: 't1', entityType: 'transaction', entityId: 'x1' },
        })
      }),
    )
    const link = await linkTag('t1', { entityType: 'transaction', entityId: 'x1' })
    expect(link.entityId).toBe('x1')
    expect(body).toMatchObject({ entityType: 'transaction', entityId: 'x1' })
  })

  it('unlinks a tag using the typed path', async () => {
    let path = ''
    server.use(
      http.delete(`${FINANCES_BASE}/tags/t1/links/transaction/x1`, ({ request }) => {
        path = new URL(request.url).pathname
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await unlinkTag('t1', 'transaction', 'x1')
    expect(path).toContain('/links/transaction/x1')
  })
})
