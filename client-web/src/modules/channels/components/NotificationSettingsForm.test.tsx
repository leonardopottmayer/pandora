import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { NotificationSettingsForm } from './NotificationSettingsForm'

const BASE = `${TEST_API_BASE}/api/v1.0/channels`
beforeAll(async () => { await i18n.changeLanguage('en') })
describe('Notification settings read failures', () => {
  it.each(['', '/preferences', '/notification-settings'])('shows failure reading %s', async (path) => {
    server.use(
      http.get(`${BASE}${path}`, () => HttpResponse.json({ success: false, errorCode: 'Unavailable', errorMessage: 'Notification settings unavailable', fieldErrors: null, traceId: null }, { status: 500 })),
      http.get(BASE, () => HttpResponse.json({ success: true, data: [] })),
      http.get(`${BASE}/preferences`, () => HttpResponse.json({ success: true, data: [] })),
      http.get(`${BASE}/notification-settings`, () => HttpResponse.json({ success: true, data: { quietHoursEnabled: false, quietHoursStart: null, quietHoursEnd: null, quietHoursBehaviour: null } })),
    )
    renderWithProviders(<NotificationSettingsForm />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Notification settings unavailable')
    if (path === '/preferences') expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})
