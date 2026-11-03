import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { listAllAuditTimeline, listAuditTimeline } from './audit.service'
import { PAGE_SIZE } from './transactions.service'

describe('audit.service', () => {
  it('loads every page for the same filter, including the events beyond the first 200', async () => {
    const events = Array.from({ length: PAGE_SIZE + 3 }, (_, i) => ({ id: `event-${i}` }))
    const skips: string[] = []
    server.use(http.get(`${FINANCES_BASE}/audit`, ({ request }) => {
      const query = new URL(request.url).searchParams
      expect(query.get('correlationId')).toBe('correlation-1')
      expect(query.get('take')).toBe(String(PAGE_SIZE))
      skips.push(query.get('skip')!)
      const skip = Number(query.get('skip'))
      return HttpResponse.json({ success: true, data: events.slice(skip, skip + PAGE_SIZE) })
    }))
    const result = await listAllAuditTimeline({ correlationId: 'correlation-1' })
    expect(skips).toEqual(['0', String(PAGE_SIZE)])
    expect(result).toEqual(events)
  })

  it('rejects rather than returning a partial history when a later page fails', async () => {
    server.use(http.get(`${FINANCES_BASE}/audit`, ({ request }) =>
      Number(new URL(request.url).searchParams.get('skip')) === 0
        ? HttpResponse.json({ success: true, data: Array.from({ length: PAGE_SIZE }, (_, i) => ({ id: String(i) })) })
        : new HttpResponse(null, { status: 500 }),
    ))
    await expect(listAllAuditTimeline({ entityType: 'account', entityId: 'a1' })).rejects.toBeDefined()
  })
  it('lists audit events forwarding filters', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/audit`, ({ request }) => {
        seenUrl = new URL(request.url)
        return HttpResponse.json({
          success: true,
          data: [
            {
              id: 'e1',
              actorUserId: 'u1',
              entityType: 'account',
              entityId: 'a1',
              eventType: 'account.created',
              data: '{"name":"Conta"}',
              correlationId: null,
              occurredAt: '2026-06-13T10:00:00Z',
            },
          ],
        })
      }),
    )
    const events = await listAuditTimeline({ entityType: 'account', take: 50 })
    expect(events[0].eventType).toBe('account.created')
    expect(seenUrl?.searchParams.get('entityType')).toBe('account')
    expect(seenUrl?.searchParams.get('take')).toBe('50')
  })
})
