import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { ConnectionsPage } from './pages/ConnectionsPage'
import { IntegrationActivity } from './components/IntegrationActivity'

beforeAll(async () => { await i18n.changeLanguage('en') })
describe('Read failures', () => {
  it.each([
    ['connections', ConnectionsPage, '/api/v1.0/integrations/accounts'],
    ['activity', IntegrationActivity, '/api/v1.0/integrations/events']
  ])('shows a failure on %s rather than an empty collection or editable defaults', async (_name, Page, path) => {
    server.use(
      http.get(`${TEST_API_BASE}${path}`, () => HttpResponse.json({ success: false, errorCode: 'Unavailable', errorMessage: 'Read unavailable', fieldErrors: null, traceId: null }, { status: 500 })),
      http.get(`${TEST_API_BASE}/api/*`, () => HttpResponse.json({ success: true, data: [] })),
    )
    renderWithProviders(<Page />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Read unavailable')
  })
})
