import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { TagPicker } from './TagPicker'
import { useTags } from '../hooks/useTags'

vi.mock('../hooks/useTags')

beforeEach(() => {
  vi.mocked(useTags).mockReturnValue({
    data: [
      { id: 't1', name: 'Viagem' },
      { id: 't2', name: 'Trabalho' },
    ],
    isLoading: false,
  } as never)
})

describe('TagPicker', () => {
  it('renders the available tags as options', async () => {
    const user = userEvent.setup()
    renderWithProviders(<TagPicker />)
    await user.click(screen.getByRole('combobox'))
    expect(await screen.findByText('Viagem')).toBeInTheDocument()
    expect(screen.getByText('Trabalho')).toBeInTheDocument()
  })

  it('emits the list of selected tag ids (multiple mode)', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<TagPicker onChange={onChange} />)
    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByText('Viagem'))
    expect(onChange).toHaveBeenCalledWith(['t1'], expect.anything())
  })
})
