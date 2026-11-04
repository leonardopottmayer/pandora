import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { PreferencesContext, type PreferencesContextValue } from '@/modules/identity/context/preferences-context'
import { renderWithProviders } from '@/test/utils'
import type { CalendarDto } from '../../models'
import { AgendaSettingsPage } from './AgendaSettingsPage'

function calendar(overrides: Partial<CalendarDto>): CalendarDto {
  return {
    id: 'c1',
    name: 'Work',
    color: '#1677ff',
    isDefault: false,
    isVisible: true,
    timeZone: 'UTC',
    origin: 'Local',
    archivedAt: null,
    ...overrides,
  }
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('AgendaSettingsPage', () => {
  it('shows the current default calendar and the scheduling defaults', async () => {
    server.use(http.get(`${AGENDA_BASE}/preferences`, () => HttpResponse.json({ success: true, data: { defaultAlertOffsetMinutes: -15 } })))
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () =>
        HttpResponse.json({
          success: true,
          data: [calendar({ id: 'c1', name: 'Work', isDefault: true }), calendar({ id: 'c2', name: 'Personal' })],
        }),
      ),
    )
    renderWithProviders(<AgendaSettingsPage />)

    expect(await screen.findByText('Work')).toBeInTheDocument()
    // The scheduling defaults sourced from Identity preferences are on the same screen.
    expect(screen.getByText('Time zone')).toBeInTheDocument()
    expect(screen.getByText('Week starts on')).toBeInTheDocument()
    expect(screen.getByText('Default alert')).toBeInTheDocument()
  })

  it('edits the week start here, and leaves the time zone to Preferences', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json({ success: true, data: [] })),
      http.get(`${AGENDA_BASE}/preferences`, () => HttpResponse.json({ success: true, data: { defaultAlertOffsetMinutes: -15 } })),
    )
    const setWeekStartsOn = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(
      <PreferencesContext.Provider
        value={{ timeZone: 'UTC', weekStartsOn: 'sunday', setWeekStartsOn } as unknown as PreferencesContextValue}
      >
        <AgendaSettingsPage />
      </PreferencesContext.Provider>,
    )

    await user.click(await screen.findByText('Monday'))

    await waitFor(() => expect(setWeekStartsOn).toHaveBeenCalledWith('monday'))
    expect(screen.getByRole('link', { name: 'Edit in Preferences' })).toHaveAttribute('href', '/settings')
  })

  it("shows the default alert from the Agenda's own preferences, and saves it there", async () => {
    const saved: unknown[] = []
    server.use(
      http.get(`${AGENDA_BASE}/calendars`, () => HttpResponse.json({ success: true, data: [] })),
      http.get(`${AGENDA_BASE}/preferences`, () => HttpResponse.json({ success: true, data: { defaultAlertOffsetMinutes: -30 } })),
      http.put(`${AGENDA_BASE}/preferences`, async ({ request }) => {
        const body = await request.json()
        saved.push(body)
        return HttpResponse.json({ success: true, data: body })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AgendaSettingsPage />)

    await user.click(await screen.findByText('30 minutes before'))
    await user.click(await screen.findByText('5 minutes before'))

    await waitFor(() => expect(saved).toEqual([{ defaultAlertOffsetMinutes: -5 }]))
  })
})
