import { describe, it, expect, beforeAll, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import { todayIn } from '@/lib/today'
import type { PositionDto } from '../../models'
import { UpdateValuesModal } from './UpdateValuesModal'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

const position = (id: string, assetCode: string): PositionDto => ({
  id, accountId: 'a1', assetId: `s-${id}`, assetName: assetCode, assetCode, assetClassId: 'c1', behavior: 'unit',
  status: 'open', currency: 'BRL', quantity: 10, averageCost: 10, investedAmount: null, appliedOn: null, index: null,
  indexPercent: null, spreadRate: null, maturityOn: null, costBasis: 100, currentValue: 100, valuedOn: null, closedAt: null,
})

describe('UpdateValuesModal', () => {
  it('sends only the rows given a value, all on the chosen date', async () => {
    let body: unknown
    server.use(
      http.get(`${FINANCES_BASE}/accounts`, () => HttpResponse.json({ success: true, data: [] })),
      http.post(`${FINANCES_BASE}/positions/valuations`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: [] })
      }),
    )
    const onClose = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<UpdateValuesModal positions={[position('p1', 'ITSA4'), position('p2', 'HGLG11')]} onClose={onClose} />)

    // Nothing typed yet: nothing to save.
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()

    await user.type(screen.getByRole('spinbutton', { name: 'New value of HGLG11' }), '1650.5')
    await user.click(screen.getByRole('button', { name: 'Save 1 value' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(body).toEqual({
      valuedOn: todayIn('America/Sao_Paulo').format('YYYY-MM-DD'),
      items: [{ positionId: 'p2', value: 1650.5 }],
    })
  })
})
