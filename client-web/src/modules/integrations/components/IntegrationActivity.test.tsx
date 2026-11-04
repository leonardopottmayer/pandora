import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { PreferencesContext, type PreferencesContextValue } from '@/modules/identity/context/preferences-context'
import { IntegrationActivity } from './IntegrationActivity'

beforeAll(async () => { await i18n.changeLanguage('en') })
describe('IntegrationActivity', () => {
  it('keeps event metadata on a phone and uses the application time zone', async () => {
    const BASE = `${TEST_API_BASE}/api/v1.0/integrations`
    server.use(
      http.get(`${BASE}/events`, () => HttpResponse.json({ success: true, data: [{ id: 'e1', externalAccountId: 'a1', provider: 'google', eventType: 'refresh-failed', detail: 'Token expired', occurredAt: '2026-01-05T10:00:00Z' }] })),
      http.get(`${BASE}/accounts`, () => HttpResponse.json({ success: true, data: [{ id: 'a1', label: 'Personal' }] })),
    )
    renderWithProviders(<PreferencesContext.Provider value={{ timeZone: 'Asia/Tokyo' } as PreferencesContextValue}><IntegrationActivity /></PreferencesContext.Provider>)
    expect(await screen.findByText('Token expired')).toBeInTheDocument()
    expect(screen.getByText('Google · Personal')).toBeInTheDocument()
    expect(screen.getByText(/7:00 PM/)).toBeInTheDocument()
    expect(screen.getAllByRole('columnheader')).toHaveLength(1)
  })
})
