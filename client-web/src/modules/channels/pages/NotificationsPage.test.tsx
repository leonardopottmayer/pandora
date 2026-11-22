import { beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { NotificationHistoryItem, NotificationSettings } from '../models'
import { NotificationsPage } from './NotificationsPage'

const BASE = `${TEST_API_BASE}/api/v1.0/channels`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const quietOff: NotificationSettings = {
  quietHoursEnabled: false,
  quietHoursStart: null,
  quietHoursEnd: null,
  quietHoursBehaviour: null,
}

const failed: NotificationHistoryItem = {
  id: 'n1',
  channel: 'telegram',
  category: 'agenda.task',
  templateKey: 'agenda.task-alert',
  subject: 'Pay the bill',
  status: 'Failed',
  attemptCount: 3,
  lastError: 'Bot was blocked',
  provider: null,
  correlationId: 'c1',
  groupId: null,
  createdAt: '2026-01-05T10:00:00Z',
  updatedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('NotificationsPage', () => {
  beforeEach(() => {
    server.use(
      http.get(BASE, () => ok([{ channel: 'email', address: 'me@mail.test', isVerified: true, isEnabled: true, disabledReason: null, verifiedAt: null }])),
      http.get(`${BASE}/preferences`, () => ok([{ category: 'agenda.task', channels: ['telegram'] }])),
      http.get(`${BASE}/notification-settings`, () => ok(quietOff)),
      http.get(`${BASE}/notifications`, () => ok([failed])),
    )
  })

  it('opens on the settings, without the history', async () => {
    renderWithProviders(<NotificationsPage />)

    expect(await screen.findByText('me@mail.test')).toBeInTheDocument()
    expect(screen.queryByText('Pay the bill')).not.toBeInTheDocument()
  })

  it('opens on the history from the URL, showing what went out and why it failed', async () => {
    renderWithProviders(<NotificationsPage />, { route: '/?tab=history' })

    expect(await screen.findByText('Pay the bill')).toBeInTheDocument()
    expect(screen.getByText(/Tasks · 3 attempts/)).toBeInTheDocument()
    expect(screen.getByText('Bot was blocked')).toBeInTheDocument()
  })

  it('lists the mailbox category too, and saves a change to the channels', async () => {
    const saved: { category: string; body: unknown }[] = []
    server.use(
      http.put(`${BASE}/preferences/:category`, async ({ params, request }) => {
        saved.push({ category: String(params.category), body: await request.json() })
        return ok(null)
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<NotificationsPage />)

    await user.click(await screen.findByRole('checkbox', { name: 'Mailboxes: Telegram' }))

    await waitFor(() =>
      expect(saved).toEqual([{ category: 'communications.mailbox', body: { channels: ['email'] } }]),
    )
  })

  it('starts quiet hours from a usable window and saves without a button', async () => {
    const saved: unknown[] = []
    server.use(
      http.put(`${BASE}/notification-settings`, async ({ request }) => {
        saved.push(await request.json())
        return ok(null)
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<NotificationsPage />)

    await user.click(await screen.findByRole('switch', { name: 'Enable quiet hours' }))

    await waitFor(() =>
      expect(saved).toEqual([
        { quietHoursEnabled: true, quietHoursStart: '22:00', quietHoursEnd: '07:00', quietHoursBehaviour: 'suppress' },
      ]),
    )
    expect(screen.queryByRole('button', { name: /save/i })).not.toBeInTheDocument()
  })
})
