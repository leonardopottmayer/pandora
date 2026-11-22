import { beforeAll, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { NOTES_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import { TemplatePageModal } from './TemplatePageModal'

beforeAll(async () => { await i18n.changeLanguage('en') })

describe('TemplatePageModal', () => {
  it('offers active templates and creates a page in the requested destination', async () => {
    let sent: unknown
    const created = vi.fn()
    server.use(
      http.get(`${NOTES_BASE}/pages`, () => HttpResponse.json([
        { id: 'model', title: 'Review {{date}}', isTemplate: true },
        { id: 'ordinary', title: 'Ordinary page', isTemplate: false },
        { id: 'archived', title: 'Archived model', isTemplate: true, isArchived: true },
      ])),
      http.post(`${NOTES_BASE}/pages/from-template`, async ({ request }) => {
        sent = await request.json()
        return HttpResponse.json({ id: 'copy' })
      }),
    )
    renderWithProviders(<TemplatePageModal spaceId="destination" folderId="folder" onClose={vi.fn()} onCreated={created} />)
    expect(screen.getByRole('button', { name: 'New page' })).toBeDisabled()
    await userEvent.click(screen.getByRole('combobox', { name: 'Template' }))
    await userEvent.click(await screen.findByText('Review {{date}}'))
    expect(screen.queryByText('Ordinary page')).not.toBeInTheDocument()
    expect(screen.queryByText('Archived model')).not.toBeInTheDocument()
    const title = screen.getByRole('textbox', { name: 'New page title' })
    expect(title).toHaveValue('Review {{date}}')
    await userEvent.clear(title)
    await userEvent.type(title, 'Weekly review')
    await userEvent.click(screen.getByRole('button', { name: 'New page' }))
    await waitFor(() => expect(created).toHaveBeenCalledWith('copy'))
    expect(sent).toEqual({ templateId: 'model', title: 'Weekly review', spaceId: 'destination', folderId: 'folder' })
  })

  it('explains an empty catalog and keeps creation disabled', async () => {
    server.use(http.get(`${NOTES_BASE}/pages`, () => HttpResponse.json([])))
    renderWithProviders(<TemplatePageModal spaceId={null} folderId={null} onClose={vi.fn()} onCreated={vi.fn()} />)
    await userEvent.click(screen.getByRole('combobox', { name: 'Template' }))
    expect(await screen.findByText('Mark a page as a template in its page menu to use it here.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New page' })).toBeDisabled()
  })

  it('keeps the selection and title after a failed creation so the user can retry', async () => {
    const created = vi.fn()
    server.use(
      http.get(`${NOTES_BASE}/pages`, () => HttpResponse.json([{ id: 'model', title: 'Review', isTemplate: true }])),
      http.post(`${NOTES_BASE}/pages/from-template`, () => HttpResponse.json({ success: false, errorMessage: 'Please retry.' }, { status: 503 })),
    )
    renderWithProviders(<TemplatePageModal spaceId="s" folderId={null} onClose={vi.fn()} onCreated={created} />)
    await userEvent.click(screen.getByRole('combobox', { name: 'Template' }))
    await userEvent.click(await screen.findByText('Review'))
    await userEvent.click(screen.getByRole('button', { name: 'New page' }))
    expect(await screen.findByText('Please retry.')).toBeInTheDocument()
    expect(created).not.toHaveBeenCalled()
    expect(screen.getByRole('textbox', { name: 'New page title' })).toHaveValue('Review')
    expect(screen.getByRole('button', { name: 'New page' })).toBeEnabled()
  })
})
