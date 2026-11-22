import { describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { GraphDetailsPanel } from './GraphDetailsPanel'
import type { PageGraphDto, TagDto } from '../models'
import type { GraphFamily } from '../lib/graphGroups'

const graph: PageGraphDto = {
  nodes: [
    { id: 'a', title: 'Work plan', slug: 'a', icon: '📋', isArchived: true, degree: 4, tagIds: ['work'] },
    { id: 'b', title: 'Project', slug: 'b', icon: null, isArchived: false, degree: 3, tagIds: [] },
    { id: 'c', title: 'Recipe', slug: 'c', icon: null, isArchived: false, degree: 1, tagIds: [] },
  ],
  edges: [
    { sourceId: 'a', targetId: 'b', kind: 'wikilink' },
    { sourceId: 'a', targetId: 'b', kind: 'embed' },
    { sourceId: 'b', targetId: 'a', kind: 'wikilink' },
    { sourceId: 'c', targetId: 'a', kind: 'wikilink' },
  ],
}
const tags: TagDto[] = [{ id: 'work', name: 'work', slug: 'work', color: null, pageCount: 1 }]
const families: GraphFamily[] = [
  { id: 'work', name: 'Work', tagIds: ['work'], color: '#1677ff' },
  { id: 'projects', name: 'Projects', tagIds: ['work'], color: '#52c41a' },
]

describe('GraphDetailsPanel', () => {
  it('shows tags, all matching families, region priority and distinct incoming/outgoing neighbors', async () => {
    const user = userEvent.setup()
    const onSelect = vi.fn()
    const onOpen = vi.fn()
    const onClose = vi.fn()
    renderWithProviders(<GraphDetailsPanel node={graph.nodes[0]} graph={graph} tags={tags} families={families} compact={false} onSelect={onSelect} onOpen={onOpen} onClose={onClose} />)
    expect(screen.getByText('📋 Work plan')).toBeInTheDocument()
    expect(screen.getByText('Archived')).toBeInTheDocument()
    expect(screen.getByText('#work')).toBeInTheDocument()
    expect(screen.getByText('Projects')).toBeInTheDocument()
    expect(screen.getByText('Region when groups are enabled: Work.')).toBeInTheDocument()
    expect(screen.getByText('Connected notes (2)')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Project' })).toHaveLength(1)
    await user.click(screen.getByRole('button', { name: 'Recipe' }))
    expect(onSelect).toHaveBeenCalledWith('c')
    expect(onOpen).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Open note' }))
    expect(onOpen).toHaveBeenCalledWith('a')
    await user.click(screen.getByRole('button', { name: 'Close note details' }))
    expect(onClose).toHaveBeenCalledOnce()
  })

  it('shows empty metadata and no connections outside the filtered graph', () => {
    const node = { ...graph.nodes[1], tagIds: [] }
    renderWithProviders(<GraphDetailsPanel node={node} graph={{ nodes: [node], edges: [] }} tags={tags} families={families} compact onSelect={vi.fn()} onOpen={vi.fn()} onClose={vi.fn()} />)
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByText('No tags.')).toBeInTheDocument()
    expect(screen.getByText('No family')).toBeInTheDocument()
    expect(screen.getByText('No connected notes in this graph.')).toBeInTheDocument()
  })
})
