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

  it('offers to delete an asset only while it never had a position', async () => {
    const used: AssetDto = { ...asset, id: 's2', name: 'Hunter Total Return' }
    let deleted = ''
    server.use(
      http.get(`${FINANCES_BASE}/asset-classes`, () => HttpResponse.json({ success: true, data: [cdb] })),
      http.get(`${FINANCES_BASE}/assets`, () => HttpResponse.json({ success: true, data: [asset, used] })),
      // A closed position still counts as history.
      http.get(`${FINANCES_BASE}/positions`, () => HttpResponse.json({ success: true, data: [{ id: 'p1', assetId: 's2', status: 'closed' }] })),
      http.delete(`${FINANCES_BASE}/assets/:id`, ({ params }) => {
        deleted = params.id as string
        return HttpResponse.json({ success: true, data: true })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AssetClassesPage />)

    await user.click(await screen.findByRole('button', { name: 'More actions for Hunter Total Return' }))
    expect(await screen.findByText('Archive')).toBeInTheDocument()
    expect(screen.queryByText('Delete')).not.toBeInTheDocument()
    await user.keyboard('{Escape}')

    await user.click(screen.getByRole('button', { name: 'More actions for Itaú Hunter CDB 110%' }))
    await user.click(await screen.findByText('Delete'))
    await user.click(await screen.findByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(deleted).toBe('s1'))
  })

  it('offers to delete an own class only while no asset uses it', async () => {
    const used: AssetClassDto = { id: 'u1', code: null, name: 'Quotas', behavior: 'application', isSystem: false, archivedAt: null }
    const unused: AssetClassDto = { ...used, id: 'u2', name: 'Plans' }
    let deleted = ''
    server.use(
      http.get(`${FINANCES_BASE}/asset-classes`, () => HttpResponse.json({ success: true, data: [cdb, used, unused] })),
      http.get(`${FINANCES_BASE}/assets`, () => HttpResponse.json({ success: true, data: [{ ...asset, assetClassId: 'u1' }] })),
      http.get(`${FINANCES_BASE}/positions`, () => HttpResponse.json({ success: true, data: [] })),
      http.delete(`${FINANCES_BASE}/asset-classes/:id`, ({ params }) => {
        deleted = params.id as string
        return HttpResponse.json({ success: true, data: true })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<AssetClassesPage />)

    await user.click(await screen.findByRole('button', { name: 'More actions for Quotas' }))
    expect(await screen.findByText('Archive')).toBeInTheDocument()
    expect(screen.queryByText('Delete')).not.toBeInTheDocument()
    await user.keyboard('{Escape}')

    await user.click(screen.getByRole('button', { name: 'More actions for Plans' }))
    await user.click(await screen.findByText('Delete'))
    await user.click(await screen.findByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(deleted).toBe('u2'))
  })
})
