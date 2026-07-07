import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TransactionDto } from '../models'
import { LinkTransactionModal } from './LinkTransactionModal'

const tx: TransactionDto = {
  id: 'x1',
  accountId: 'a1',
  cardStatementId: null,
  cardId: null,
  paidStatementId: null,
  kind: 'expense',
  status: 'posted',
  amount: 50,
  currency: 'BRL',
  occurredOn: '2026-06-13',
  description: 'Mercado',
  payee: null,
  notes: null,
  systemCategoryId: null,
  userCategoryId: null,
  transferGroupId: null,
  fxRate: null,
  installmentPlanId: null,
  installmentNumber: null,
  origin: 'manual',
  postedAt: '2026-06-13T00:00:00Z',
  voidedAt: null,
  voidReason: null,
  descriptionKey: null,
  descriptionArgs: null,
  statementReferenceMonth: null,
  statementDueDate: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('LinkTransactionModal', () => {
  it('pre-fills the search from defaultSearch and lets the user pick a transaction', async () => {
    let picked: string | undefined
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, () =>
        HttpResponse.json({ success: true, data: [tx] }),
      ),
    )
    const user = userEvent.setup()
    renderWithProviders(
      <LinkTransactionModal
        open
        defaultSearch="Mercado"
        onClose={vi.fn()}
        onPick={(id) => {
          picked = id
        }}
      />,
    )

    // Search is seeded with the suggestion description.
    expect(screen.getByDisplayValue('Mercado')).toBeInTheDocument()

    // Pick the candidate row, then confirm.
    await user.click(await screen.findByText('Mercado'))
    await user.click(screen.getByRole('button', { name: 'Link' }))
    expect(picked).toBe('x1')
  })

  it('keeps the confirm button disabled until a row is selected', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/transactions`, () =>
        HttpResponse.json({ success: true, data: [tx] }),
      ),
    )
    renderWithProviders(<LinkTransactionModal open onClose={vi.fn()} onPick={vi.fn()} />)
    expect(screen.getByRole('button', { name: 'Link' })).toBeDisabled()
  })
})
