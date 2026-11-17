import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { EntityTags } from './EntityTags'
import { useEntityTags, useSetEntityTags, useTags } from '../hooks/useTags'

vi.mock('../hooks/useTags')

const mutate = vi.fn()

beforeEach(() => {
  mutate.mockReset()
  vi.mocked(useTags).mockReturnValue({
    data: [{ id: 't1', name: 'Viagem' }, { id: 't2', name: 'Trabalho' }],
    isLoading: false,
  } as never)
  vi.mocked(useEntityTags).mockReturnValue({ data: ['t1'], isLoading: false } as never)
  vi.mocked(useSetEntityTags).mockReturnValue({ mutate, isPending: false } as never)
})

describe('EntityTags', () => {
  it('shows the entity tags and saves the new set on change', async () => {
    const user = userEvent.setup()
    renderWithProviders(<EntityTags entityType="account" entityId="a1" />)
    expect(screen.getByText('Viagem')).toBeInTheDocument()

    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByText('Trabalho'))

    expect(useSetEntityTags).toHaveBeenCalledWith('account', 'a1')
    expect(mutate).toHaveBeenCalledWith(['t1', 't2'], expect.anything())
  })
})
