import { describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { TrashDrawer } from '../components/TrashDrawer'
import { PublicPage } from './PublicPage'

const ok = (data: unknown) => HttpResponse.json({ success: true, data })

describe('PublicPage', () => {
  it('renders Finance blocks as private without querying financial data', async () => {
    let reads = 0
    server.use(
      http.get(`${TEST_API_BASE}/api/v1/public/notes/pages/finance`, () => ok({
        title: 'Public finance note', icon: null, appearance: { width: 'normal', font: 'default', smallText: false }, updatedAt: null,
        content: { version: 1, blocks: [{ id: 'finance', type: 'finance-summary', props: { mode: 'account', accountId: 'private-bank' } }] },
      })),
      http.get(`${TEST_API_BASE}/api/v1.0/finances/*`, () => { reads++; return ok([]) }),
    )
    renderWithProviders(<Routes><Route path="/p/:token" element={<PublicPage />} /></Routes>, { route: '/p/finance' })
    expect(await screen.findByText('Private content')).toBeInTheDocument()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(reads).toBe(0)
  })

  it.each(['tasks', 'events'])('renders live Agenda %s blocks as private without requesting module data', async (mode) => {
    let reads = 0
    server.use(
      http.get(`${TEST_API_BASE}/api/v1/public/notes/pages/agenda`, () => ok({
        title: 'Public note', icon: null, appearance: { width: 'normal', font: 'default', smallText: false }, updatedAt: null,
        content: { version: 1, blocks: [{ id: 'agenda', type: 'agenda-list', props: { mode, listId: 'private-list', calendarId: 'private-calendar', days: 30 } }] },
      })),
      http.get(`${TEST_API_BASE}/api/v1.0/agenda/*`, () => { reads++; return ok([]) }),
    )
    renderWithProviders(<Routes><Route path="/p/:token" element={<PublicPage />} /></Routes>, { route: '/p/agenda' })
    expect(await screen.findByText('Private content')).toBeInTheDocument()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(reads).toBe(0)
  })

  it('shows the page read-only, a page it names as private', async () => {
    server.use(
      http.get(`${TEST_API_BASE}/api/v1/public/notes/pages/tok123`, () =>
        ok({
          title: 'Guia público',
          icon: '📘',
          appearance: { cover: null, width: 'normal', font: 'serif', smallText: false },
          updatedAt: null,
          content: { version: 1, blocks: [{ id: 'p', type: 'paragraph', content: [{ text: 'Olá ' }, { mention: { page: 'secret' } }] }] },
        }),
      ),
    )
    const { container } = renderWithProviders(<Routes><Route path="/p/:token" element={<PublicPage />} /></Routes>, { route: '/p/tok123' })

    expect(await screen.findByRole('heading', { name: 'Guia público' })).toBeInTheDocument()
    expect(await screen.findByText('Private page')).toBeInTheDocument()
    expect(container.querySelector('[contenteditable="true"]')).toBeNull()
    expect(container.querySelector('.notes-font-serif')).not.toBeNull()
  })

  it('says so when the link is closed', async () => {
    server.use(http.get(`${TEST_API_BASE}/api/v1/public/notes/pages/gone`, () => HttpResponse.json({ success: false, errorCode: 'Pages.NotFound' }, { status: 404 })))
    renderWithProviders(<Routes><Route path="/p/:token" element={<PublicPage />} /></Routes>, { route: '/p/gone' })
    expect(await screen.findByText(/not available/)).toBeInTheDocument()
  })
})

describe('TrashDrawer', () => {
  it('lists the deleted pages and brings one back', async () => {
    const restored = vi.fn()
    let restoredId = ''
    server.use(
      http.get(`${TEST_API_BASE}/api/v1/notes/trash`, () =>
        ok([{ id: 'p1', spaceId: 's1', title: 'Velho plano', icon: null, deletedAt: new Date().toISOString(), subpageCount: 2 }]),
      ),
      http.post(`${TEST_API_BASE}/api/v1/notes/trash/:id/restore`, ({ params }) => {
        restoredId = String(params.id)
        return ok({ id: 'p1' })
      }),
    )
    renderWithProviders(<TrashDrawer open onClose={() => undefined} onRestored={restored} />)

    expect(await screen.findByText('Velho plano')).toBeInTheDocument()
    expect(screen.getByText(/2 subpages/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Restore/ }))
    await waitFor(() => expect(restored).toHaveBeenCalledWith('p1'))
    expect(restoredId).toBe('p1')
  })
})
