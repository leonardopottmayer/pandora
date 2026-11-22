import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { NOTES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { SearchPalette } from './SearchPalette'

const hit = (id: string, title: string) => ({
  id,
  title,
  slug: title.toLowerCase(),
  icon: null,
  isArchived: false,
  excerpt: `trecho de ${title}`,
})

function mockSearch(results: ReturnType<typeof hit>[]) {
  server.use(
    http.get(`${NOTES_BASE}/pages/search`, () => HttpResponse.json({ success: true, data: results })),
  )
}

/** Stands in for the page: owns whether the palette is open, and offers a button that raises it —
 *  the sidebar's magnifier. */
function Harness({ onSelect }: { onSelect: (id: string) => void }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button aria-expanded={open} onClick={() => setOpen(true)}>search</button>
      <SearchPalette open={open} onOpenChange={setOpen} onSelect={onSelect} />
    </>
  )
}

describe('SearchPalette', () => {
  it('offers questions beside search and closes when a cited page is chosen', async () => {
    const onSelect = vi.fn()
    server.use(
      http.get(`${NOTES_BASE}/spaces`, () => HttpResponse.json({ data: [], success: true })),
      http.get(`${NOTES_BASE}/tags`, () => HttpResponse.json({ data: [], success: true })),
      http.post(`${NOTES_BASE}/ai/ask`, () => HttpResponse.json({ answer: 'March 20', sources: [
      { pageId: 'p1', spaceId: 's1', title: 'Apollo deadline', icon: null, excerpt: 'Launch date' },
    ] })))
    renderWithProviders(<Harness onSelect={onSelect} />)
    await userEvent.click(screen.getByRole('button', { name: 'search' }))
    await userEvent.click(await screen.findByText('Ask your notes'))
    await userEvent.type(screen.getByRole('textbox', { name: 'Question for your notes' }), 'Apollo deadline')
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }))
    await userEvent.click(await screen.findByRole('button', { name: /Apollo deadline/ }))
    expect(onSelect).toHaveBeenCalledWith('p1')
    expect(screen.getByRole('button', { name: 'search' })).toHaveAttribute('aria-expanded', 'false')
  })

  it('opens on Ctrl+K, searches, and opens the chosen page on Enter', async () => {
    mockSearch([hit('p1', 'Alpha'), hit('p2', 'Beta')])
    const onSelect = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<Harness onSelect={onSelect} />)

    expect(screen.queryByPlaceholderText('Search pages...')).not.toBeInTheDocument()

    await user.keyboard('{Control>}k{/Control}')
    const input = await screen.findByPlaceholderText('Search pages...')

    await user.type(input, 'a')
    expect(await screen.findByRole('option', { name: /Alpha/ })).toBeInTheDocument()

    // First hit is active; one arrow down moves to the second.
    await user.keyboard('{ArrowDown}{Enter}')
    expect(onSelect).toHaveBeenCalledWith('p2')
  })

  it('shows the empty state when nothing matches', async () => {
    mockSearch([])
    const user = userEvent.setup()
    renderWithProviders(<Harness onSelect={vi.fn()} />)

    await user.keyboard('{Control>}k{/Control}')
    await user.type(await screen.findByPlaceholderText('Search pages...'), 'zzz')

    expect(await screen.findByText('No page matches this search.')).toBeInTheDocument()
  })

  it('opens the page that was clicked', async () => {
    mockSearch([hit('p1', 'Alpha'), hit('p2', 'Beta')])
    const onSelect = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<Harness onSelect={onSelect} />)

    await user.keyboard('{Control>}k{/Control}')
    await user.type(await screen.findByPlaceholderText('Search pages...'), 'a')

    await user.click(await screen.findByRole('option', { name: /Beta/ }))
    expect(onSelect).toHaveBeenCalledWith('p2')
  })

  it('opens from the outside too, which is what the sidebar button does', async () => {
    mockSearch([hit('p1', 'Alpha')])
    const user = userEvent.setup()
    renderWithProviders(<Harness onSelect={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'search' }))
    expect(await screen.findByPlaceholderText('Search pages...')).toBeInTheDocument()
  })

  it('highlights the title and excerpt while rendering note HTML as literal text', async () => {
    mockSearch([{ ...hit('p1', 'Viagem anual'), excerpt: '<img src=x onerror=alert(1)> viagem' }])
    const user = userEvent.setup()
    renderWithProviders(<Harness onSelect={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: 'search' }))
    await user.type(await screen.findByPlaceholderText('Search pages...'), 'viag')
    const result = await screen.findByRole('option', { name: /Viagem anual/ })
    expect([...result.querySelectorAll('mark')].map((mark) => mark.textContent)).toEqual(['Viagem', 'viagem'])
    expect(within(result).getByText(/<img src=x onerror=alert\(1\)>/)).toBeInTheDocument()
    expect(result.querySelector('img')).toBeNull()
  })
})
