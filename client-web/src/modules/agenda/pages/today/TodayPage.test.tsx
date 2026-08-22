import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TodayItemDto } from '../../models'
import { TodayPage } from './TodayPage'

const task: TodayItemDto = {
  kind: 'task',
  id: 't1',
  title: 'Pay the bills',
  notes: null,
  at: '2026-08-21T12:00:00Z',
  endsAt: null,
  isAllDay: false,
  calendarId: null,
  status: 'Todo',
}

const reminder: TodayItemDto = {
  kind: 'reminder',
  id: 'r1',
  title: 'Call the dentist',
  notes: null,
  at: '2026-08-21T15:00:00Z',
  endsAt: null,
  isAllDay: false,
  calendarId: null,
  status: 'Scheduled',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('TodayPage', () => {
  it('renders the day items', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/today`, () =>
        HttpResponse.json({ success: true, data: [task, reminder] }),
      ),
    )
    renderWithProviders(<TodayPage />)
    expect(await screen.findByText('Pay the bills')).toBeInTheDocument()
    expect(screen.getByText('Call the dentist')).toBeInTheDocument()
  })

  it('shows an empty state when there is nothing today', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/today`, () => HttpResponse.json({ success: true, data: [] })),
    )
    renderWithProviders(<TodayPage />)
    expect(await screen.findByText('Nothing scheduled for today.')).toBeInTheDocument()
  })

  it('completes a task', async () => {
    let completed = false
    server.use(
      http.get(`${AGENDA_BASE}/today`, () => HttpResponse.json({ success: true, data: [task] })),
      http.post(`${AGENDA_BASE}/tasks/t1/complete`, () => {
        completed = true
        return HttpResponse.json({ success: true, data: { ...task, status: 'Done' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TodayPage />)

    await screen.findByText('Pay the bills')
    await user.click(screen.getByRole('button', { name: /Complete/ }))
    await waitFor(() => expect(completed).toBe(true))
  })

  it('acknowledges a reminder', async () => {
    let acked = false
    server.use(
      http.get(`${AGENDA_BASE}/today`, () =>
        HttpResponse.json({ success: true, data: [reminder] }),
      ),
      http.post(`${AGENDA_BASE}/reminders/r1/acknowledge`, () => {
        acked = true
        return HttpResponse.json({ success: true, data: { ...reminder, status: 'Acknowledged' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TodayPage />)

    await screen.findByText('Call the dentist')
    await user.click(screen.getByRole('button', { name: 'Acknowledge' }))
    await waitFor(() => expect(acked).toBe(true))
  })
})
