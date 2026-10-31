import { beforeEach, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { ComposeDraft, Mailbox, MessageSummary } from '../models'
import { ComposeDrawer } from './ComposeDrawer'

const BASE = `${TEST_API_BASE}/api/v1/communications`

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

const message: MessageSummary = {
  id: 'm1',
  mailboxId: 'mb1',
  threadKey: 't1',
  from: { name: 'Ana', address: 'ana@mail.test' },
  to: [],
  cc: [],
  subject: 'Question',
  snippet: '',
  sentAt: '2026-10-27T10:00:00Z',
  isUnread: false,
  isStarred: false,
  hasAttachments: false,
  labelIds: [],
}

const draft: ComposeDraft = {
  to: [{ name: 'Ana', address: 'ana@mail.test' }],
  cc: [{ name: null, address: 'bo@mail.test' }],
  subject: 'Re: Question',
  inReplyTo: 'q@mail.test',
  references: ['q@mail.test'],
  originalFrom: { name: 'Ana', address: 'ana@mail.test' },
  originalDate: '2026-10-27T10:00:00Z',
  originalText: 'The question',
  attachments: [{ id: '1', fileName: 'report.pdf', contentType: 'application/pdf', sizeBytes: 4, contentId: null }],
}

const refuse = (status: number, errorCode: string, errorMessage: string) =>
  HttpResponse.json({ success: false, errorCode, errorMessage, fieldErrors: null, traceId: null }, { status })

/** Counts the sends; the multipart body cannot be read under jsdom, so `toSendForm` has its own test. */
function countSends(answer: () => Response = () => HttpResponse.json({ success: true, data: { copySaved: true } })) {
  const sends = { count: 0 }
  server.use(
    http.post(`${BASE}/messages/send`, () => {
      sends.count++
      return answer()
    }),
  )
  return sends
}

async function fillNew(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('To'), 'ana@mail.test{Enter}')
  await user.type(screen.getByLabelText('Subject'), 'Hello')
  await user.type(screen.getByLabelText('Message'), 'The body')
}

describe('ComposeDrawer', () => {
  beforeEach(() => localStorage.clear())

  it('sends once on a double click, and closes', async () => {
    const sends = countSends()
    let closed = false
    const user = userEvent.setup()
    renderWithProviders(<ComposeDrawer request={{ mode: 'new' }} mailboxes={[mailbox]} onClose={() => (closed = true)} />)

    await fillNew(user)
    await user.dblClick(screen.getByRole('button', { name: 'Send' }))

    await waitFor(() => expect(closed).toBe(true))
    expect(sends.count).toBe(1)
  })

  it('keeps the content when the server refuses', async () => {
    const sends = countSends(() => refuse(422, 'Communications.RecipientsInvalid', 'Bad address'))
    let closed = false
    const user = userEvent.setup()
    renderWithProviders(<ComposeDrawer request={{ mode: 'new' }} mailboxes={[mailbox]} onClose={() => (closed = true)} />)

    await fillNew(user)
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(await screen.findByText('Bad address')).toBeInTheDocument()
    expect(sends.count).toBe(1)
    expect(closed).toBe(false)
    expect(screen.getByLabelText('Message')).toHaveValue('The body')
    expect(screen.getByRole('button', { name: 'Send' })).toBeEnabled()
  })

  it('does not offer to send again as if nothing happened when the mail may have gone', async () => {
    const sends = countSends(() => refuse(422, 'Communications.SendUncertain', 'It may have been sent'))
    const user = userEvent.setup()
    renderWithProviders(<ComposeDrawer request={{ mode: 'new' }} mailboxes={[mailbox]} onClose={() => {}} />)

    await fillNew(user)
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(await screen.findByText('It may have been sent')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Send' })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Send anyway' }))
    expect(screen.getByRole('button', { name: 'Send' })).toBeEnabled()
    expect(sends.count).toBe(1)
  })

  it('warns before sending with no subject', async () => {
    const sends = countSends()
    const user = userEvent.setup()
    renderWithProviders(<ComposeDrawer request={{ mode: 'new' }} mailboxes={[mailbox]} onClose={() => {}} />)

    await user.type(screen.getByLabelText('To'), 'ana@mail.test{Enter}')
    await user.type(screen.getByLabelText('Message'), 'The body')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect((await screen.findAllByText('Send with no subject or text?')).length).toBeGreaterThan(0)
    expect(sends.count).toBe(0)
  })

  it('needs a recipient', async () => {
    const sends = countSends()
    const user = userEvent.setup()
    renderWithProviders(<ComposeDrawer request={{ mode: 'new' }} mailboxes={[mailbox]} onClose={() => {}} />)

    await user.type(screen.getByLabelText('Subject'), 'Hello')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    await waitFor(() => expect(document.querySelector('.ant-form-item-explain-error')).not.toBeNull())
    expect(sends.count).toBe(0)
  })

  it('brings the draft back after closing, and forgets it after sending', async () => {
    const sends = countSends()
    const user = userEvent.setup()
    const first = renderWithProviders(<ComposeDrawer request={{ mode: 'new' }} mailboxes={[mailbox]} onClose={() => {}} />)
    await fillNew(user)
    first.unmount()

    let closed = false
    const second = renderWithProviders(<ComposeDrawer request={{ mode: 'new' }} mailboxes={[mailbox]} onClose={() => (closed = true)} />)
    expect(await screen.findByLabelText('Message')).toHaveValue('The body')
    await user.click(screen.getByRole('button', { name: 'Send' }))
    await waitFor(() => expect(closed).toBe(true))
    expect(sends.count).toBe(1)
    second.unmount()

    renderWithProviders(<ComposeDrawer request={{ mode: 'new' }} mailboxes={[mailbox]} onClose={() => {}} />)
    expect(await screen.findByLabelText('Message')).toHaveValue('')
  })

  it('fills a reply from the server, with the original quoted and the mailbox fixed', async () => {
    server.use(
      http.get(`${BASE}/messages/m1/compose`, ({ request }) => {
        expect(new URL(request.url).searchParams.get('mode')).toBe('reply-all')
        return HttpResponse.json({ success: true, data: draft })
      }),
    )
    renderWithProviders(<ComposeDrawer request={{ mode: 'reply-all', message }} mailboxes={[mailbox]} onClose={() => {}} />)

    expect(await screen.findByLabelText('Subject')).toHaveValue('Re: Question')
    expect(screen.getByText('ana@mail.test')).toBeInTheDocument()
    expect(screen.getByText('bo@mail.test')).toBeInTheDocument()
    expect((screen.getByLabelText('Message') as HTMLTextAreaElement).value).toContain('> The question')
    expect(screen.queryByText('report.pdf')).not.toBeInTheDocument()
  })

  it('offers the original files on a forward', async () => {
    server.use(
      http.get(`${BASE}/messages/m1/compose`, () =>
        HttpResponse.json({ success: true, data: { ...draft, to: [], cc: [], subject: 'Fwd: Question' } }),
      ),
    )
    renderWithProviders(<ComposeDrawer request={{ mode: 'forward', message }} mailboxes={[mailbox]} onClose={() => {}} />)

    expect(await screen.findByRole('checkbox', { name: 'report.pdf' })).toBeChecked()
  })

  it('shows why a reply cannot be prepared', async () => {
    server.use(http.get(`${BASE}/messages/m1/compose`, () => refuse(422, 'x', 'Server unreachable')))
    renderWithProviders(<ComposeDrawer request={{ mode: 'reply', message }} mailboxes={[mailbox]} onClose={() => {}} />)

    expect(await screen.findByText('Server unreachable')).toBeInTheDocument()
  })
})
