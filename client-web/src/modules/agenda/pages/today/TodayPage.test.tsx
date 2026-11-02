import { describe, it, expect, beforeAll, beforeEach } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TaskDto, TodayItemDto } from '../../models'
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

const overdueTask: TaskDto = {
  id: 'old',
  listId: 'l1',
  parentTaskId: null,
  title: 'Renew the passport',
  notes: null,
  dueAt: '2020-01-01T12:00:00Z',
  dueHasTime: false,
  priority: 'None',
  status: 'Todo',
  completedAt: null,
  timeZone: 'UTC',
  rrule: null,
  position: 0,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('TodayPage', () => {
  beforeEach(() => {
    server.use(
      http.get(`${AGENDA_BASE}/events`, () => HttpResponse.json({ success: true, data: [] })),
      http.get(`${AGENDA_BASE}/tasks`, () => HttpResponse.json({ success: true, data: [] })),
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json({ success: true, data: [] })),
    )
  })

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
    await user.click(screen.getByRole('checkbox', { name: 'Pay the bills' }))
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
    await user.click(screen.getByRole('button', { name: 'Got it' }))
    await waitFor(() => expect(acked).toBe(true))
  })

  it('offers nothing on a reminder already seen', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/today`, () =>
        HttpResponse.json({ success: true, data: [{ ...reminder, status: 'Acknowledged' }] }),
      ),
    )
    renderWithProviders(<TodayPage />)

    await screen.findByText('Call the dentist')
    expect(screen.queryByRole('button', { name: 'Got it' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Snooze' })).not.toBeInTheDocument()
  })

  it('lists open tasks from earlier days as overdue, and counts the day', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/today`, () => HttpResponse.json({ success: true, data: [task, reminder] })),
      http.get(`${AGENDA_BASE}/tasks`, () =>
        HttpResponse.json({ success: true, data: [overdueTask, { ...overdueTask, id: 'done', title: 'Old', status: 'Done' }] }),
      ),
    )
    renderWithProviders(<TodayPage />)

    expect(await screen.findByText('Overdue · 1')).toBeInTheDocument()
    expect(screen.getByText('Renew the passport')).toBeInTheDocument()
    expect(screen.queryByText('Old')).not.toBeInTheDocument()
    expect(screen.getByText('1 task · 1 reminder')).toBeInTheDocument()
  })
})
