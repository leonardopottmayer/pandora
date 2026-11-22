import { describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '@/test/utils'
import { TableSizeModal } from './TableSizeModal'

describe('TableSizeModal', () => {
  it('confirms the size that was typed', async () => {
    const onConfirm = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<TableSizeModal onCancel={vi.fn()} onConfirm={onConfirm} />)

    const rows = screen.getByLabelText('Rows')
    await user.clear(rows)
    await user.type(rows, '5')
    await user.click(screen.getByRole('button', { name: 'Insert' }))

    expect(onConfirm).toHaveBeenCalledWith(5, 3)
  })

  it('confirms on Enter without reaching for the button', async () => {
    const onConfirm = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<TableSizeModal onCancel={vi.fn()} onConfirm={onConfirm} />)

    await user.type(screen.getByLabelText('Columns'), '{Enter}')

    expect(onConfirm).toHaveBeenCalledWith(3, 3)
  })

  it('cancels without inserting anything', async () => {
    const onCancel = vi.fn()
    const onConfirm = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<TableSizeModal onCancel={onCancel} onConfirm={onConfirm} />)

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalled()
    expect(onConfirm).not.toHaveBeenCalled()
  })
})
