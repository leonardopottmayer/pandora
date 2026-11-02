import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { NotesGraphPage } from './NotesGraphPage'
import { graphPreferencesKey } from '../hooks/useGraphPreferences'

vi.mock('@/modules/identity/context/auth-context', () => ({ useAuth: () => ({ user: { id: 'graph-user' } }) }))
vi.mock('../hooks/useSpaces', () => ({ useSpaces: () => ({ data: [] }) }))
vi.mock('../hooks/useTags', () => ({ useTags: () => ({ tags: [] }) }))
vi.mock('../hooks/usePages', () => ({ useGraph: () => ({ data: { nodes: [{ id: 'p1' }], edges: [] }, isLoading: false }) }))
vi.mock('../components/NotesSidebar', () => ({ NotesSidebar: () => null }))
vi.mock('../components/SearchPalette', () => ({ SearchPalette: () => null }))
vi.mock('../components/TagFilter', () => ({ TagFilter: () => null }))
vi.mock('../components/GraphView', () => ({ GraphView: ({ families }: { families?: unknown[] }) =>
  <div data-testid="graph-mode">{families ? 'grouped' : 'free'}</div> }))

beforeEach(() => localStorage.clear())

describe('NotesGraphPage', () => {
  it('starts in free mode and remembers both switching on and switching off across visits', async () => {
    const user = userEvent.setup()
    const first = renderWithProviders(<NotesGraphPage />)
    expect(screen.getByRole('switch', { name: 'Show groups' })).not.toBeChecked()
    expect(screen.getByTestId('graph-mode')).toHaveTextContent('free')
    await user.click(screen.getByRole('switch', { name: 'Show groups' }))
    expect(screen.getByTestId('graph-mode')).toHaveTextContent('grouped')
    expect(JSON.parse(localStorage.getItem(graphPreferencesKey('graph-user'))!).showGroups).toBe(true)
    first.unmount()
    const second = renderWithProviders(<NotesGraphPage />)
    expect(screen.getByRole('switch', { name: 'Show groups' })).toBeChecked()
    await user.click(screen.getByRole('switch', { name: 'Show groups' }))
    second.unmount()
    renderWithProviders(<NotesGraphPage />)
    expect(screen.getByRole('switch', { name: 'Show groups' })).not.toBeChecked()
  })
})
