import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { SnoozeModal } from './SnoozeModal'

beforeAll(async () => { await i18n.changeLanguage('en') })
afterEach(() => { vi.useRealTimers() })

describe('SnoozeModal', () => {
  it('refreshes the custom time on reopening and sends the new instant', async () => {
    vi.setSystemTime(new Date('2026-11-04T12:00:00Z'))
    let body: unknown
    const onClose = vi.fn()
    server.use(http.post(`${AGENDA_BASE}/reminders/r1/snooze`, async ({ request }) => {
      body = await request.json()
      return HttpResponse.json({ success: true, data: {} })
    }))
    const user = userEvent.setup()
    const { rerender } = renderWithProviders(<SnoozeModal open reminderId="r1" onClose={onClose} />)
    const format = (iso: string) => dayjs(iso).tz('America/Sao_Paulo').format('DD/MM/YYYY HH:mm')
    expect(screen.getByRole('textbox')).toHaveValue(format('2026-11-04T13:00:00Z'))
    rerender(<SnoozeModal open={false} reminderId="r1" onClose={onClose} />)
    vi.setSystemTime(new Date('2026-11-06T12:00:00Z'))
    rerender(<SnoozeModal open reminderId="r1" onClose={onClose} />)
    expect(screen.getByRole('textbox')).toHaveValue(format('2026-11-06T13:00:00Z'))
    await user.click(screen.getByRole('button', { name: 'Snooze' }))
    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toEqual({ until: '2026-11-06T10:00:00-03:00' }) // São Paulo's wall clock, with its offset
  })
})
