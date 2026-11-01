import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { MailRule, Mailbox } from '../models'
import { MailRulesPage } from './MailRulesPage'

const BASE = `${TEST_API_BASE}/api/v1/communications`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })
const refused = (errorCode: string, errorMessage: string) =>
  HttpResponse.json({ success: false, errorCode, errorMessage, fieldErrors: null, traceId: null }, { status: 422 })

const gmail: Mailbox = {
  id: 'mb1',
  provider: 'gmail',
  externalAccountId: 'acc1',
  address: 'me@gmail.test',
  displayName: null,
  imapHost: null,
  imapPort: null,
  smtpHost: null,
  smtpPort: null,
  status: 'active',
  lastError: null,
  lastSyncedAt: null,
  messageCount: 0,
  messageTotalEstimate: 0,
  backfillCompletedAt: '2026-10-28T00:00:00Z',
  filterSyncError: null,
}

const filterRule: MailRule = {
  id: 'r1',
  name: 'Bills',
  position: 0,
  conditions: [{ kind: 'from', value: 'bank' }],
  actions: [{ kind: 'star', labelId: null }],
  execution: 'provider-filter',
  isEnabled: true,
  status: 'ok',
  lastError: null,
  providerFilterRaw: null,
}

const readOnlyRule: MailRule = {
  ...filterRule,
  id: 'r2',
  name: 'size: 100',
  conditions: [],
  actions: [],
  status: 'read-only',
  providerFilterRaw: '{"action":{"forward":"x@mail.test"}}',
}

function serve(mailbox: Mailbox, rules: MailRule[]) {
  server.use(
    http.get(`${BASE}/mailboxes`, () => ok([mailbox])),
    http.get(`${BASE}/mailboxes/:id/labels`, () => ok([])),
    http.get(`${BASE}/mailboxes/:id/rules`, () => ok(rules)),
    http.get(`${TEST_API_BASE}/api/v1.0/integrations/providers`, () =>
      ok([{ provider: 'google', defaultScopes: ['openid'] }]),
    ),
  )
}

