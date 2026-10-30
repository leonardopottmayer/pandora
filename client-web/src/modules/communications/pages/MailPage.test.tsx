import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
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

describe('MailPage', () => {
  it('renders an opened message in a sandbox, without remote images until allowed', async () => {
    server.use(
      http.get(`${BASE}/mailboxes`, () => ok([mailbox])),
      http.get(`${BASE}/mailboxes/mb1/labels`, () =>
        ok([{ id: 'l1', name: 'INBOX', kind: 'system', role: 'inbox', isExclusive: true, total: 1, unread: 1 }]),
      ),
      http.get(`${BASE}/messages`, () => ok({ items: [summary], nextCursor: null })),
      http.get(`${BASE}/messages/m1`, () => ok(opened)),
    )
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
