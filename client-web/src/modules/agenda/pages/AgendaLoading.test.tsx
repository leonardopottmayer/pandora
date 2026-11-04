import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { TodayPage } from './today/TodayPage'
import { CalendarPage } from './calendar/CalendarPage'
import { TasksListPage } from './tasks/TasksListPage'
import { RemindersListPage } from './reminders/RemindersListPage'
import { AgendaSettingsPage } from './settings/AgendaSettingsPage'

beforeAll(async () => { await i18n.changeLanguage('en') })

describe('Agenda loading failures', () => {
  it.each([
    ['today', TodayPage, 'today'],
    ['calendar', CalendarPage, 'events'],
    ['tasks', TasksListPage, 'tasks'],
    ['reminders', RemindersListPage, 'reminders'],
    ['settings', AgendaSettingsPage, 'preferences'],
  ])('shows a request failure on %s instead of an empty result', async (_name, Page, path) => {
    server.use(
      http.get(`${AGENDA_BASE}/${path}`, () => HttpResponse.json({ success: false, errorCode: 'Unavailable', errorMessage: 'Service unavailable', fieldErrors: null, traceId: null }, { status: 500 })),
      http.get(`${AGENDA_BASE}/*`, () => HttpResponse.json({ success: true, data: [] })),
    )
    renderWithProviders(<Page />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Service unavailable')
  })
})
