import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { ReminderDto } from '../../models'
import { RemindersListPage } from './RemindersListPage'

const reminder: ReminderDto = {
  id: 'r1',
  title: 'Call the dentist',
  notes: null,
  remindAt: '2026-08-21T15:00:00Z',
  timeZone: 'America/Sao_Paulo',
  rrule: null,
  recurrenceEndsAt: null,
  status: 'Scheduled',
  snoozedUntil: null,
  acknowledgedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('RemindersListPage', () => {
  it('renders reminders returned by the API', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/reminders`, () =>
        HttpResponse.json({ success: true, data: [reminder] }),
      ),
    )
    renderWithProviders(<RemindersListPage />)
    expect(await screen.findByText('Call the dentist')).toBeInTheDocument()
  })

  it('opens the create modal', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/reminders`, () => HttpResponse.json({ success: true, data: [] })),
    )
    const user = userEvent.setup()
    renderWithProviders(<RemindersListPage />)

    await user.click(await screen.findByRole('button', { name: /New reminder/ }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('New reminder')).toBeInTheDocument()
  })

  it('acknowledges a reminder', async () => {
    let acked = false
    server.use(
      http.get(`${AGENDA_BASE}/reminders`, () =>
        HttpResponse.json({ success: true, data: [reminder] }),
      ),
      http.post(`${AGENDA_BASE}/reminders/r1/acknowledge`, () => {
        acked = true
        return HttpResponse.json({ success: true, data: { ...reminder, status: 'Acknowledged' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<RemindersListPage />)

    await screen.findByText('Call the dentist')
    await user.click(screen.getByRole('button', { name: 'Acknowledge' }))
    await waitFor(() => expect(acked).toBe(true))
  })
})
