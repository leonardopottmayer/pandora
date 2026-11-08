import { beforeAll, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { NOTES_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import { useUpdatePage } from '../hooks/usePages'
import { FolderPropertiesTable } from './FolderPropertiesTable'

beforeAll(async () => { await i18n.changeLanguage('en') })

const page = (id: string) => ({ id, title: id, icon: null })
const props = { folderId: 'folder', includeArchived: false, tagIds: [] as string[], onOpen: vi.fn() }

describe('FolderPropertiesTable', () => {
  it('shows property columns, repeated values and missing cells as plain read-only text, and opens a page', async () => {
    server.use(http.get(`${NOTES_BASE}/folders/folder/properties`, () => HttpResponse.json({
      columns: ['Status', 'Owner'], pages: [
        { page: page('A'), properties: { Status: 'Pending\nReview', Owner: '<img src=x onerror=alert(1)>' } },
        { page: page('B'), properties: {} },
      ],
    })))
    const open = vi.fn()
    const { container } = renderWithProviders(<FolderPropertiesTable {...props} onOpen={open} />)
    await screen.findByRole('button', { name: 'A' })
    expect(screen.getByRole('columnheader', { name: 'Status' })).toBeInTheDocument()
    expect(screen.getByText('Pending Review')).toBeInTheDocument()
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeInTheDocument()
    expect(container.querySelector('img')).toBeNull()
    const missing = screen.getByRole('button', { name: 'B' }).closest('tr')!
    expect(within(missing).getAllByText('—')).toHaveLength(2)
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'A' }))
    expect(open).toHaveBeenCalledWith(expect.objectContaining({ kind: 'page', id: 'A', page: page('A') }))
  })

  it('forwards archive and tag filters and clears the old folder rows while changing folders', async () => {
    let query: URLSearchParams | undefined
    server.use(
      http.get(`${NOTES_BASE}/folders/folder/properties`, ({ request }) => {
        query = new URL(request.url).searchParams
        return HttpResponse.json({ columns: [], pages: [{ page: page('Old page'), properties: {} }] })
      }),
      http.get(`${NOTES_BASE}/folders/second/properties`, () => HttpResponse.json({ columns: [], pages: [] })),
    )
    const { rerender } = renderWithProviders(<FolderPropertiesTable {...props} includeArchived tagIds={['one', 'two']} />)
    await screen.findByRole('button', { name: 'Old page' })
    expect(query?.get('includeArchived')).toBe('true')
    expect(query?.getAll('tagIds')).toEqual(['one', 'two'])
    rerender(<FolderPropertiesTable {...props} folderId="second" />)
    expect(screen.queryByRole('button', { name: 'Old page' })).not.toBeInTheDocument()
    expect(await screen.findByText('No pages directly in this folder.')).toBeInTheDocument()
  })

  it('shows an API failure without displaying a stale table', async () => {
    server.use(http.get(`${NOTES_BASE}/folders/folder/properties`, () => HttpResponse.json({ success: false, errorMessage: 'Folder unavailable.' }, { status: 404 })))
    renderWithProviders(<FolderPropertiesTable {...props} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Folder unavailable.')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('refreshes the active projection after a page save', async () => {
    let saved = false
    let reads = 0
    server.use(
      http.get(`${NOTES_BASE}/folders/folder/properties`, () => {
        reads++
        return HttpResponse.json({ columns: ['Status'], pages: [{ page: page('A'), properties: { Status: saved ? 'Done' : 'Pending' } }] })
      }),
      http.put(`${NOTES_BASE}/pages/A`, () => {
        saved = true
        return HttpResponse.json({ id: 'A' })
      }),
    )
    function SavePage() {
      const save = useUpdatePage()
      return <button onClick={() => save.mutate({ id: 'A', body: { title: 'A', content: { version: 1, blocks: [] } } })}>Save page</button>
    }
    renderWithProviders(<><FolderPropertiesTable {...props} /><SavePage /></>)
    await screen.findByText('Pending')
    await userEvent.click(screen.getByRole('button', { name: 'Save page' }))
    await screen.findByText('Done')
    await waitFor(() => expect(reads).toBe(2))
  })
})
