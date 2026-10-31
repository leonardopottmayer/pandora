import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { MailMessage, Mailbox, MessageSummary } from '../models'
import { MailPage } from './MailPage'

const BASE = `${TEST_API_BASE}/api/v1/communications`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const mailbox: Mailbox = {
  id: 'mb1',
  provider: 'titan',
  externalAccountId: 'acc1',
  address: 'me@titan.test',
  displayName: null,
  imapHost: null,
  imapPort: null,
  smtpHost: null,
  smtpPort: null,
  status: 'active',
  lastError: null,
  lastSyncedAt: null,
  messageCount: 1,
  messageTotalEstimate: 1,
  backfillCompletedAt: '2026-10-28T00:00:00Z',
}

const summary: MessageSummary = {
  id: 'm1',
  mailboxId: 'mb1',
  threadKey: 't1',
  from: { name: 'Newsletter', address: 'news@example.com' },
  to: [{ name: null, address: 'me@titan.test' }],
  cc: [],
  subject: 'Weekly digest',
  snippet: 'This week…',
  sentAt: '2026-10-27T10:00:00Z',
  isUnread: true,
  isStarred: false,
  hasAttachments: false,
  labelIds: [],
}

const opened: MailMessage = {
  message: summary,
  rfc822MessageId: null,
  textBody: null,
  htmlBody: '<p>Hello</p><img src="https://tracker.example/open.gif"><script>alert(1)</script>',
  attachments: [],
}

const inbox = { id: 'l1', name: 'INBOX', kind: 'system', role: 'inbox', isExclusive: true, total: 1, unread: 1 }
const second: Mailbox = { ...mailbox, id: 'mb2', address: 'me@other.test' }
const inSecond: MessageSummary = { ...summary, id: 'm2', mailboxId: 'mb2', subject: 'Other box' }

type ActionBody = { mailboxId: string; messageIds: string[]; action: string; labelId?: string }

/** Records every action, and answers with the messages unchanged unless told to refuse. */
function recordActions(status = 200) {
  const actions: ActionBody[] = []
  server.use(
    http.post(`${BASE}/messages/actions`, async ({ request }) => {
      const body = (await request.json()) as ActionBody
      actions.push(body)
      return status === 200
        ? ok([])
        : HttpResponse.json(
            { success: false, errorCode: 'x', errorMessage: 'Refused by the server', fieldErrors: null, traceId: null },
            { status },
          )
    }),
  )
  return actions
}

