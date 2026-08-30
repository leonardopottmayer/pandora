import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
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
  })
})
