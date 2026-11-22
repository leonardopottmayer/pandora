import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { NotesGraphPage } from './NotesGraphPage'
import { graphPreferencesKey } from '../hooks/useGraphPreferences'
import type { PageGraphDto } from '../models'

const fixture: PageGraphDto = {
  nodes: [
    { id: 'p1', title: 'Reunião de trabalho', slug: 'meeting', icon: null, isArchived: false, degree: 0, tagIds: ['work'] },
    { id: 'p2', title: 'Receitas', slug: 'recipes', icon: null, isArchived: false, degree: 0, tagIds: [] },
  ],
  edges: [{ sourceId: 'p1', targetId: 'p2', kind: 'wikilink' }],
}
const graphQuery = vi.hoisted(() => vi.fn())
const navigate = vi.hoisted(() => vi.fn())

vi.mock('react-router-dom', async (importOriginal) => ({
  ...await importOriginal<typeof import('react-router-dom')>(), useNavigate: () => navigate,
}))

vi.mock('@/modules/identity/context/auth-context', () => ({ useAuth: () => ({ user: { id: 'graph-user' } }) }))
vi.mock('../hooks/useSpaces', () => ({ useSpaces: () => ({ data: [] }) }))
vi.mock('../hooks/useTags', () => ({ useTags: () => ({ tags: [] }) }))
vi.mock('../hooks/usePages', () => ({ useGraph: () => graphQuery() }))
vi.mock('../components/NotesSidebar', () => ({ NotesSidebar: () => null }))
vi.mock('../components/SearchPalette', () => ({ SearchPalette: () => null }))
vi.mock('../components/TagFilter', () => ({ TagFilter: () => null }))
vi.mock('../components/GraphView', () => ({ GraphView: ({ graph, families, focusedId, onSelect, collapsedFamilyIds, onToggleFamily }: { graph: PageGraphDto; families?: unknown[]; focusedId?: string; onSelect: (id: string) => void; collapsedFamilyIds: string[]; onToggleFamily: (id: string) => void }) =>
  <div>
    <div data-testid="visible-nodes">{graph.nodes.map((node) => node.id).join(',')}</div>
    <div data-testid="graph-mode">{families ? 'grouped' : 'free'}</div>
    <div data-testid="focused-node">{focusedId}</div>
    <div data-testid="collapsed-families">{collapsedFamilyIds.join(',')}</div>
    <button onClick={() => onSelect('p1')}>Inspect p1</button>
    {families && <button onClick={() => onToggleFamily('work')}>Toggle Work</button>}
  </div> }))

beforeEach(() => {
  localStorage.clear()
  navigate.mockClear()
  graphQuery.mockReturnValue({ data: fixture, isLoading: false })
})

