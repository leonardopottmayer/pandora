import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CalendarDto, EventOccurrenceDto } from '../../models'
import { CalendarPage } from './CalendarPage'

const calendar: CalendarDto = {
  id: 'c1',
  name: 'Work',
  color: '#1677ff',
  isDefault: true,
  isVisible: true,
  timeZone: 'UTC',
  origin: 'Local',
  archivedAt: null,
}

// An occurrence on today so it lands in the current month grid.
const today = dayjs().hour(10).minute(0).second(0)
const occurrence: EventOccurrenceDto = {
  eventId: 'e1',
  calendarId: 'c1',
  originalStartsAt: today.toISOString(),
  startsAt: today.toISOString(),
  endsAt: today.add(1, 'hour').toISOString(),
  isAllDay: false,
  title: 'Standup',
  description: null,
  location: null,
  url: null,
  status: 'Confirmed',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function mockBase(occurrences: EventOccurrenceDto[]) {
  server.use(
    http.get(`${AGENDA_BASE}/calendars`, () =>
      HttpResponse.json({ success: true, data: [calendar] }),
    ),
    http.get(`${AGENDA_BASE}/events`, () =>
      HttpResponse.json({ success: true, data: occurrences }),
    ),
  )
}

describe('CalendarPage', () => {
  it('renders occurrence pills for visible calendars', async () => {
    mockBase([occurrence])
    renderWithProviders(<CalendarPage />)
    expect(await screen.findByText('Standup')).toBeInTheDocument()
  })

  it('renders the timed event in the week view', async () => {
    mockBase([occurrence])
    const user = userEvent.setup()
    renderWithProviders(<CalendarPage />)

    await screen.findByText('Standup')
    await user.click(screen.getByText('Week'))

    // The hour gutter and the titled event block are specific to the time-grid week view.
    expect(await screen.findByText('15:00')).toBeInTheDocument()
    expect(screen.getByTitle('Standup')).toBeInTheDocument()
  })

  it('opens the occurrence detail when a pill is clicked', async () => {
    mockBase([occurrence])
    server.use(
      http.get(`${AGENDA_BASE}/events/e1`, () =>
        HttpResponse.json({
          success: true,
          data: {
            id: 'e1',
            calendarId: 'c1',
            title: 'Standup',
            description: null,
            location: null,
            url: null,
            startsAt: occurrence.startsAt,
            endsAt: occurrence.endsAt,
            isAllDay: false,
            timeZone: 'UTC',
            rrule: null,
            recurrenceEndsAt: null,
            status: 'Confirmed',
          },
        }),
      ),
      http.get(`${AGENDA_BASE}/event/e1/alerts`, () =>
        HttpResponse.json({ success: true, data: [] }),
      ),
    )
    const user = userEvent.setup()
    renderWithProviders(<CalendarPage />)

    await user.click(await screen.findByText('Standup'))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Event')).toBeInTheDocument()
  })
})
