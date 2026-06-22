import { describe, it, expect, vi, beforeEach, beforeAll } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { renderWithProviders } from '@/test/utils'
import { CategorySelect } from './CategorySelect'
import { useSystemCategories, useUserCategories } from '../hooks/useCategories'

vi.mock('../hooks/useCategories')

beforeAll(async () => {
  await i18n.changeLanguage('pt-BR')
})

beforeEach(() => {
  vi.mocked(useSystemCategories).mockReturnValue({
    data: [{ id: 's1', name: 'Alimentação', children: [] }],
  } as never)
  vi.mocked(useUserCategories).mockReturnValue({
    data: [
      { id: 'u1', name: 'Hobby', nature: 'expense', children: [] },
      { id: 'u2', name: 'Salário extra', nature: 'income', children: [] },
    ],
  } as never)
})

describe('CategorySelect', () => {
  it('renders the configured value (system category) without crashing', () => {
    renderWithProviders(
      <CategorySelect value={{ systemCategoryId: 's1', userCategoryId: null }} />,
    )
    // The TreeSelect displays the selected node's title.
    expect(screen.getByText('Alimentação')).toBeInTheDocument()
  })

  it('keeps user categories matching the nature filter', async () => {
    const user = userEvent.setup()
    renderWithProviders(<CategorySelect nature="income" />)
    await user.click(screen.getByRole('combobox'))
    await user.type(screen.getByRole('combobox'), 'Salário')
    expect(await screen.findByText('Salário extra')).toBeInTheDocument()
  })

  it('filters out user categories of a different nature', async () => {
    const user = userEvent.setup()
    renderWithProviders(<CategorySelect nature="expense" />)
    await user.click(screen.getByRole('combobox'))
    await user.type(screen.getByRole('combobox'), 'Salário')
    await waitFor(() =>
      expect(screen.queryByText('Salário extra')).not.toBeInTheDocument(),
    )
  })

  it('emits a user-category selection through onChange', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<CategorySelect onChange={onChange} />)
    await user.click(screen.getByRole('combobox'))
    await user.type(screen.getByRole('combobox'), 'Hobby')
    await user.click(await screen.findByText('Hobby'))
    expect(onChange).toHaveBeenCalledWith({ systemCategoryId: null, userCategoryId: 'u1' })
  })
})