describe('MailPage', () => {
  it('splits a selection in two mailboxes into two requests, and offers no move for it', async () => {
    server.use(
      http.get(`${BASE}/mailboxes`, () => ok([mailbox, second])),
      http.get(`${BASE}/mailboxes/:id/labels`, () => ok([inbox])),
      http.get(`${BASE}/messages`, () => ok({ items: [summary, inSecond], nextCursor: null })),
    )
    const actions = recordActions()
    const user = userEvent.setup()
    renderWithProviders(<MailPage />)

    await user.click(await screen.findByRole('checkbox', { name: 'Select: Weekly digest' }))
    await user.click(screen.getByRole('checkbox', { name: 'Select: Other box' }))

    expect(screen.queryByRole('button', { name: 'Move to' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Star' }))

    await waitFor(() => expect(actions).toHaveLength(2))
    expect(actions).toEqual(
      expect.arrayContaining([
        { mailboxId: 'mb1', messageIds: ['m1'], action: 'star' },
        { mailboxId: 'mb2', messageIds: ['m2'], action: 'star' },
      ]),
    )
    await waitFor(() => expect(screen.getByRole('checkbox', { name: 'Select: Other box' })).not.toBeChecked())
  })

  it('offers moving within one mailbox, and never labels on IMAP', async () => {
    server.use(
      http.get(`${BASE}/mailboxes`, () => ok([mailbox])),
      http.get(`${BASE}/mailboxes/mb1/labels`, () =>
        ok([
          inbox,
          { id: 'l2', name: 'Projects', kind: 'user', role: null, isExclusive: true, total: 0, unread: 0 },
          { id: 'l3', name: 'Sent', kind: 'system', role: 'sent', isExclusive: true, total: 0, unread: 0 },
        ]),
      ),
      http.get(`${BASE}/messages`, () => ok({ items: [summary], nextCursor: null })),
    )
    const actions = recordActions()
    const user = userEvent.setup()
    renderWithProviders(<MailPage />)

    await user.click(await screen.findByRole('checkbox', { name: 'Select: Weekly digest' }))
    await user.click(await screen.findByRole('button', { name: 'Move to' }))

    expect(screen.queryByRole('button', { name: 'Labels' })).not.toBeInTheDocument()
    // Only the dropdown: the sidebar lists every label as a menu item too.
    const projects = await screen.findByRole('menuitem', { name: 'Projects' })
    const dropdown = within(projects.closest('ul')!)
    expect(dropdown.queryByRole('menuitem', { name: 'Sent' })).not.toBeInTheDocument()

    await user.click(projects)
    await waitFor(() =>
      expect(actions).toEqual([{ mailboxId: 'mb1', messageIds: ['m1'], action: 'move', labelId: 'l2' }]),
    )
  })

  it('keeps the row and the selection when the server refuses an action', async () => {
    server.use(
      http.get(`${BASE}/mailboxes`, () => ok([mailbox])),
      http.get(`${BASE}/mailboxes/mb1/labels`, () => ok([inbox])),
      http.get(`${BASE}/messages`, () => ok({ items: [summary], nextCursor: null })),
    )
    const actions = recordActions(422)
    const user = userEvent.setup()
    renderWithProviders(<MailPage />)

    const box = await screen.findByRole('checkbox', { name: 'Select: Weekly digest' })
    await user.click(box)
    await user.click(screen.getByRole('button', { name: 'Archive' }))

    await waitFor(() => expect(actions).toHaveLength(1))
    expect(await screen.findByText('Refused by the server')).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Select: Weekly digest' })).toBeChecked()
    expect(screen.getByText('Newsletter')).toBeInTheDocument()
  })

  it('asks before moving to the trash', async () => {
    server.use(
      http.get(`${BASE}/mailboxes`, () => ok([mailbox])),
      http.get(`${BASE}/mailboxes/mb1/labels`, () => ok([inbox])),
      http.get(`${BASE}/messages`, () => ok({ items: [summary], nextCursor: null })),
    )
    const actions = recordActions()
    const user = userEvent.setup()
    renderWithProviders(<MailPage />)

    await user.click(await screen.findByRole('checkbox', { name: 'Select: Weekly digest' }))
    await user.click(screen.getByRole('button', { name: 'Move to trash' }))

    expect(await screen.findByText('Move this message to the trash?')).toBeInTheDocument()
    expect(actions).toHaveLength(0)
  })

  it('marks an unread message as read once when it opens, and an already read one not at all', async () => {
    server.use(
      http.get(`${BASE}/mailboxes`, () => ok([mailbox])),
      http.get(`${BASE}/mailboxes/mb1/labels`, () => ok([inbox])),
      http.get(`${BASE}/messages`, () =>
        ok({ items: [summary, { ...summary, id: 'm3', subject: 'Read before', isUnread: false, from: { name: 'Old', address: 'o@x.test' } }], nextCursor: null }),
      ),
      http.get(`${BASE}/messages/m1`, () => ok(opened)),
      http.get(`${BASE}/messages/m3`, () => ok({ ...opened, message: { ...summary, id: 'm3', isUnread: false } })),
    )
    const actions = recordActions()
    const user = userEvent.setup()
    renderWithProviders(<MailPage />)

    await user.click(await screen.findByText('Newsletter'))
    await screen.findByTitle('Weekly digest')
    await waitFor(() => expect(actions).toEqual([{ mailboxId: 'mb1', messageIds: ['m1'], action: 'mark-read' }]))

    // The first is the row in the list; the reader's conversation lists it too.
    await user.click(screen.getAllByText('Old')[0])
    await screen.findByTitle('Read before')
    expect(actions).toHaveLength(1)
  })

  it('renders an opened message in a sandbox, without remote images until allowed', async () => {
    server.use(
      http.get(`${BASE}/mailboxes`, () => ok([mailbox])),
      http.get(`${BASE}/mailboxes/mb1/labels`, () =>
        ok([{ id: 'l1', name: 'INBOX', kind: 'system', role: 'inbox', isExclusive: true, total: 1, unread: 1 }]),
      ),
      http.get(`${BASE}/messages`, () => ok({ items: [summary], nextCursor: null })),
      http.get(`${BASE}/messages/m1`, () => ok(opened)),
    )
    recordActions()
    const user = userEvent.setup()
    renderWithProviders(<MailPage />)

    expect(await screen.findByText('Inbox')).toBeInTheDocument()
    await user.click(await screen.findByText('Newsletter'))

    const frame = (await screen.findByTitle('Weekly digest')) as HTMLIFrameElement
    expect(frame.getAttribute('sandbox')).toBe('allow-popups allow-popups-to-escape-sandbox')
    expect(frame.srcdoc).toContain('<p>Hello</p>')
    expect(frame.srcdoc).not.toContain('tracker.example')
    expect(frame.srcdoc).not.toContain('<script')

    await user.click(screen.getByRole('button', { name: 'Show images' }))
    await waitFor(() => expect(frame.srcdoc).toContain('https://tracker.example/open.gif'))
  })
})