describe('MailRulesPage', () => {
  beforeEach(() => sessionStorage.clear())
  afterEach(() => vi.unstubAllGlobals())

  it('does not send a rule the form refuses', async () => {
    serve(gmail, [])
    const posted = vi.fn()
    server.use(http.post(`${BASE}/mailboxes/:id/rules`, () => (posted(), ok([]))))
    const user = userEvent.setup()
    renderWithProviders(<MailRulesPage />)

    await user.click(await screen.findByRole('button', { name: /New rule/ }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText(/Give the rule a name/)).toBeInTheDocument()

    await user.type(screen.getByRole('textbox', { name: /Name/ }), 'Mine')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText(/Fill in every condition/)).toBeInTheDocument()
    expect(posted).not.toHaveBeenCalled()
  })

  it('sends a good rule and closes the form', async () => {
    serve(gmail, [])
    const bodies: unknown[] = []
    server.use(
      http.post(`${BASE}/mailboxes/:id/rules`, async ({ request }) => {
        bodies.push(await request.json())
        return ok([])
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<MailRulesPage />)

    await user.click(await screen.findByRole('button', { name: /New rule/ }))
    await user.type(screen.getByRole('textbox', { name: /Name/ }), ' Mine ')
    await user.type(screen.getByRole('textbox', { name: 'Value' }), 'ana')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('textbox', { name: 'Value' })).not.toBeInTheDocument())
    expect(bodies).toEqual([
      { name: 'Mine', conditions: [{ kind: 'from', value: 'ana' }], actions: [{ kind: 'mark-read', labelId: null }] },
    ])
  })

  it('says that deleting a Gmail rule deletes the filter, and only then does it', async () => {
    serve(gmail, [filterRule])
    const deleted = vi.fn()
    server.use(http.delete(`${BASE}/rules/:id`, () => (deleted(), ok([]))))
    const user = userEvent.setup()
    renderWithProviders(<MailRulesPage />)

    await user.click(await screen.findByRole('button', { name: 'Delete' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/deletes the filter from your Gmail account too/)).toBeInTheDocument()
    expect(deleted).not.toHaveBeenCalled()

    await user.click(within(dialog).getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(deleted).toHaveBeenCalledOnce())
  })

  it('says that disabling a Gmail rule stops it in Gmail, and sends nothing if declined', async () => {
    serve(gmail, [filterRule])
    const put = vi.fn()
    server.use(http.put(`${BASE}/rules/:id`, () => (put(), ok([]))))
    const user = userEvent.setup()
    renderWithProviders(<MailRulesPage />)

    await user.click(await screen.findByRole('switch'))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/stops working there too/)).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    expect(put).not.toHaveBeenCalled()
  })

  it('disables a rule that runs in Pandora without asking', async () => {
    serve({ ...gmail, provider: 'titan' }, [{ ...filterRule, execution: 'pandora' }])
    const bodies: { isEnabled: boolean }[] = []
    server.use(
      http.put(`${BASE}/rules/:id`, async ({ request }) => {
        bodies.push((await request.json()) as { isEnabled: boolean })
        return ok([])
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<MailRulesPage />)

    await user.click(await screen.findByRole('switch'))

    await waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0].isEnabled).toBe(false)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('shows a read-only rule with no edit and no toggle, and warns about what deleting loses', async () => {
    serve(gmail, [readOnlyRule])
    const user = userEvent.setup()
    renderWithProviders(<MailRulesPage />)

    expect(await screen.findByText(/Edit it in Gmail/)).toBeInTheDocument()
    expect(screen.getByText(/"forward"/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
    expect(screen.queryByRole('switch')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Delete' }))
    expect(await screen.findByText(/such as a forward/)).toBeInTheDocument()
  })

  it('highlights what is wrong: a broken rule, its error, and a filter sync that failed', async () => {
    serve({ ...gmail, filterSyncError: 'Filters are down' }, [
      { ...filterRule, status: 'broken', lastError: "The label 'Work' no longer exists." },
    ])
    renderWithProviders(<MailRulesPage />)

    expect(await screen.findByText('Broken')).toBeInTheDocument()
    expect(screen.getByText(/no longer exists/)).toBeInTheDocument()
    expect(screen.getByText('Filters are down')).toBeInTheDocument()
  })

  it('moves a rule with the buttons', async () => {
    serve(gmail, [filterRule, { ...filterRule, id: 'r3', name: 'Other' }])
    const bodies: unknown[] = []
    server.use(
      http.put(`${BASE}/mailboxes/:id/rules/order`, async ({ request }) => {
        bodies.push(await request.json())
        return ok([])
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<MailRulesPage />)

    await user.click((await screen.findAllByRole('button', { name: 'Move down' }))[0])

    await waitFor(() => expect(bodies).toEqual([{ ruleIds: ['r3', 'r1'] }]))
  })

  it('moves a rule by dragging it onto another', async () => {
    serve(gmail, [filterRule, { ...filterRule, id: 'r3', name: 'Other' }, { ...filterRule, id: 'r4', name: 'Last' }])
    const bodies: unknown[] = []
    server.use(
      http.put(`${BASE}/mailboxes/:id/rules/order`, async ({ request }) => {
        bodies.push(await request.json())
        return ok([])
      }),
    )
    renderWithProviders(<MailRulesPage />)

    const row = (name: string) => screen.getByText(name).closest('[draggable="true"]')!
    await screen.findByText('Bills')
    fireEvent.dragStart(row('Last'))
    fireEvent.dragOver(row('Bills'))
    fireEvent.drop(row('Bills'))

    await waitFor(() => expect(bodies).toEqual([{ ruleIds: ['r4', 'r1', 'r3'] }]))
  })

  it('offers consent when Gmail lacks the scope, and brings the form back afterwards', async () => {
    serve(gmail, [])
    server.use(
      http.post(`${BASE}/mailboxes/:id/rules`, () => refused('Communications.MissingFilterScope', 'No access')),
      http.post(`${TEST_API_BASE}/api/v1.0/integrations/google/connect`, () => ok('https://accounts.google.test/consent')),
    )
    const assign = vi.fn()
    vi.stubGlobal('location', { ...window.location, assign })
    const user = userEvent.setup()
    const first = renderWithProviders(<MailRulesPage />)

    await user.click(await screen.findByRole('button', { name: /New rule/ }))
    await user.type(screen.getByRole('textbox', { name: /Name/ }), 'Mine')
    await user.type(screen.getByRole('textbox', { name: 'Value' }), 'ana')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await user.click(await screen.findByRole('button', { name: 'Grant access' }))
    await waitFor(() => expect(assign).toHaveBeenCalledWith('https://accounts.google.test/consent'))
    first.unmount()

    // Back from Google: a new page, with the form as it was.
    renderWithProviders(<MailRulesPage />)
    expect(await screen.findByRole('textbox', { name: /Name/ })).toHaveValue('Mine')
    expect(screen.getByRole('textbox', { name: 'Value' })).toHaveValue('ana')
    expect(screen.queryByText(/needs your permission/)).not.toBeInTheDocument()
  })
})
