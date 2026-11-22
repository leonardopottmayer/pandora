import { describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import type { PageSummaryDto } from '../models'
import { PagePickerModal } from './PagePickerModal'

vi.mock('../services/spaces.service', () => ({
  listSpaces: vi.fn(async () => [{ id: 's1', name: 'Trabalho', icon: '💼', color: null }]),
}))

const page = (id: string, title: string, parentId: string | null = null, icon: string | null = null) =>
  ({ id, spaceId: 's1', parentId, title, slug: title.toLowerCase(), icon, orderIndex: 0, isFavorite: false, isArchived: false }) as PageSummaryDto

const pages = [page('p1', 'Projetos', null, '🚀'), page('p2', 'Pandora', 'p1')]

describe('PagePickerModal', () => {
  it('shows every page under its space, icons included, and picks one on click', async () => {
    const onPick = vi.fn()
    renderWithProviders(<PagePickerModal open pages={pages} onPick={onPick} onClose={vi.fn()} />)

    expect(await screen.findByText('Trabalho')).toBeInTheDocument()
    expect(screen.getByText('🚀')).toBeInTheDocument()
    await userEvent.click(screen.getByText('Pandora'))
    expect(onPick).toHaveBeenCalledWith(pages[1])
  })

  it('lists search matches with the path to each', async () => {
    renderWithProviders(<PagePickerModal open pages={pages} onPick={vi.fn()} onClose={vi.fn()} />)

    await screen.findByText('Trabalho')
    await userEvent.type(screen.getByRole('textbox'), 'pand')
    expect(screen.getByText('Trabalho › Projetos')).toBeInTheDocument()
  })
})
