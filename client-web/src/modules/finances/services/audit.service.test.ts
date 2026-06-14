import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { listAuditTimeline } from './audit.service'

describe('audit.service', () => {
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
