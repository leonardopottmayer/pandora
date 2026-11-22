import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { NOTES_BASE, TEST_API_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import { SaveMessageToNotesModal } from './SaveMessageToNotesModal'

const URL = `${TEST_API_BASE}/api/v1/communications/messages/message/notes`
beforeAll(async () => { await i18n.changeLanguage('en') })
beforeEach(() => server.use(
  http.get(`${NOTES_BASE}/spaces`, () => HttpResponse.json([{ id: 's1', name: 'Home', isDefault: true }, { id: 's2', name: 'Work' }])),
  http.get(`${NOTES_BASE}/folders`, () => HttpResponse.json([
    { id: 'parent', spaceId: 's1', name: 'Archive', parentId: null, orderIndex: 0 },
    { id: 'nested', spaceId: 's1', name: 'Invoices', parentId: 'parent', orderIndex: 0 },
    { id: 'other', spaceId: 's2', name: 'Other space folder', parentId: null, orderIndex: 0 },
  ])),
))

describe('SaveMessageToNotesModal', () => {
  it('chooses a nested folder and title, sends identifiers rather than mail content, and opens the created page', async () => {
    let sent: unknown
    const close = vi.fn()
    server.use(http.post(URL, async ({ request }) => {
      sent = await request.json()
      return HttpResponse.json('new-page')
    }))
    renderWithProviders(<Routes>
      <Route path="/" element={<SaveMessageToNotesModal messageId="message" subject="Original subject" onClose={close} />} />
      <Route path="/notes/new-page" element={<p>Created page opened</p>} />
    </Routes>)
    const title = screen.getByRole('textbox', { name: 'Page title' })
    expect(title).toHaveValue('Original subject')
    await userEvent.clear(title)
    await userEvent.type(title, 'Copied mail')
    await userEvent.click(screen.getByRole('combobox', { name: 'Folder' }))
    await userEvent.click(await screen.findByText('Archive / Invoices'))
    expect(screen.queryByText('Other space folder')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Created page opened')).toBeInTheDocument()
    expect(close).toHaveBeenCalledOnce()
    expect(sent).toEqual({ title: 'Copied mail', spaceId: 's1', folderId: 'nested' })
  })

  it('clears the selected folder when changing space', async () => {
    let sent: unknown
    const close = vi.fn()
    server.use(http.post(URL, async ({ request }) => {
      sent = await request.json()
      return HttpResponse.json('new-page')
    }))
    renderWithProviders(<SaveMessageToNotesModal messageId="message" subject="Subject" onClose={close} />)
    await userEvent.click(screen.getByRole('combobox', { name: 'Folder' }))
    await userEvent.click(await screen.findByText('Archive / Invoices'))
    await userEvent.click(screen.getByRole('combobox', { name: 'Space' }))
    await userEvent.click(await screen.findByText('Work'))
    expect(screen.getByRole('combobox', { name: 'Folder' }).closest('.ant-select')).toHaveTextContent('Top of the space')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(close).toHaveBeenCalledOnce())
    expect(sent).toEqual({ title: 'Subject', spaceId: 's2', folderId: null })
  })

  it('refuses to save when destinations failed to load', async () => {
    server.use(http.get(`${NOTES_BASE}/folders`, () => HttpResponse.json({}, { status: 503 })))
    renderWithProviders(<SaveMessageToNotesModal messageId="message" subject="Subject" onClose={vi.fn()} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load Notes destinations.')
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('keeps the title and dialog after a failed copy so the user can retry', async () => {
    const close = vi.fn()
    server.use(http.post(URL, () => HttpResponse.json({ success: false, errorMessage: 'Folder no longer exists.' }, { status: 404 })))
    renderWithProviders(<SaveMessageToNotesModal messageId="message" subject="Subject" onClose={close} />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled())
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Folder no longer exists.')).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Page title' })).toHaveValue('Subject')
    expect(close).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()
  })
})
