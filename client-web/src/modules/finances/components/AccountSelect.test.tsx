import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { AccountSelect } from './AccountSelect'
import { useAccounts } from '../hooks/useAccounts'

vi.mock('../hooks/useAccounts')

const accounts = [
  { id: 'a1', name: 'Conta', currency: 'BRL' },
  { id: 'a2', name: 'Poupança', currency: 'USD' },
]

beforeEach(() => {
  vi.mocked(useAccounts).mockReturnValue({ data: accounts, isLoading: false } as never)
})

describe('AccountSelect', () => {
  it('lists accounts with "name (currency)" labels', async () => {
    const user = userEvent.setup()
    renderWithProviders(<AccountSelect />)
    await user.click(screen.getByRole('combobox'))
    expect(await screen.findByText('Conta (BRL)')).toBeInTheDocument()
    expect(screen.getByText('Poupança (USD)')).toBeInTheDocument()
  })

  it('excludes accounts listed in excludeIds', async () => {
    const user = userEvent.setup()
    renderWithProviders(<AccountSelect excludeIds={['a1']} />)
    await user.click(screen.getByRole('combobox'))
    expect(await screen.findByText('Poupança (USD)')).toBeInTheDocument()
    expect(screen.queryByText('Conta (BRL)')).not.toBeInTheDocument()
  })

  it('emits the selected account id via onChange', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<AccountSelect onChange={onChange} />)
    await user.click(screen.getByRole('combobox'))
    await user.click(await screen.findByText('Conta (BRL)'))
    expect(onChange).toHaveBeenCalledWith('a1', expect.anything())
  })
})
