import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { createEvent, fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { NOTES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { MediaFoldersDto, MediaItemDto } from '../models'
import { MEDIA_DRAG_TYPE } from '../lib/media'
import { LibraryPage } from './LibraryPage'

const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const item = (over: Partial<MediaItemDto>): MediaItemDto => ({
  id: 'm1',
  folderId: null,
  kind: 'image',
  name: 'beach.png',
  originalFileName: 'beach.png',
  contentType: 'image/png',
  sizeBytes: 2048,
  width: 800,
  height: 600,
  url: '/api/v1/notes/media/m1',
  thumbnailUrl: '/api/v1/notes/media/m1/thumbnail',
  createdAt: '2026-11-02T10:00:00Z',
  ...over,
})

const folders: MediaFoldersDto = {
  folders: [
    { id: 'trips', parentId: null, name: 'Trips', itemCount: 1 },
    { id: 'japan', parentId: 'trips', name: 'Japan', itemCount: 0 },
  ],
  unsortedCount: 2,
  totalCount: 3,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
  URL.createObjectURL = vi.fn(() => 'blob:fake')
})

describe('LibraryPage', () => {
  let listed: string[]

  beforeEach(() => {
    listed = []
    server.use(
      http.get(`${NOTES_BASE}/media-folders`, () => ok(folders)),
      http.get(`${NOTES_BASE}/media`, ({ request }) => {
        listed.push(new URL(request.url).search)
        return ok([item({}), item({ id: 'm2', kind: 'file', name: 'receipt.pdf', contentType: 'application/pdf', width: null, height: null, thumbnailUrl: null })])
      }),
      http.get(`${NOTES_BASE}/media/:id/thumbnail`, () => new HttpResponse(new Blob(['x']))),
      http.get(`${NOTES_BASE}/media/:id`, () => new HttpResponse(new Blob(['x']))),
    )
  })

  it('shows the places with their counts and the items with what they are', async () => {
    renderWithProviders(<LibraryPage />)

    expect(await screen.findByRole('button', { name: 'beach.png' })).toHaveTextContent('2.0 KB · 800×600')
    expect(screen.getByRole('button', { name: /Everything/ })).toHaveTextContent('3')
    expect(screen.getByRole('button', { name: /Unsorted/ })).toHaveTextContent('2')
    expect(screen.getByText('Japan')).toBeInTheDocument()
  })

  it('asks for one folder once it is picked, and for every folder while searching', async () => {
    const user = userEvent.setup()
    renderWithProviders(<LibraryPage />)

    await user.click(await screen.findByText('Trips'))
    await waitFor(() => expect(listed).toContain('?folderId=trips'))

    await user.type(screen.getByRole('textbox', { name: 'Search by name' }), 'bea')
    await waitFor(() => expect(listed).toContain('?q=bea'))
  })

  it('uploads into the folder on screen', async () => {
    // The service pins a bare multipart Content-Type (no boundary under jsdom), so the body is read as text.
    const posted: string[] = []
    server.use(
      http.post(`${NOTES_BASE}/media`, async ({ request }) => {
        posted.push(await request.text())
        return ok(item({ id: 'new' }))
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<LibraryPage />, { route: '/?folder=trips' })

    await screen.findByRole('button', { name: 'beach.png' })
    await user.upload(screen.getByLabelText('Upload', { selector: 'input' }), new File(['img'], 'new.png', { type: 'image/png' }))

    await waitFor(() => expect(posted).toHaveLength(1))
    expect(posted[0]).toContain('trips')
  })

  it('moves an item dropped on a folder', async () => {
    const moves: unknown[] = []
    server.use(
      http.post(`${NOTES_BASE}/media/:id/move`, async ({ params, request }) => {
        moves.push({ id: params.id, ...(await request.json() as object) })
        return ok(item({ folderId: 'japan' }))
      }),
    )
    renderWithProviders(<LibraryPage />)

    await screen.findByRole('button', { name: 'beach.png' })
    const target = screen.getByText('Japan')
    const drop = createEvent.drop(target)
    Object.defineProperty(drop, 'dataTransfer', {
      value: { types: [MEDIA_DRAG_TYPE], getData: (type: string) => (type === MEDIA_DRAG_TYPE ? 'm1' : '') },
    })
    fireEvent(target, drop)

    await waitFor(() => expect(moves).toEqual([{ id: 'm1', folderId: 'japan' }]))
  })

  it('renames an item from its details', async () => {
    const renames: unknown[] = []
    server.use(
      http.put(`${NOTES_BASE}/media/:id`, async ({ request }) => {
        renames.push(await request.json())
        return ok(item({ name: 'Beach day' }))
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<LibraryPage />)

    await user.click(await screen.findByRole('button', { name: 'receipt.pdf' }))
    const drawer = await screen.findByRole('dialog')
    expect(within(drawer).getByText('File')).toBeInTheDocument()

    const name = within(drawer).getByRole('textbox', { name: 'Name' })
    await user.clear(name)
    await user.type(name, 'Lunch receipt{Enter}')

    await waitFor(() => expect(renames).toEqual([{ name: 'Lunch receipt' }]))
  })

  it('says where a deleted folder hands its contents', async () => {
    const user = userEvent.setup()
    renderWithProviders(<LibraryPage />)

    await screen.findByText('Japan')
    await user.click(screen.getByRole('button', { name: 'Actions for Japan' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Delete' }))

    expect(await screen.findByText('What it holds moves to Trips. Nothing is deleted with it.')).toBeInTheDocument()
  })
})
