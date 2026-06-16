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

describe('AuditPage', () => {
  it('does not query until a valid filter is provided', () => {
    // Without an entityId, no request should be made (onUnhandledRequest: error would fail the test).
    renderWithProviders(<AuditPage />)
    expect(screen.getByText(/Provide an entity type and ID/i)).toBeInTheDocument()
  })

  it('queries by entity once type + id are set, forwarding both', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/audit`, ({ request }) => {
        seenUrl = new URL(request.url)
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
    const user = userEvent.setup()
    renderWithProviders(<AuditPage />)

    // entityType starts as "account"; only the id needs to be provided.
    await user.type(screen.getByPlaceholderText(/Entity ID/i), 'a1')

    expect(await screen.findByText('account.created')).toBeInTheDocument()
    expect(seenUrl?.searchParams.get('entityType')).toBe('account')
    expect(seenUrl?.searchParams.get('entityId')).toBe('a1')
  })

  it('localizes the entity type label (pt-BR)', async () => {
    await i18n.changeLanguage('pt-BR')
    server.use(
      http.get(`${FINANCES_BASE}/audit`, () =>
        HttpResponse.json({
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
        }),
      ),
    )
    const user = userEvent.setup()
    renderWithProviders(<AuditPage />)

    await user.type(screen.getByPlaceholderText(/ID da entidade/i), 'a1')
    await screen.findByText('account.created')
    // "account" -> "Conta" (appears in the selector and in the entity type column).
    expect(screen.getAllByText('Conta').length).toBeGreaterThan(0)
    await i18n.changeLanguage('en')
  })
})
