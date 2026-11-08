import { beforeAll, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { NOTES_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import * as access from '@/modules/identity/hooks/useAccess'
import { DailyNoteButton } from './DailyNoteButton'

beforeAll(async () => { await i18n.changeLanguage('en') })

describe('DailyNoteButton', () => {
  it('leaves today to the server and opens the returned page through the sidebar callback', async () => {
    let body: unknown
    const onOpen = vi.fn()
    server.use(http.post(`${NOTES_BASE}/daily`, async ({ request }) => {
      body = await request.json()
      return HttpResponse.json({ id: 'today-page' })
    }))
    renderWithProviders(<DailyNoteButton onOpen={onOpen} />)
    await userEvent.click(screen.getByRole('button', { name: 'Today' }))
    await waitFor(() => expect(onOpen).toHaveBeenCalledWith('today-page'))
    expect(body).toEqual({})
  })

  it('navigates to the page when used from Home', async () => {
    server.use(http.post(`${NOTES_BASE}/daily`, () => HttpResponse.json({ id: 'today-page' })))
    renderWithProviders(<Routes>
      <Route path="/" element={<DailyNoteButton />} />
      <Route path="/notes/today-page" element={<p>Daily page opened</p>} />
    </Routes>)
    await userEvent.click(screen.getByRole('button', { name: 'Today' }))
    expect(await screen.findByText('Daily page opened')).toBeInTheDocument()
  })

  it('shows a server error and allows another attempt', async () => {
    const onOpen = vi.fn()
    server.use(http.post(`${NOTES_BASE}/daily`, () => HttpResponse.json({ success: false, errorMessage: 'Please retry.' }, { status: 503 })))
    renderWithProviders(<DailyNoteButton onOpen={onOpen} />)
    await userEvent.click(screen.getByRole('button', { name: 'Today' }))
    expect(await screen.findByText('Please retry.')).toBeInTheDocument()
    expect(onOpen).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Today' })).toBeEnabled()
  })

  it('hides the shortcut without the permissions to open or create a daily note', () => {
    const permission = vi.spyOn(access, 'usePermission').mockReturnValue(false)
    renderWithProviders(<DailyNoteButton />)
    expect(screen.queryByRole('button', { name: 'Today' })).not.toBeInTheDocument()
    permission.mockRestore()
  })
})
