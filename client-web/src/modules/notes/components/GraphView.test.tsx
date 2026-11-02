import { forwardRef, useImperativeHandle } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { GraphView } from './GraphView'
import type { PageGraphDto } from '../models'
import type { ForceLink } from '../lib/graphData'
import type { PositionedNode } from '../lib/graphGroups'

interface CanvasProps {
  graphData: { nodes: PositionedNode[] }
  nodeColor: (node: PositionedNode) => string
  linkColor: (link: ForceLink) => string
  onNodeHover: (node: PositionedNode | null) => void
  onNodeClick: (node: PositionedNode) => void
  onEngineStop: () => void
}

const canvas = vi.hoisted(() => ({ centerAt: vi.fn(), zoom: vi.fn(), render: vi.fn(), zoomToFit: vi.fn(), strength: vi.fn(() => 0.1) }))
vi.mock('react-force-graph-2d', () => ({
  default: forwardRef(function Canvas(props: CanvasProps, ref) {
    props.graphData.nodes.forEach((node, index) => { node.x = index * 100; node.y = index * 50 })
    canvas.render(props)
    useImperativeHandle(ref, () => ({
      centerAt: canvas.centerAt, zoom: canvas.zoom, zoomToFit: canvas.zoomToFit,
      d3Force: () => ({ strength: canvas.strength }), d3ReheatSimulation: () => {},
    }))
    return null
  }),
}))

const graph: PageGraphDto = {
  nodes: ['a', 'b', 'c'].map((id) => ({ id, title: id, slug: id, icon: null, isArchived: false, degree: 1, tagIds: [] })),
  edges: [{ sourceId: 'a', targetId: 'b', kind: 'wikilink' }],
}

function latest(): CanvasProps { return canvas.render.mock.calls.at(-1)![0] }

beforeEach(() => {
  vi.clearAllMocks()
  vi.stubGlobal('ResizeObserver', class {
    private callback: ResizeObserverCallback
    constructor(callback: ResizeObserverCallback) { this.callback = callback }
    observe() {
      this.callback([{ contentRect: { width: 800, height: 500 } } as ResizeObserverEntry], this as unknown as ResizeObserver)
    }
    disconnect() {}
  })
})
afterEach(() => vi.unstubAllGlobals())

describe('GraphView search focus', () => {
  it('shows collapsed family counts and expands synthetic nodes without inspecting them as pages', async () => {
    const user = userEvent.setup()
    const onSelect = vi.fn()
    const onToggleFamily = vi.fn()
    const groupedGraph = { ...graph, nodes: graph.nodes.map((node) => ({ ...node, tagIds: node.id === 'c' ? [] : ['work'] })) }
    const families = [{ id: 'work', name: 'Work', tagIds: ['work'], color: '#1677ff' }]
    const view = renderWithProviders(<GraphView graph={groupedGraph} families={families} collapsedFamilyIds={['work']} onToggleFamily={onToggleFamily} onSelect={onSelect} />)
    expect(screen.getByRole('button', { name: 'Work (2)' })).toBeInTheDocument()
    expect(latest().graphData.nodes.map((node) => node.id)).toEqual(['c', 'family:work'])
    act(() => latest().onNodeClick(latest().graphData.nodes[1]))
    expect(onToggleFamily).toHaveBeenCalledWith('work')
    expect(onSelect).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Expand Work' }))
    expect(onToggleFamily).toHaveBeenCalledTimes(2)
    act(() => latest().onEngineStop())
    expect(canvas.zoomToFit).toHaveBeenCalled()
    view.rerender(<GraphView graph={groupedGraph} collapsedFamilyIds={['work']} onSelect={onSelect} />)
    expect(latest().graphData.nodes).toHaveLength(3)
    expect(screen.queryByRole('button', { name: 'Expand Work' })).not.toBeInTheDocument()
  })

  it('centers without navigating, keeps neighbors highlighted and restores search focus after hover', () => {
    const onSelect = vi.fn()
    const view = renderWithProviders(<GraphView graph={graph} focusedId="a" onSelect={onSelect} />)
    expect(canvas.centerAt).toHaveBeenLastCalledWith(0, 0, 300)
    expect(canvas.zoom).toHaveBeenLastCalledWith(2, 300)
    expect(onSelect).not.toHaveBeenCalled()
    const [a, b, c] = latest().graphData.nodes
    expect(latest().nodeColor(b)).not.toBe(latest().nodeColor(c))
    const selectedColor = latest().nodeColor(a)
    expect(latest().linkColor({ source: 'a', target: 'b', kind: 'wikilink' })).toBe(selectedColor)
    act(() => latest().onNodeHover(c))
    expect(latest().nodeColor(b)).toBe(latest().nodeColor(a))
    act(() => latest().onNodeHover(null))
    expect(latest().nodeColor(b)).not.toBe(latest().nodeColor(c))
    act(() => latest().onEngineStop())
    expect(canvas.centerAt).toHaveBeenLastCalledWith(0, 0, 300)
    act(() => latest().onNodeClick(b))
    expect(onSelect).toHaveBeenCalledWith('b')
    view.rerender(<GraphView graph={graph} onSelect={onSelect} />)
    expect(latest().nodeColor(b)).toBe(latest().nodeColor(c))
  })

  it('does not move the camera for a result absent from the filtered graph', () => {
    renderWithProviders(<GraphView graph={graph} focusedId="absent" onSelect={vi.fn()} />)
    expect(canvas.centerAt).not.toHaveBeenCalled()
    expect(graph.nodes[0]).not.toHaveProperty('x')
  })
})
