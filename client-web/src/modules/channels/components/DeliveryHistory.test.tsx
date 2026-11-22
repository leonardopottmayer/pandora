import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { NotificationHistoryItem } from '../models'
import { DeliveryHistory } from './DeliveryHistory'

const BASE = `${TEST_API_BASE}/api/v1.0/channels`
const item = (id: string, createdAt: string): NotificationHistoryItem => ({
  id, channel: 'telegram', category: null, templateKey: 'reminder', subject: `Subject ${id}`, status: 'Sent',
  attemptCount: 1, lastError: null, provider: null, correlationId: id, groupId: null, createdAt, updatedAt: null,
})

// The device sits in Tokyo; the account zone (test preferences) is São Paulo.
beforeAll(async () => {
  vi.stubEnv('TZ', 'Asia/Tokyo')
  await i18n.changeLanguage('en')
})
afterAll(() => {
  vi.unstubAllEnvs()
  vi.useRealTimers()
})

describe('DeliveryHistory', () => {
  it('says today and yesterday on the account clock, not the device one', async () => {
    // 23:00 Nov 13 in São Paulo — already 11:00 Nov 14 in Tokyo.
    vi.setSystemTime(new Date('2026-11-14T02:00:00Z'))
    server.use(http.get(`${BASE}/notifications`, () => HttpResponse.json({
      success: true,
      data: [
        item('a', '2026-11-13T20:00:00Z'), // 17:00 Nov 13 in São Paulo (05:00 Nov 14 in Tokyo)
        item('b', '2026-11-13T01:00:00Z'), // 22:00 Nov 12 in São Paulo (10:00 Nov 13 in Tokyo)
      ],
    })))

    renderWithProviders(<DeliveryHistory />)

    expect(await screen.findByText('Today 17:00')).toBeInTheDocument()
    expect(screen.getByText('Yesterday 22:00')).toBeInTheDocument()
  })
})
