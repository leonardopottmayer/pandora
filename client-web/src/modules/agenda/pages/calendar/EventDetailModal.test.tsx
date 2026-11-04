import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CalendarDto, EventOccurrenceDto } from '../../models'
import { EventDetailModal } from './EventDetailModal'

const calendar: CalendarDto = { id: 'c1', name: 'Work', color: null, isDefault: true, isVisible: true, timeZone: 'UTC', origin: 'Local', archivedAt: null }
const occurrence: EventOccurrenceDto = { eventId: 'e1', calendarId: 'c1', title: 'Meeting', startsAt: '2026-11-04T12:00:00Z', originalStartsAt: '2026-11-04T12:00:00Z', endsAt: '2026-11-04T13:00:00Z', isAllDay: false, description: null, location: null, url: null, status: 'Confirmed' }
beforeAll(async () => { await i18n.changeLanguage('en') })
function mockAuxiliary() {
  server.use(
    http.get(`${AGENDA_BASE}/event/e1/alerts`, () => HttpResponse.json({ success: true, data: [] })),
    http.get(`${AGENDA_BASE}/preferences`, () => HttpResponse.json({ success: true, data: { defaultAlertOffsetMinutes: -15 } })),
  )
}
describe('EventDetailModal', () => {
  it('requires a period before saving an edited event', async () => {
    let updated = false
    mockAuxiliary()
    server.use(
      http.get(`${AGENDA_BASE}/events/e1`, () => HttpResponse.json({ success: true, data: { rrule: null } })),
      http.put(`${AGENDA_BASE}/events/e1`, () => { updated = true; return HttpResponse.json({ success: true, data: {} }) }),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<EventDetailModal open occurrence={occurrence} calendars={[calendar]} onClose={() => {}} />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit' })).toBeEnabled())
    await user.click(screen.getByRole('button', { name: 'Edit' }))
    const start = screen.getByPlaceholderText('Start date')
    const clear = start.closest('.ant-picker')!.querySelector('.ant-picker-clear')!
    await user.click(clear)
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Choose a start.')).toBeInTheDocument()
    expect(updated).toBe(false)
  })
  it('prevents choosing an incorrect scope when the event series cannot load', async () => {
    mockAuxiliary()
    server.use(http.get(`${AGENDA_BASE}/events/e1`, () => HttpResponse.json({ success: false, errorCode: 'Unavailable', errorMessage: 'Series unavailable', fieldErrors: null, traceId: null }, { status: 500 })))
    renderWithProviders(<EventDetailModal open occurrence={occurrence} calendars={[calendar]} onClose={() => {}} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Series unavailable')
    expect(screen.getByRole('button', { name: 'Edit' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Delete' })).toBeDisabled()
  })
})
