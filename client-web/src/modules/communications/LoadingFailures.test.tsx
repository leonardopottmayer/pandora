import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { MailAccountsPage } from './pages/MailAccountsPage'
import { MailRulesPage } from './pages/MailRulesPage'
import { MailPage } from './pages/MailPage'

beforeAll(async () => { await i18n.changeLanguage('en') })
describe('Read failures', () => {
  it.each([
    ['accounts', MailAccountsPage, '/api/v1/communications/mailboxes'],
    ['rules', MailRulesPage, '/api/v1/communications/mailboxes'],
    ['mail', MailPage, '/api/v1/communications/mailboxes']
  ])('shows a failure on %s rather than an empty collection or editable defaults', async (_name, Page, path) => {
    server.use(
      http.get(`${TEST_API_BASE}${path}`, () => HttpResponse.json({ success: false, errorCode: 'Unavailable', errorMessage: 'Read unavailable', fieldErrors: null, traceId: null }, { status: 500 })),
      http.get(`${TEST_API_BASE}/api/v1/communications/messages`, () => HttpResponse.json({ success: true, data: { items: [], nextCursor: null } })),
      http.get(`${TEST_API_BASE}/api/*`, () => HttpResponse.json({ success: true, data: [] })),
    )
    renderWithProviders(<Page />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Read unavailable')
  })
})
