import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { FeatureDto } from '../../models'
import { FeaturesPage } from './FeaturesPage'

const BASE = `${TEST_API_BASE}/api/v1.0/admin`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const features: FeatureDto[] = [
  { key: 'finances', parent: null, defaultEnabled: true, override: null, enabled: true },
  { key: 'finances.import', parent: 'finances', defaultEnabled: true, override: null, enabled: true },
]

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('FeaturesPage', () => {
  it('asks before turning a module off, naming what goes with it, then sends the change', async () => {
    let sent: unknown = null
    server.use(
      http.get(`${BASE}/features`, () => ok(features)),
      http.put(`${BASE}/features/finances`, async ({ request }) => {
        sent = await request.json()
        return ok(features)
      }),
    )
    renderWithProviders(<FeaturesPage />)

    await userEvent.click(await screen.findByRole('switch', { name: 'Finances' }))
    expect(await screen.findByText(/This also turns off: Statement imports/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Turn off' }))

    await waitFor(() => expect(sent).toEqual({ enabled: false }))
  })

  it('sends an administrator without two-step verification to turn it on', async () => {
    server.use(
      http.get(`${BASE}/features`, () =>
        HttpResponse.json(
          { success: false, errorCode: 'Admin.MfaRequired', errorMessage: 'Turn it on.', fieldErrors: null, traceId: null },
          { status: 403 },
        ),
      ),
    )
    renderWithProviders(<FeaturesPage />)

    expect(await screen.findByText('Turn on two-step verification')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Go to security' })).toBeInTheDocument()
  })
})
