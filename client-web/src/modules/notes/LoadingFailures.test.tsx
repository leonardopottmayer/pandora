import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { SpacesPage } from './pages/SpacesPage'
import { LibraryPage } from './pages/LibraryPage'

beforeAll(async () => { await i18n.changeLanguage('en') })
describe('Read failures', () => {
  it.each([
    ['spaces', SpacesPage, '/api/v1/notes/spaces'],
    ['library', LibraryPage, '/api/v1/notes/media']
  ])('shows a failure on %s rather than an empty collection or editable defaults', async (_name, Page, path) => {
    server.use(
      http.get(`${TEST_API_BASE}${path}`, () => HttpResponse.json({ success: false, errorCode: 'Unavailable', errorMessage: 'Read unavailable', fieldErrors: null, traceId: null }, { status: 500 })),
      http.get(`${TEST_API_BASE}/api/*`, () => HttpResponse.json({ success: true, data: [] })),
    )
    renderWithProviders(<Page />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Read unavailable')
  })
})
