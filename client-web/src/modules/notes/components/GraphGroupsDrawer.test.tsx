import { describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { GraphGroupsDrawer } from './GraphGroupsDrawer'
import type { GraphFamily } from '../lib/graphGroups'

const families: GraphFamily[] = [
  { id: 'work', name: 'Work', color: '#1677ff', tagIds: ['t1'] },
  { id: 'travel', name: 'Travel', color: '#52c41a', tagIds: ['t2'] },
]
const tags = [
  { id: 't1', name: 'work', slug: 'work', color: null, pageCount: 2 },
  { id: 't2', name: 'travel', slug: 'travel', color: null, pageCount: 1 },
]

describe('GraphGroupsDrawer', () => {
  it('saves renamed families in their chosen priority without mutating the original', async () => {
    const onSave = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<GraphGroupsDrawer families={families} tags={tags} onSave={onSave} onClose={vi.fn()} />)
    const names = screen.getAllByRole('textbox', { name: 'Family name' })
    await user.clear(names[1])
    await user.type(names[1], 'Holidays')
    await user.click(screen.getAllByRole('button', { name: 'Higher priority' })[1])
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(onSave).toHaveBeenCalledWith([{ ...families[1], name: 'Holidays' }, families[0]])
    expect(families[1].name).toBe('Travel')
  })

  it('does not save incomplete families or changes cancelled by the user', async () => {
    const onSave = vi.fn()
    const onClose = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<GraphGroupsDrawer families={[]} tags={tags} onSave={onSave} onClose={onClose} />)
    await user.click(screen.getByRole('button', { name: /Add family/ }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Give each family a name and at least one tag.')).toBeInTheDocument()
    expect(onSave).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(onClose).toHaveBeenCalled()
  })
})
