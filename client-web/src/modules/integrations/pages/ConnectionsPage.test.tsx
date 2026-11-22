import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { ExternalAccount, IntegrationEvent, ProviderCatalogItem } from '../models'
import { ConnectionsPage } from './ConnectionsPage'

const BASE = `${TEST_API_BASE}/api/v1.0/integrations`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const provider = (name: string, authKind: ProviderCatalogItem['authKind'], displayName: string | null = null): ProviderCatalogItem => ({
  provider: name,
  authKind,
  displayName,
  defaultScopes: [],
  accountCount: 0,
})

const healthy: ExternalAccount = {
  id: 'a1',
  provider: 'google',
  authKind: 'oauth',
  label: 'Personal',
  displayName: 'me@mail.test',
  scopes: [],
  status: 'connected',
  lastError: null,
  connectedAt: '2026-01-01T00:00:00Z',
  lastRefreshedAt: null,
  usages: [{ module: 'nowhere', purpose: 'thing', detail: 'me@mail.test' }],
}

const revoked: ExternalAccount = { ...healthy, id: 'a2', label: 'Work', status: 'revoked', lastError: 'Access was revoked', usages: [] }

const event: IntegrationEvent = {
  id: 'e1',
  externalAccountId: 'a1',
  provider: 'google',
  eventType: 'refresh-failed',
  detail: 'Token expired',
  occurredAt: '2026-01-05T10:00:00Z',
}

const orphanEvent: IntegrationEvent = { ...event, id: 'e2', externalAccountId: 'gone', eventType: 'disconnected', detail: null }

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('ConnectionsPage', () => {
  beforeEach(() => {
    server.use(
      http.get(`${BASE}/providers`, () => ok([provider('google', 'oauth'), provider('gemini', 'api-key', 'Google Gemini')])),
      http.get(`${BASE}/accounts`, () => ok([healthy, revoked])),
      http.get(`${BASE}/events`, () => ok([event, orphanEvent])),
    )
  })

  it('shows accounts under their provider, and a provider without one on the list of others', async () => {
    renderWithProviders(<ConnectionsPage />)

    expect(await screen.findByText('Personal')).toBeInTheDocument()
    expect(screen.getByText('Other providers')).toBeInTheDocument()
    expect(screen.getByText('Google Gemini')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Connect' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Add account/ })).toBeInTheDocument()
  })

  it('offers reconnect outright only where an account needs it', async () => {
    renderWithProviders(<ConnectionsPage />)

    const buttons = await screen.findAllByRole('button', { name: 'Reconnect' })
    expect(buttons).toHaveLength(1)
    expect(screen.getByText('Access was revoked')).toBeInTheDocument()
  })

  it('keeps disconnect in the menu, and lists what uses the account before it', async () => {
    const deleted = vi.fn()
    server.use(http.delete(`${BASE}/accounts/:id`, () => (deleted(), ok(null))))
    const user = userEvent.setup()
    renderWithProviders(<ConnectionsPage />)

    expect(await screen.findByText('Personal')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Disconnect' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'More actions for Personal' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Disconnect' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/nowhere · thing · me@mail.test/)).toBeInTheDocument()
    expect(deleted).not.toHaveBeenCalled()

    await user.click(within(dialog).getByRole('button', { name: 'Disconnect' }))
    await waitFor(() => expect(deleted).toHaveBeenCalledOnce())
  })

  it('renames from the menu, starting from the current name', async () => {
    const bodies: unknown[] = []
    server.use(
      http.patch(`${BASE}/accounts/:id`, async ({ request }) => {
        bodies.push(await request.json())
        return ok(null)
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<ConnectionsPage />)

    await user.click(await screen.findByRole('button', { name: 'More actions for Personal' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Rename' }))

    const input = await screen.findByRole('textbox', { name: 'Name' })
    expect(input).toHaveValue('Personal')
    await user.clear(input)
    await user.type(input, 'Home')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(bodies).toEqual([{ label: 'Home' }]))
  })

  it('opens on the activity from the URL, naming the account while it exists', async () => {
    renderWithProviders(<ConnectionsPage />, { route: '/?tab=activity' })

    expect(await screen.findByText('Google · Personal')).toBeInTheDocument()
    expect(screen.getByText('Google')).toBeInTheDocument()
    expect(screen.getByText('Token expired')).toBeInTheDocument()
  })
})
