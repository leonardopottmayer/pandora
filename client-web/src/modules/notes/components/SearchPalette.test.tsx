import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
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
      <button onClick={() => setOpen(true)}>search</button>
      <SearchPalette open={open} onOpenChange={setOpen} onSelect={onSelect} />
    </>
  )
}

describe('SearchPalette', () => {
  it('opens on Ctrl+K, searches, and opens the chosen page on Enter', async () => {
    mockSearch([hit('p1', 'Alpha'), hit('p2', 'Beta')])
    const onSelect = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<Harness onSelect={onSelect} />)

    expect(screen.queryByPlaceholderText('Search pages...')).not.toBeInTheDocument()

    await user.keyboard('{Control>}k{/Control}')
    const input = await screen.findByPlaceholderText('Search pages...')

    await user.type(input, 'a')
    expect(await screen.findByText('Alpha')).toBeInTheDocument()

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

    await user.click(await screen.findByText('Beta'))
    expect(onSelect).toHaveBeenCalledWith('p2')
  })

  it('opens from the outside too, which is what the sidebar button does', async () => {
    mockSearch([hit('p1', 'Alpha')])
    const user = userEvent.setup()
    renderWithProviders(<Harness onSelect={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'search' }))
    expect(await screen.findByPlaceholderText('Search pages...')).toBeInTheDocument()
  })
})
