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

describe('SearchPalette', () => {
  it('opens on Ctrl+K, searches, and opens the chosen page on Enter', async () => {
    mockSearch([hit('p1', 'Alpha'), hit('p2', 'Beta')])
    const onSelect = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<SearchPalette onSelect={onSelect} />)

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
    renderWithProviders(<SearchPalette onSelect={vi.fn()} />)

    await user.keyboard('{Control>}k{/Control}')
    await user.type(await screen.findByPlaceholderText('Search pages...'), 'zzz')

    expect(await screen.findByText('No page matches this search.')).toBeInTheDocument()
  })

  it('opens the page that was clicked', async () => {
    mockSearch([hit('p1', 'Alpha'), hit('p2', 'Beta')])
    const onSelect = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<SearchPalette onSelect={onSelect} />)

    await user.keyboard('{Control>}k{/Control}')
    await user.type(await screen.findByPlaceholderText('Search pages...'), 'a')

    await user.click(await screen.findByText('Beta'))
    expect(onSelect).toHaveBeenCalledWith('p2')
  })
})
