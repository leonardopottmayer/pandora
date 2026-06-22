import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { CardSelect } from './CardSelect'
import { useCards } from '../hooks/useCards'

vi.mock('../hooks/useCards')

beforeEach(() => {
  vi.mocked(useCards).mockReturnValue({
    data: [
      { id: 'c1', name: 'Nubank', lastFour: '1234' },
      { id: 'c2', name: 'Vale', lastFour: null },
    ],
    isLoading: false,
  } as never)
})

describe('CardSelect', () => {
  it('appends ····lastFour when present and shows the bare name otherwise', async () => {
    const user = userEvent.setup()
    renderWithProviders(<CardSelect />)
    await user.click(screen.getByRole('combobox'))
    expect(await screen.findByText('Nubank ····1234')).toBeInTheDocument()
    expect(screen.getByText('Vale')).toBeInTheDocument()
  })

  it('emits the selected card id', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<CardSelect onChange={onChange} />)
    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByText('Nubank ····1234'))
    expect(onChange).toHaveBeenCalledWith('c1', expect.anything())
  })
})
