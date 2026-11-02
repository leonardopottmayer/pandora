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
        HttpResponse.json({ success: true, data: [{ ...reminder, status: 'Notified' }] }),
      ),
      http.post(`${AGENDA_BASE}/reminders/r1/acknowledge`, () => {
        acked = true
        return HttpResponse.json({ success: true, data: { ...reminder, status: 'Acknowledged' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<RemindersListPage />)

    await screen.findByText('Call the dentist')
    await user.click(screen.getByRole('button', { name: 'Got it' }))
    await waitFor(() => expect(acked).toBe(true))
  })

  it('renames a reminder from its menu', async () => {
    let body: unknown
    server.use(
      http.get(`${AGENDA_BASE}/reminders`, () =>
        HttpResponse.json({ success: true, data: [reminder] }),
      ),
      http.patch(`${AGENDA_BASE}/reminders/r1`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: true })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<RemindersListPage />)

    await screen.findByText('Call the dentist')
    await user.click(screen.getByRole('button', { name: 'More actions for Call the dentist' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Rename' }))
    const input = await screen.findByRole('textbox', { name: 'Title' })
    expect(input).toHaveValue('Call the dentist')
    await user.clear(input)
    await user.type(input, 'Call the vet{Enter}')
    await waitFor(() => expect(body).toEqual({ title: 'Call the vet' }))
  })

  it('puts fired reminders first and closed ones away', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/reminders`, () =>
        HttpResponse.json({
          success: true,
          data: [
            { ...reminder, id: 'r2', title: 'Later one', remindAt: '2026-09-01T15:00:00Z' },
            { ...reminder, id: 'r3', title: 'Fired one', status: 'Notified' },
            { ...reminder, id: 'r4', title: 'Gone one', status: 'Cancelled' },
          ],
        }),
      ),
    )
    renderWithProviders(<RemindersListPage />)

    expect(await screen.findByText('Waiting for you · 1')).toBeInTheDocument()
    expect(screen.getByText('Upcoming · 1')).toBeInTheDocument()
    expect(screen.getByText('Done and cancelled · 1')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'More actions for Gone one' })).not.toBeInTheDocument()
  })

  it('cancels with a dialog that says what each button does', async () => {
    let cancelled = false
    server.use(
      http.get(`${AGENDA_BASE}/reminders`, () => HttpResponse.json({ success: true, data: [reminder] })),
      http.delete(`${AGENDA_BASE}/reminders/r1`, () => {
        cancelled = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<RemindersListPage />)

    await user.click(await screen.findByRole('button', { name: 'More actions for Call the dentist' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Cancel reminder' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('button', { name: 'Keep' })).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Cancel reminder' }))
    await waitFor(() => expect(cancelled).toBe(true))
  })
})
