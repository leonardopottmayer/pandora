import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { PreferencesContext, type PreferencesContextValue } from '../context/preferences-context'
import { ForgotPasswordPage } from './ForgotPasswordPage'

const AUTH = `${TEST_API_BASE}/api/v1/identity/auth`

const prefs: PreferencesContextValue = {
  theme: 'light',
  setTheme: () => {},
  isDark: false,
  language: 'en',
  setLanguage: () => {},
}

function renderPage() {
  return renderWithProviders(
    <PreferencesContext.Provider value={prefs}>
      <ForgotPasswordPage />
    </PreferencesContext.Provider>,
  )
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('ForgotPasswordPage', () => {
  it('sends a reset link for a valid email and shows the success result', async () => {
    let sentEmail: string | undefined
    server.use(
      http.post(`${AUTH}/password/forgot`, async ({ request }) => {
        sentEmail = ((await request.json()) as { email: string }).email
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByRole('textbox'), 'leo@example.com')
    await user.click(screen.getByRole('button', { name: /Send|Reset|Recover/i }))

    await waitFor(() => expect(sentEmail).toBe('leo@example.com'))
    // The success Result replaces the form.
    expect(await screen.findByText(i18n.t('forgot.sentTitle'))).toBeInTheDocument()
  })

  it('rejects an invalid email without calling the API', async () => {
    let posted = false
    server.use(
      http.post(`${AUTH}/password/forgot`, () => {
        posted = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByRole('textbox'), 'not-an-email')
    await user.click(screen.getByRole('button', { name: /Send|Reset|Recover/i }))

    expect(await screen.findByText(i18n.t('forgot.emailInvalid'))).toBeInTheDocument()
    expect(posted).toBe(false)
  })
})
