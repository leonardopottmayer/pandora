import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { AuditPage } from './AuditPage'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

// The entity-id field is a Select populated from the user's entities; EntityIdSelect loads several
// of them up-front. Stub every endpoint it touches (accounts carries the option we pick) plus /audit.
function stubEntityEndpoints(onAudit: (url: URL) => void) {
  const empty = () => HttpResponse.json({ success: true, data: [] })
  server.use(
    http.get(`${FINANCES_BASE}/accounts`, () =>
      HttpResponse.json({ success: true, data: [{ id: 'a1', name: 'Checking', currency: 'BRL' }] }),
    ),
    http.get(`${FINANCES_BASE}/cards`, empty),
    http.get(`${FINANCES_BASE}/tags`, empty),
    http.get(`${FINANCES_BASE}/categories`, empty),
    http.get(`${FINANCES_BASE}/transactions`, empty),
    http.get(`${FINANCES_BASE}/recurring-transactions`, empty),
    http.get(`${FINANCES_BASE}/pending-transactions`, empty),
    http.get(`${FINANCES_BASE}/audit`, ({ request }) => {
      onAudit(new URL(request.url))
      return HttpResponse.json({
        success: true,
        data: [
          {
            id: 'e1',
            actorUserId: 'u1',
            entityType: 'account',
            entityId: 'a1',
            eventType: 'account.created',
            data: null,
            correlationId: null,
            occurredAt: '2026-06-13T10:00:00Z',
          },
        ],
      })
    }),
  )
}

// Picks the 'Checking' account in the entity-id Select (the second combobox after the type Select).
async function selectCheckingAccount(user: ReturnType<typeof userEvent.setup>) {
  const comboboxes = screen.getAllByRole('combobox')
  await user.click(comboboxes[comboboxes.length - 1])
  await user.click(await screen.findByText('Checking (BRL)'))
}

describe('AuditPage', () => {
  it('does not query until a valid filter is provided', () => {
    // Without an entityId, no request should be made (onUnhandledRequest: error would fail the test).
    renderWithProviders(<AuditPage />)
    expect(screen.getByText(/Provide an entity type and ID/i)).toBeInTheDocument()
  })

  it('queries by entity once type + id are set, forwarding both', async () => {
    let seenUrl: URL | undefined
    stubEntityEndpoints((url) => (seenUrl = url))
    const user = userEvent.setup()
    renderWithProviders(<AuditPage />)

    // entityType starts as "account"; pick the account whose id is the entity id to query.
    await selectCheckingAccount(user)

    expect(await screen.findByText('account.created')).toBeInTheDocument()
    expect(seenUrl?.searchParams.get('entityType')).toBe('account')
    expect(seenUrl?.searchParams.get('entityId')).toBe('a1')
  })

  it('localizes the entity type label (pt-BR)', async () => {
    await i18n.changeLanguage('pt-BR')
    stubEntityEndpoints(() => {})
    const user = userEvent.setup()
    renderWithProviders(<AuditPage />)

    await selectCheckingAccount(user)
    await screen.findByText('account.created')
    // "account" -> "Conta" (appears in the type selector and in the entity type column).
    expect(screen.getAllByText('Conta').length).toBeGreaterThan(0)
    await i18n.changeLanguage('en')
  })
})
