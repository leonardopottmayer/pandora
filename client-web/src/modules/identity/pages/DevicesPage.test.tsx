import { describe, it, expect, beforeAll, afterEach, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { PandoraDesktop } from '@/lib/desktop'
import type { Device } from '../models'
import { DevicesPage } from './DevicesPage'

const DEVICES = `${TEST_API_BASE}/api/v1/identity/devices`

const homelab: Device = {
  id: 'd1',
  name: 'HOMELAB',
  platform: 'windows',
  form: 'desktop',
  scopes: ['files.agent'],
  createdAt: '2026-10-06T12:00:00Z',
  lastSeenAt: null,
}

function fakeDesktop(deviceId: string | null) {
  const invoke = vi.fn(async (method: string) => {
    if (method === 'desktop.getSettings')
      return { version: '0.1.0', serverUrl: 'http://homelab/', autostart: false, machineName: 'NOTEBOOK', platform: 'windows', form: 'desktop', deviceId }
    return true
  })
  window.pandoraDesktop = { version: '0.1.0', capabilities: async () => [], invoke: invoke as PandoraDesktop['invoke'], on: () => () => {} }
  return invoke
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

afterEach(() => {
  delete window.pandoraDesktop
})

describe('DevicesPage', () => {
  it('lists the devices and revokes one, without a "this computer" section in a browser', async () => {
    let deleted: string | undefined
    server.use(
      http.get(DEVICES, () => HttpResponse.json({ success: true, data: [homelab] })),
      http.delete(`${DEVICES}/:id`, ({ params }) => {
        deleted = params.id as string
        return HttpResponse.json({ success: true, data: true })
      }),
    )
    renderWithProviders(<DevicesPage />)

    expect(await screen.findByText('HOMELAB')).toBeInTheDocument()
    expect(screen.getByText(/files\.agent/)).toBeInTheDocument()
    expect(screen.queryByText('This computer')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Revoke' }))
    // The confirmation's OK button carries the same label; it is the last one rendered.
    await waitFor(() => expect(screen.getAllByRole('button', { name: 'Revoke' })).toHaveLength(2))
    await userEvent.click(screen.getAllByRole('button', { name: 'Revoke' })[1])

    await waitFor(() => expect(deleted).toBe('d1'))
  })

  it('inside the app, pairs this computer and hands the key to the shell', async () => {
    let body: unknown
    server.use(
      http.get(DEVICES, () => HttpResponse.json({ success: true, data: [] })),
      http.post(DEVICES, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { device: { ...homelab, id: 'd9', name: 'NOTEBOOK', scopes: [] }, key: 'pdk_new' } })
      }),
    )
    const invoke = fakeDesktop(null)
    renderWithProviders(<DevicesPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Connect this computer' }))

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('desktop.storeCredential', { deviceId: 'd9', key: 'pdk_new' }))
    expect(body).toEqual({ name: 'NOTEBOOK', platform: 'windows', form: 'desktop', scopes: [] })
  })
})