describe('NotesGraphPage', () => {
  it('filters graph nodes and title lookup together and clears details when visibility changes', async () => {
    const user = userEvent.setup()
    graphQuery.mockReturnValue({ data: { ...fixture, nodes: [
      ...fixture.nodes,
      { ...fixture.nodes[0], id: 'old', title: 'Old note', isArchived: true },
      { ...fixture.nodes[0], id: 'solo', title: 'Isolated note' },
    ] }, isLoading: false })
    renderWithProviders(<NotesGraphPage />)
    expect(screen.getByTestId('visible-nodes')).toHaveTextContent('p1,p2,solo')
    expect(screen.getByRole('switch', { name: 'Show archived' })).not.toBeChecked()
    await user.click(screen.getByRole('switch', { name: 'Show archived' }))
    expect(screen.getByTestId('visible-nodes')).toHaveTextContent('p1,p2,old,solo')
    await user.type(screen.getByRole('combobox', { name: 'Locate a note in the graph' }), 'Old note')
    await user.click(await screen.findByText('Old note'))
    expect(screen.getByTestId('focused-node')).toHaveTextContent('old')
    await user.click(screen.getByRole('switch', { name: 'Show archived' }))
    expect(screen.getByTestId('focused-node')).toBeEmptyDOMElement()
    expect(screen.queryByText('Note details')).not.toBeInTheDocument()
    await user.click(screen.getByRole('switch', { name: 'Hide isolated notes' }))
    expect(screen.getByTestId('visible-nodes')).toHaveTextContent(/^p1,p2$/)
    await user.type(screen.getByRole('combobox', { name: 'Locate a note in the graph' }), 'Old note')
    expect(await screen.findByText('No matching notes in this graph.')).toBeInTheDocument()
  })

  it('shows a visibility-specific empty result and lets the user restore isolated pages', async () => {
    const user = userEvent.setup()
    graphQuery.mockReturnValue({ data: { nodes: [fixture.nodes[0]], edges: [] }, isLoading: false })
    renderWithProviders(<NotesGraphPage />)
    await user.click(screen.getByRole('switch', { name: 'Hide isolated notes' }))
    expect(screen.getByText('No notes match the graph visibility filters.')).toBeInTheDocument()
    expect(screen.queryByTestId('visible-nodes')).not.toBeInTheDocument()
    await user.click(screen.getByRole('switch', { name: 'Hide isolated notes' }))
    expect(screen.getByTestId('visible-nodes')).toHaveTextContent('p1')
  })

  it('closes selected member details on collapse and expands the family when searching for that page', async () => {
    localStorage.setItem(graphPreferencesKey('graph-user'), JSON.stringify({
      showGroups: true, families: [{ id: 'work', name: 'Work', color: '#1677ff', tagIds: ['work'] }],
    }))
    const user = userEvent.setup()
    renderWithProviders(<NotesGraphPage />)
    await user.click(screen.getByRole('button', { name: 'Inspect p1' }))
    await user.click(screen.getByRole('button', { name: 'Toggle Work' }))
    expect(screen.getByTestId('collapsed-families')).toHaveTextContent('work')
    expect(screen.getByTestId('focused-node')).toBeEmptyDOMElement()
    expect(screen.queryByText('Note details')).not.toBeInTheDocument()
    await user.type(screen.getByRole('combobox', { name: 'Locate a note in the graph' }), 'REUNIAO')
    await user.click(await screen.findByText('Reunião de trabalho'))
    expect(screen.getByTestId('collapsed-families')).toBeEmptyDOMElement()
    expect(screen.getByTestId('focused-node')).toHaveTextContent('p1')
    expect(screen.getByText('Note details')).toBeInTheDocument()
  })

  it('inspects nodes and their connections before explicitly navigating to a page', async () => {
    const user = userEvent.setup()
    renderWithProviders(<NotesGraphPage />)
    await user.click(screen.getByRole('button', { name: 'Inspect p1' }))
    expect(await screen.findByText('Note details')).toBeInTheDocument()
    expect(screen.getByTestId('focused-node')).toHaveTextContent('p1')
    expect(navigate).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Receitas' }))
    expect(screen.getByTestId('focused-node')).toHaveTextContent('p2')
    expect(navigate).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Open note' }))
    expect(navigate).toHaveBeenCalledWith('/notes/p2')
  })

  it('locates a title ignoring accents and case, and clears the persistent selection', async () => {
    const user = userEvent.setup()
    renderWithProviders(<NotesGraphPage />)
    const search = screen.getByRole('combobox', { name: 'Locate a note in the graph' })
    await user.type(search, 'REUNIAO')
    await user.click(await screen.findByText('Reunião de trabalho'))
    expect(screen.getByTestId('focused-node')).toHaveTextContent('p1')
    await user.click(screen.getByRole('img', { name: 'close-circle' }))
    expect(screen.getByTestId('focused-node')).toBeEmptyDOMElement()
    expect(screen.queryByText('Note details')).not.toBeInTheDocument()
  })

  it('keeps the highlight when details close and reopens them when the same search result is selected', async () => {
    const user = userEvent.setup()
    renderWithProviders(<NotesGraphPage />)
    await user.click(screen.getByRole('button', { name: 'Inspect p1' }))
    await user.click(screen.getByRole('button', { name: 'Close' }))
    expect(screen.queryByText('Note details')).not.toBeInTheDocument()
    expect(screen.getByTestId('focused-node')).toHaveTextContent('p1')
    await user.type(screen.getByRole('combobox', { name: 'Locate a note in the graph' }), 'REUNIAO')
    await user.click(await screen.findByText('Reunião de trabalho', { selector: '.ant-select-item-option-content' }))
    expect(await screen.findByText('Note details')).toBeInTheDocument()
  })

  it('reports when no visible title matches', async () => {
    const user = userEvent.setup()
    renderWithProviders(<NotesGraphPage />)
    await user.type(screen.getByRole('combobox', { name: 'Locate a note in the graph' }), 'missing')
    expect(await screen.findByText('No matching notes in this graph.')).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Locate a note in the graph' })).toHaveAttribute('aria-expanded', 'true')
  })

  it('drops the highlight when the selected node is absent from the returned graph', async () => {
    const user = userEvent.setup()
    const view = renderWithProviders(<NotesGraphPage />)
    await user.type(screen.getByRole('combobox', { name: 'Locate a note in the graph' }), 'Receitas')
    await user.click(await screen.findByText('Receitas'))
    expect(screen.getByTestId('focused-node')).toHaveTextContent('p2')
    graphQuery.mockReturnValue({ data: { nodes: [fixture.nodes[0]], edges: [] }, isLoading: false })
    view.rerender(<NotesGraphPage />)
    expect(screen.getByTestId('focused-node')).toBeEmptyDOMElement()
    expect(screen.queryByText('Note details')).not.toBeInTheDocument()
  })

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
