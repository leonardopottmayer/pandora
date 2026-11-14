import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { todayIn } from '@/lib/today'
import type { CalendarDto, EventOccurrenceDto } from '../models'
import { EventDetailModal } from '../pages/calendar/EventDetailModal'
import { fromPicker, nowPicker, toPicker } from './datetime'

// The device sits in Tokyo while the account zone (the test preferences) is São Paulo, 12 hours apart.
const SP = 'America/Sao_Paulo'
beforeAll(async () => {
  vi.stubEnv('TZ', 'Asia/Tokyo')
  await i18n.changeLanguage('en')
})
afterAll(() => {
  vi.unstubAllEnvs()
})

describe('pickers on a device in another zone', () => {
  it('really runs in Tokyo', () => {
    expect(new Date('2026-11-14T00:00:00Z').getTimezoneOffset()).toBe(-540)
  })

  it('shows a stored instant on the account clock', () => {
    expect(toPicker('2026-11-14T13:00:00Z', SP).format('YYYY-MM-DD HH:mm')).toBe('2026-11-14 10:00')
  })

  it('sends what the picker shows as the account clock, with its offset', () => {
    expect(fromPicker(dayjs('2026-11-14T10:00:00'), SP)).toBe('2026-11-14T10:00:00-03:00')
    // Late at night stays on the picked date, which the backend keeps for a date-only due.
    expect(fromPicker(dayjs('2026-11-14T23:30:00'), SP)).toBe('2026-11-14T23:30:00-03:00')
  })

  it('round-trips an instant unchanged', () => {
    const iso = '2026-03-08T05:30:00Z'
    expect(new Date(fromPicker(toPicker(iso, SP), SP)).toISOString()).toBe('2026-03-08T05:30:00.000Z')
  })

  it('takes now and today from the account zone', () => {
    // 01:00 Nov 15 in Tokyo is still 13:00 Nov 14 in São Paulo.
    vi.setSystemTime(new Date('2026-11-14T16:00:00Z'))
    try {
      expect(nowPicker(SP).format('YYYY-MM-DD HH:mm')).toBe('2026-11-14 13:00')
      expect(todayIn(SP).format('YYYY-MM-DD')).toBe('2026-11-14')
    } finally {
      vi.useRealTimers()
    }
  })
})

const calendar: CalendarDto = { id: 'c1', name: 'Work', color: null, isDefault: true, isVisible: true, timeZone: SP, origin: 'Local', archivedAt: null, isReadOnly: false }
// 12:00 UTC: 09:00 in São Paulo, 21:00 in Tokyo.
const occurrence: EventOccurrenceDto = { eventId: 'e1', calendarId: 'c1', title: 'Meeting', startsAt: '2026-11-04T12:00:00Z', originalStartsAt: '2026-11-04T12:00:00Z', endsAt: '2026-11-04T13:00:00Z', isAllDay: false, description: null, location: null, url: null, status: 'Confirmed' }

describe('EventDetailModal on a device in another zone', () => {
  it('edits on the account clock and saves that clock back', async () => {
    let body: Record<string, unknown> | undefined
    server.use(
      http.get(`${AGENDA_BASE}/event/e1/alerts`, () => HttpResponse.json({ success: true, data: [] })),
      http.get(`${AGENDA_BASE}/preferences`, () => HttpResponse.json({ success: true, data: { defaultAlertOffsetMinutes: -15 } })),
      http.get(`${AGENDA_BASE}/events/e1`, () => HttpResponse.json({ success: true, data: { rrule: null } })),
      http.patch(`${AGENDA_BASE}/events/e1`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ success: true, data: {} })
      }),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<EventDetailModal open occurrence={occurrence} calendars={[calendar]} onClose={() => {}} />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit' })).toBeEnabled())
    await user.click(screen.getByRole('button', { name: 'Edit' }))

    expect(screen.getByPlaceholderText('Start date')).toHaveValue('04/11/2026 09:00')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(body).toBeDefined())
    expect(body!.startsAt).toBe('2026-11-04T09:00:00-03:00')
    expect(body!.endsAt).toBe('2026-11-04T10:00:00-03:00')
  })
})
