import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { AssistantProfile, Invocation } from '../models'
import { AssistantPage } from './AssistantPage'

const BASE = `${TEST_API_BASE}/api/v1.0/assistant`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const profile: AssistantProfile = {
  provider: 'gemini',
  chatAccountId: 'k1',
  model: 'gemini-test',
  isEnabled: true,
  localeOverride: null,
  confirmationLevel: 'balanced',
}

const invocation = (over: Partial<Invocation>): Invocation => ({
  id: 'i1',
  conversationId: 'c-new',
  utterance: 'remind me to pay rent',
  commandName: 'create_reminder',
  arguments: '{"title":"pay rent"}',
  status: 'executed',
  result: 'Reminder created.',
  error: null,
  provider: 'gemini',
  model: 'gemini-test',
  latencyMs: 420,
  promptTokens: 10,
  completionTokens: 5,
  expiresAt: null,
  createdAt: '2026-01-02T10:00:00Z',
  ...over,
})

// Newest first, as the API returns them.
const invocations = [
  invocation({}),
  invocation({
    id: 'i2',
    conversationId: 'c-old',
    utterance: 'delete my dentist event',
    commandName: 'delete_event',
    status: 'pending-confirmation',
    result: 'Delete "Dentist"?',
    createdAt: '2026-01-01T09:00:00Z',
  }),
]

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('AssistantPage', () => {
  beforeEach(() => {
    server.use(
      http.get(`${BASE}/profile`, () => ok(profile)),
      http.get(`${BASE}/invocations`, () => ok(invocations)),
    )
  })

  it('opens on the latest conversation, with the status shown only where it matters', async () => {
    renderWithProviders(<AssistantPage />)

    expect(await screen.findByText('Reminder created.')).toBeInTheDocument()
    expect(screen.queryByText('Executed')).not.toBeInTheDocument()
    expect(screen.queryByText('delete my dentist event')).not.toBeInTheDocument()
  })

  it('sends on Enter into the conversation on screen, and Shift+Enter only breaks the line', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post(`${BASE}/interpret`, async ({ request }) => {
        bodies.push(await request.json())
        return ok({ conversationId: 'c-new', message: 'ok', invocations: [], transcript: null })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AssistantPage />)
    await screen.findByText('Reminder created.')

    const input = screen.getByRole('textbox', { name: 'Ask the assistant…' })
    await user.type(input, 'and the water bill{Shift>}{Enter}{/Shift}too')
    expect(bodies).toHaveLength(0)
    await user.type(input, '{Enter}')

    await waitFor(() => expect(bodies).toEqual([{ text: 'and the water bill\ntoo', conversationId: 'c-new' }]))
  })

  it('starts a new conversation from an example', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post(`${BASE}/interpret`, async ({ request }) => {
        bodies.push(await request.json())
        return ok({ conversationId: 'c-3', message: 'ok', invocations: [], transcript: null })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AssistantPage />)
    await screen.findByText('Reminder created.')

    await user.click(screen.getByRole('button', { name: /New conversation/ }))
    expect(screen.getByText('How can I help?')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: "What's on my agenda today?" }))
    await user.click(screen.getByRole('button', { name: 'Send' }))

    await waitFor(() => expect(bodies).toEqual([{ text: "What's on my agenda today?", conversationId: null }]))
  })

  it('opens an older conversation from the history, where a held action can be confirmed', async () => {
    const confirmed = vi.fn()
    server.use(
      http.post(`${BASE}/invocations/:id/confirm`, ({ params }) => {
        confirmed(params.id)
        return ok({ invocationId: params.id, status: 'executed', commandName: null, arguments: null, message: '' })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AssistantPage />, { route: '/?tab=history' })

    const row = (await screen.findByText('delete my dentist event')).closest('tr')!
    await user.click(row.querySelector('button')!)
    await user.click(await screen.findByText('Open conversation'))

    // The history tab stays mounted, hidden: look only at the chat.
    const chat = await screen.findByRole('tabpanel', { name: 'Chat' })
    expect(within(chat).getByText('Awaiting confirmation')).toBeInTheDocument()
    expect(within(chat).getByText('Delete "Dentist"?')).toBeInTheDocument()
    await user.click(within(chat).getByRole('button', { name: 'Confirm' }))
    await waitFor(() => expect(confirmed).toHaveBeenCalledWith('i2'))
  })
})
