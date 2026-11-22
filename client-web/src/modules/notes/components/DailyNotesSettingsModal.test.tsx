import { beforeAll, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { NOTES_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import { DailyNotesSettingsModal } from './DailyNotesSettingsModal'

beforeAll(async () => { await i18n.changeLanguage('en') })

describe('DailyNotesSettingsModal', () => {
  it('saves an active template preference without creating or changing a page', async () => {
    let sent: unknown
    const close = vi.fn()
    server.use(
      http.get(`${NOTES_BASE}/preferences`, () => HttpResponse.json({ dailyTemplateId: null })),
      http.get(`${NOTES_BASE}/pages`, () => HttpResponse.json([
        { id: 'model', title: 'Journal template', isTemplate: true },
        { id: 'page', title: 'Ordinary page', isTemplate: false },
      ])),
      http.put(`${NOTES_BASE}/preferences`, async ({ request }) => {
        sent = await request.json()
        return HttpResponse.json({ dailyTemplateId: 'model' })
      }),
    )
    renderWithProviders(<DailyNotesSettingsModal onClose={close} />)
    await userEvent.click(screen.getByRole('combobox', { name: 'Daily note template' }))
    await userEvent.click(await screen.findByText('Journal template'))
    expect(screen.queryByText('Ordinary page')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(close).toHaveBeenCalledOnce())
    expect(sent).toEqual({ dailyTemplateId: 'model' })
  })

  it('allows clearing an unavailable template without exposing its internal id', async () => {
    let sent: unknown
    server.use(
      http.get(`${NOTES_BASE}/preferences`, () => HttpResponse.json({ dailyTemplateId: 'deleted-id' })),
      http.get(`${NOTES_BASE}/pages`, () => HttpResponse.json([])),
      http.put(`${NOTES_BASE}/preferences`, async ({ request }) => {
        sent = await request.json()
        return HttpResponse.json({ dailyTemplateId: null })
      }),
    )
    const close = vi.fn()
    renderWithProviders(<DailyNotesSettingsModal onClose={close} />)
    await screen.findByText('Template unavailable')
    expect(screen.queryByText('deleted-id')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    await userEvent.click(screen.getByRole('combobox', { name: 'Daily note template' }))
    await userEvent.click(await screen.findByText('No template'))
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(close).toHaveBeenCalledOnce())
    expect(sent).toEqual({ dailyTemplateId: null })
  })

  it('shows load failure and prevents saving defaults over unknown settings', async () => {
    server.use(
      http.get(`${NOTES_BASE}/preferences`, () => HttpResponse.json({}, { status: 503 })),
      http.get(`${NOTES_BASE}/pages`, () => HttpResponse.json([])),
    )
    renderWithProviders(<DailyNotesSettingsModal onClose={vi.fn()} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load notes.')
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })
})
