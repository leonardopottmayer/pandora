import { describe, it, expect, beforeAll, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { renderWithProviders } from '@/test/utils'
import type { PendingTransactionDto } from '../../models'
import { TransferFromPendingModal } from './TransferFromPendingModal'

function leg(overrides: Partial<PendingTransactionDto>): PendingTransactionDto {
  return {
    id: 'p1',
    source: 'import',
    recurringTransactionId: null,
    accountId: 'a1',
    cardId: null,
    kind: 'expense',
    amount: 100,
    currency: 'BRL',
    occurredOn: '2026-06-13',
    description: 'Transferência saída',
    payee: null,
    notes: null,
    systemCategoryId: null,
    userCategoryId: null,
    suggestedStatementId: null,
    originalPayload: '{}',
    status: 'pending',
    decidedAt: null,
    decidedBy: null,
    rejectionReason: null,
    transactionId: null,
    importRowId: null,
    dedupStatus: null,
    duplicateOfTransactionId: null,
    attachmentCount: 0,
    createdAt: '2026-06-13T00:00:00Z',
    updatedAt: null,
    ...overrides,
  }
}

const outflow = leg({ id: 'out', accountId: 'a1', kind: 'expense', description: 'Saída' })
const inflow = leg({ id: 'in', accountId: 'a2', kind: 'income', description: 'Entrada' })
const accountNames = new Map([
  ['a1', 'Conta A'],
  ['a2', 'Conta B'],
])

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('TransferFromPendingModal', () => {
  it('pre-fills the description from the outflow leg and confirms', async () => {
    const onConfirm = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(
      <TransferFromPendingModal
        open
        outflow={outflow}
        inflow={inflow}
        accountNames={accountNames}
        onClose={vi.fn()}
        onConfirm={onConfirm}
      />,
    )

    expect(screen.getByRole('textbox', { name: 'Description' })).toHaveValue('Saída')
    expect(screen.getByRole('textbox', { name: 'Date' })).toHaveValue('13/06/2026')
    // Both account names resolve in the summary.
    expect(screen.getByText('Conta A')).toBeInTheDocument()
    expect(screen.getByText('Conta B')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Create transfer' }))
    expect(onConfirm).toHaveBeenCalledWith('Saída', '2026-06-13')
  })

  it('shows a warning when the pair is incomplete', () => {
    renderWithProviders(
      <TransferFromPendingModal
        open
        outflow={outflow}
        inflow={null}
        accountNames={accountNames}
        onClose={vi.fn()}
        onConfirm={vi.fn()}
      />,
    )
    expect(screen.getByRole('alert')).toBeInTheDocument()
  })

  it('submits an edited localized date as ISO and prevents a blank description', async () => {
    const onConfirm = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<TransferFromPendingModal open outflow={outflow} inflow={inflow} accountNames={accountNames} onClose={vi.fn()} onConfirm={onConfirm} />)
    const description = screen.getByRole('textbox', { name: 'Description' })
    await user.clear(description)
    await user.type(description, '   ')
    expect(screen.getByRole('button', { name: 'Create transfer' })).toBeDisabled()
    await user.clear(description)
    await user.type(description, 'Reconciled transfer')
    const date = screen.getByRole('textbox', { name: 'Date' })
    await user.clear(date)
    await user.type(date, '14/06/2026')
    await user.tab()
    await user.click(screen.getByRole('button', { name: 'Create transfer' }))
    expect(onConfirm).toHaveBeenCalledWith('Reconciled transfer', '2026-06-14')
  })
})
