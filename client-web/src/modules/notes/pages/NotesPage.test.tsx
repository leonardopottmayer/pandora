import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { PageDto, PageSummaryDto, SpaceDto } from '../models'
import { NotesPage } from './NotesPage'

const BASE = `${TEST_API_BASE}/api/v1/notes`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const space: SpaceDto = {
  id: 's1',
  name: 'Home',
  icon: '🏠',
  color: null,
  description: null,
  isDefault: true,
  isArchived: false,
  pageCount: 2,
  lastEditedAt: null,
  createdAt: '2026-11-01T00:00:00Z',
}

const summary = (id: string, title: string, parentId: string | null): PageSummaryDto => ({
  id,
  spaceId: 's1',
  parentId,
  title,
  slug: id,
  icon: null,
  orderIndex: 0,
  isFavorite: false,
  isArchived: false,
})

const child: PageDto = {
  ...summary('child', 'Packing list', 'parent'),
  contentMarkdown: 'Passport and charger.',
  createdAt: '2026-11-01T00:00:00Z',
  updatedAt: new Date().toISOString(),
  tags: [],
}

/** antd reads the width through matchMedia; the test setup answers "no" to every query, a phone. */
function asDesktop() {
  vi.spyOn(window, 'matchMedia').mockImplementation(
    (query: string) =>
      ({
        matches: query.includes('min-width'),
        media: query,
        onchange: null,
        addListener: () => {},
        removeListener: () => {},
        addEventListener: () => {},
        removeEventListener: () => {},
        dispatchEvent: () => false,
      }) as MediaQueryList,
  )
}

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/notes/:id" element={<NotesPage />} />
      <Route path="/notes/s/:spaceId" element={<NotesPage />} />
    </Routes>,
    { route: '/notes/child' },
  )
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('NotesPage', () => {
  it('shows a failed page load instead of spinning indefinitely', async () => {
    server.use(http.get(`${BASE}/pages/child`, () => HttpResponse.json({ success: false, errorCode: 'Unavailable', errorMessage: 'Page could not load', fieldErrors: null, traceId: null }, { status: 500 })))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('Page could not load')
    expect(screen.queryByRole('heading', { name: 'Packing list' })).not.toBeInTheDocument()
  })

  beforeEach(() => {
    // The editor is CodeMirror; reading mode keeps these tests on the rendered page.
    localStorage.setItem('pandora.notes_view_mode', 'preview')
    server.use(
      http.get(`${BASE}/pages/child`, () => ok(child)),
      http.get(`${BASE}/pages/child/backlinks`, () => ok([])),
      http.get(`${BASE}/pages`, () => ok([summary('parent', 'Trip', null), summary('child', 'Packing list', 'parent')])),
      http.get(`${BASE}/spaces`, () => ok([space])),
      http.get(`${BASE}/tags`, () => ok([])),
    )
  })

  afterEach(() => {
    vi.restoreAllMocks()
    localStorage.clear()
  })

  it('places the page under its space and parent, with the title as part of the document', async () => {
    asDesktop()
    renderPage()

    const path = await screen.findByRole('navigation', { name: 'Where this page is' })
    await waitFor(() => expect(within(path).getByRole('button', { name: /Home/ })).toBeInTheDocument())
    expect(within(path).getByRole('button', { name: 'Trip' })).toBeInTheDocument()
    expect(within(path).getByText('Packing list')).toBeInTheDocument()

    expect(screen.getByRole('textbox', { name: 'Page title' })).toHaveValue('Packing list')
    expect(screen.getByText(/^edited/)).toBeInTheDocument()
    expect(screen.getByText('Passport and charger.')).toBeInTheDocument()
  })

  it('keeps the mentions in the info panel, out of the way of the page', async () => {
    asDesktop()
    const user = userEvent.setup()
    renderPage()

    await screen.findByText('Passport and charger.')
    expect(screen.queryByText('Linked mentions')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Page info' }))
    expect(await screen.findByText('Linked mentions')).toBeInTheDocument()
  })

  it('remembers the sidebar folded away', async () => {
    asDesktop()
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: /Hide the sidebar/ }))

    expect(localStorage.getItem('pandora.notes_sidebar_closed')).toBe('1')
    expect(screen.getByRole('button', { name: /Show the sidebar/ })).toBeInTheDocument()
  })

  it('opens a page to read on a phone, with the way back up and editing one tap away', async () => {
    localStorage.removeItem('pandora.notes_view_mode')
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByRole('heading', { name: 'Packing list' })).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: 'Page title' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Side by side/ })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Trip/ })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /Edit/ }))
    expect(screen.getByRole('textbox', { name: 'Page title' })).toHaveValue('Packing list')
    expect(screen.getByRole('button', { name: /Done/ })).toBeInTheDocument()
  })
})
