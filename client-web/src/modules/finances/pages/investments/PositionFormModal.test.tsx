import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { AccountDto, AssetClassDto, AssetDto, PositionDto } from '../../models'
import { PositionFormModal } from './PositionFormModal'
import { MovementFormModal } from '../../components/MovementFormModal'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

const account = { id: 'a1', name: 'XP', currency: 'BRL', type: 'investment' } as AccountDto
const classes: AssetClassDto[] = [
  { id: 'c-stock', code: 'stock', name: 'Stock', behavior: 'unit', isSystem: true, archivedAt: null },
  { id: 'c-cdb', code: 'cdb', name: 'CDB', behavior: 'application', isSystem: true, archivedAt: null },
]
const assets: AssetDto[] = [
  { id: 's1', assetClassId: 'c-stock', name: 'Itaúsa', code: 'ITSA4', isin: null, issuer: null, currency: 'BRL', archivedAt: null },
  { id: 's2', assetClassId: 'c-cdb', name: 'CDB Inter', code: null, isin: null, issuer: null, currency: 'BRL', archivedAt: null },
]

function lookups() {
  return [
    http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [account] })),
    http.get(`${FINANCES_BASE}/asset-classes`, () => HttpResponse.json({ success: true, data: classes })),
    http.get(`${FINANCES_BASE}/assets`, () => HttpResponse.json({ success: true, data: assets })),
  ]
}

function capture(path: string, method: 'post' = 'post') {
  const box: { body?: Record<string, unknown> } = {}
  server.use(
    http[method](`${FINANCES_BASE}${path}`, async ({ request }) => {
      box.body = (await request.json()) as Record<string, unknown>
      return HttpResponse.json({ success: true, data: {} })
    }),
  )
  return box
}

async function pickAsset(user: ReturnType<typeof userEvent.setup>, label: string) {
  await user.click(screen.getByRole('combobox', { name: 'Asset' }))
  await user.click(await screen.findByText(label, { selector: '.ant-select-item-option-content' }))
}

describe('PositionFormModal', () => {
  it('opens a stock position with quantity and average cost, moving no cash', async () => {
    server.use(...lookups())
    const sent = capture('/positions')
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<PositionFormModal open defaultAccountId="a1" onClose={vi.fn()} />)

    await pickAsset(user, 'ITSA4 · Itaúsa')
    await user.type(screen.getByRole('spinbutton', { name: 'Quantity' }), '100')
    await user.type(screen.getByRole('spinbutton', { name: 'Average cost' }), '10.5')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(sent.body).toMatchObject({
      accountId: 'a1', assetId: 's1', kind: 'opening', quantity: 100, averageCost: 10.5,
      unitPrice: null, investedAmount: null, costs: 0, index: null,
    }))
  })

  it('buys a CDB with its amount, rate and the fields of fixed income', async () => {
    server.use(...lookups())
    const sent = capture('/positions')
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<PositionFormModal open defaultAccountId="a1" onClose={vi.fn()} />)

    await pickAsset(user, 'CDB Inter')
    expect(screen.queryByRole('spinbutton', { name: 'Quantity' })).not.toBeInTheDocument()
    await user.click(screen.getByText('Buy'))
    await user.type(screen.getByRole('spinbutton', { name: 'Amount invested' }), '5000')
    await user.click(screen.getByRole('combobox', { name: 'Index' }))
    await user.click(await screen.findByText('CDI', { selector: '.ant-select-item-option-content' }))
    await user.type(await screen.findByRole('spinbutton', { name: '% of the index' }), '110')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(sent.body).toMatchObject({
      assetId: 's2', kind: 'buy', investedAmount: 5000, index: 'cdi', indexPercent: 110, spreadRate: null,
      quantity: null, currentValue: null,
    }))
  })
})

describe('MovementFormModal', () => {
  const stock = { id: 'p1', behavior: 'unit', currency: 'BRL' } as PositionDto

  it('sends a sale with only the fields a sale takes', async () => {
    const sent = capture('/positions/p1/movements')
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<MovementFormModal position={stock} onClose={vi.fn()} />)

    await user.click(screen.getByRole('combobox', { name: 'Kind' }))
    await user.click(await screen.findByText('Sell', { selector: '.ant-select-item-option-content' }))
    await user.type(screen.getByRole('spinbutton', { name: 'Quantity' }), '40')
    await user.type(screen.getByRole('spinbutton', { name: 'Unit price' }), '15')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(sent.body).toMatchObject({
      kind: 'sell', quantity: 40, unitPrice: 15, grossAmount: null, incomeKind: null, splitFactor: null, costs: 0, taxWithheld: 0,
    }))
  })

  it('offers fixed-income kinds for an application', async () => {
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<MovementFormModal position={{ ...stock, behavior: 'application' }} onClose={vi.fn()} />)

    await user.click(screen.getByRole('combobox', { name: 'Kind' }))
    expect(await screen.findByText('Maturity', { selector: '.ant-select-item-option-content' })).toBeInTheDocument()
    expect(screen.queryByText('Sell', { selector: '.ant-select-item-option-content' })).not.toBeInTheDocument()
  })
})
