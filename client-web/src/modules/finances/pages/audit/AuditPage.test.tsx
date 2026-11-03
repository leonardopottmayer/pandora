import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { AuditPage } from './AuditPage'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

// Stub the selectable records and the audit query; only the chosen type should load.
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
    stubEntityEndpoints(() => { throw new Error('Audit must not be requested without a filter') })
    // Without an entityId, no request should be made (onUnhandledRequest: error would fail the test).
    renderWithProviders(<AuditPage />)
    expect(screen.getByText(/Select a record, or enter a correlation ID/i)).toBeInTheDocument()
  })

  it('queries by entity once type + id are set, forwarding both', async () => {
    let seenUrl: URL | undefined
    stubEntityEndpoints((url) => (seenUrl = url))
    const user = userEvent.setup()
    renderWithProviders(<AuditPage />)

    // entityType starts as "account"; pick the account whose id is the entity id to query.
    await selectCheckingAccount(user)

    expect(await screen.findByText('Account created')).toBeInTheDocument()
    expect(seenUrl?.searchParams.get('entityType')).toBe('account')
    expect(seenUrl?.searchParams.get('entityId')).toBe('a1')
  })

  it('localizes the entity type label (pt-BR)', async () => {
    await i18n.changeLanguage('pt-BR')
    stubEntityEndpoints(() => {})
    const user = userEvent.setup()
    renderWithProviders(<AuditPage />)

    await selectCheckingAccount(user)
    await screen.findByText('Conta criada')
    // "account" -> "Conta" (appears in the type selector and in the entity type column).
    expect(screen.getAllByText('Conta').length).toBeGreaterThan(0)
    await i18n.changeLanguage('en')
  })

  it('opens the selected event in a drawer with its original code and identifiers', async () => {
    stubEntityEndpoints(() => {})
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<AuditPage />)
    await selectCheckingAccount(user)
    await user.click(await screen.findByRole('button', { name: 'Account created' }))
    expect(await screen.findByText('Event details')).toBeInTheDocument()
    expect(screen.getByText('account.created')).toBeInTheDocument()
    expect(within(screen.getByRole('dialog')).getByText('a1')).toBeInTheDocument()
    expect(screen.getByText('u1')).toBeInTheDocument()
    expect(screen.getByText('No additional data was recorded.')).toBeInTheDocument()
  })

  it('queries by correlation alone, preserving the entity selection when switching modes', async () => {
    let seenUrl: URL | undefined
    stubEntityEndpoints((url) => { seenUrl = url })
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<AuditPage />)
    await selectCheckingAccount(user)
    await screen.findByText('Account created')
    await user.click(screen.getByText('By correlation'))
    const correlationId = '0193f000-0000-7000-8000-00000000a001'
    await user.click(screen.getByRole('textbox', { name: 'Correlation ID' }))
    await user.paste(correlationId)
    await waitFor(() => expect(seenUrl?.searchParams.get('correlationId')).toBe(correlationId))
    expect(seenUrl?.searchParams.has('entityId')).toBe(false)
    await user.click(screen.getByText('By entity'))
    expect(screen.getByText('Checking (BRL)', { selector: '.ant-select-content' })).toBeInTheDocument()
  })

  it('fetches only the options for the selected record type', async () => {
    let accounts = 0
    let cards = 0
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () => { accounts++; return HttpResponse.json({ success: true, data: [] }) }),
      http.get(`${FINANCES_BASE}/cards`, () => { cards++; return HttpResponse.json({ success: true, data: [] }) }),
    )
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<AuditPage />)
    await waitFor(() => expect(accounts).toBe(1))
    expect(cards).toBe(0)
    await user.click(screen.getByRole('combobox', { name: 'Entity type' }))
    await user.click(screen.getByText('Card', { selector: '.ant-select-item-option-content' }))
    await waitFor(() => expect(cards).toBe(1))
    expect(accounts).toBe(1)
  })

  it('loads statements only after choosing a card and clears the statement when the card changes', async () => {
    let statementsRequested = 0
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [
        { id: 'c1', name: 'First card' }, { id: 'c2', name: 'Second card' },
      ] })),
      http.get(`${FINANCES_BASE}/cards/:id/statements`, () => {
        statementsRequested++
        return HttpResponse.json({ success: true, data: [{ id: 's1', referenceMonth: '2026-06' }] })
      }),
    )
    server.use(http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] })))
    server.use(http.get(`${FINANCES_BASE}/audit`, ({ request }) => {
      seenUrl = new URL(request.url)
      return HttpResponse.json({ success: true, data: [] })
    }))
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<AuditPage />)
    await user.click(screen.getByRole('combobox', { name: 'Entity type' }))
    await user.click(screen.getByText('Statement', { selector: '.ant-select-item-option-content' }))
    expect(statementsRequested).toBe(0)
    await user.click(screen.getByRole('combobox', { name: 'Select a card' }))
    await user.click(await screen.findByText('First card', { selector: '.ant-select-item-option-content' }))
    await waitFor(() => expect(statementsRequested).toBe(1))
    await user.click(screen.getByRole('combobox', { name: 'Select a statement' }))
    await user.click(await screen.findByText('Jun 2026', { selector: '.ant-select-item-option-content' }))
    await waitFor(() => expect(seenUrl?.searchParams.get('entityId')).toBe('s1'))
    expect(seenUrl?.searchParams.get('entityType')).toBe('statement')
    await user.click(screen.getByText('By correlation'))
    await user.click(screen.getByText('By entity'))
    expect(screen.getByRole('combobox', { name: 'Select a statement' })).toBeEnabled()
    expect(screen.getByText('Jun 2026', { selector: '.ant-select-content' })).toBeInTheDocument()
    await user.click(screen.getByRole('combobox', { name: 'Select a card' }))
    await user.click(screen.getByText('Second card', { selector: '.ant-select-item-option-content' }))
    expect(screen.getByText(/Select a record, or enter a correlation ID/)).toBeInTheDocument()
  })
})
