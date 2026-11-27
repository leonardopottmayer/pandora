import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { AssetClassDto, AssetDto } from '../../models'
import { AssetClassesPage } from './AssetClassesPage'

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

const cdb: AssetClassDto = { id: 'c1', code: 'cdb', name: 'CDB', behavior: 'application', isSystem: true, archivedAt: null }
const asset: AssetDto = {
  id: 's1', assetClassId: 'c1', name: 'Itaú Hunter CDB 110%', code: null, isin: null, issuer: null, currency: 'BRL', archivedAt: null,
}

describe('AssetClassesPage', () => {
  it('lists an asset that has no position yet and archives it from the row menu', async () => {
    let archived = false
    server.use(
      http.get(`${FINANCES_BASE}/asset-classes`, () => HttpResponse.json({ success: true, data: [cdb] })),
      http.get(`${FINANCES_BASE}/assets`, () => HttpResponse.json({ success: true, data: [asset] })),
      http.get(`${FINANCES_BASE}/positions`, () => HttpResponse.json({ success: true, data: [] })),
      http.post(`${FINANCES_BASE}/assets/s1/archive`, () => {
        archived = true
        return HttpResponse.json({ success: true, data: { ...asset, archivedAt: '2026-11-25T12:00:00Z' } })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AssetClassesPage />)

    expect(await screen.findByText('Itaú Hunter CDB 110%')).toBeInTheDocument()
    expect(screen.getByText('CDB · BRL · No open position')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'More actions for Itaú Hunter CDB 110%' }))
    await user.click(await screen.findByText('Archive'))
    await waitFor(() => expect(archived).toBe(true))
  })
})
