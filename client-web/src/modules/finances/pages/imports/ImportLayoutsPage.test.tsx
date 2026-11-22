import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { ImportLayoutDto } from '../../models'
import { ImportLayoutsPage } from './ImportLayoutsPage'

const layout: ImportLayoutDto = {
  id: 'l1',
  layoutCode: 'NUBANK_OFX',
  name: 'Nubank OFX',
  bankName: 'Nubank',
  bankCode: '260',
  fileFormat: 'ofx',
  accountType: 'card',
  isSystemLayout: true,
  createdAt: '2026-06-13T00:00:00Z',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('ImportLayoutsPage', () => {
  const layouts = [layout, { ...layout, id: 'l2', name: 'Banco Regional CSV', bankName: 'Regional', bankCode: '123', layoutCode: 'REGIONAL_CSV', fileFormat: 'csv', accountType: 'account' }]

  function mockLayouts(data = layouts) {
    server.use(http.get(`${FINANCES_BASE}/import-layouts`, () => HttpResponse.json({ success: true, data })))
  }

  it('searches bank codes and clears filters without changing the catalog', async () => {
    mockLayouts()
    const user = userEvent.setup()
    renderWithProviders(<ImportLayoutsPage />)
    await screen.findByText('Banco Regional CSV')
    await user.type(screen.getByPlaceholderText('Search names, banks or codes'), '260')
    expect(screen.queryByText('Banco Regional CSV')).not.toBeInTheDocument()
    expect(screen.getByText('1 layout')).toBeInTheDocument()
    await user.click(screen.getByText('Clear filters'))
    expect(screen.getByText('Banco Regional CSV')).toBeInTheDocument()
    expect(screen.getByText('2 layouts')).toBeInTheDocument()
  })

  it('combines format and destination filters and retains phone metadata', async () => {
    mockLayouts()
    const user = userEvent.setup({ pointerEventsCheck: 0 })
    renderWithProviders(<ImportLayoutsPage />)
    await screen.findByText('Banco Regional CSV')
    expect(screen.getAllByRole('columnheader')).toHaveLength(1)
    expect(screen.getByText('CSV · Account')).toBeInTheDocument()
    await user.click(screen.getByLabelText('Format'))
    await user.click(screen.getByText('CSV', { selector: '.ant-select-item-option-content' }))
    expect(screen.queryByText('Nubank OFX')).not.toBeInTheDocument()
    await user.click(screen.getByLabelText('Destination'))
    await user.click(screen.getByText('Card', { selector: '.ant-select-item-option-content' }))
    expect(screen.getByText('No layouts match these filters.')).toBeInTheDocument()
    expect(screen.getByText('0 layouts')).toBeInTheDocument()
  })

  it('distinguishes an empty catalog from a failed request', async () => {
    mockLayouts([])
    renderWithProviders(<ImportLayoutsPage />)
    expect(await screen.findByText('No import layouts available.')).toBeInTheDocument()
  })

  it('shows a request failure', async () => {
    server.use(http.get(`${FINANCES_BASE}/import-layouts`, () => HttpResponse.json({ success: false, errors: [{ code: 'Layouts.Unavailable', message: 'Layouts unavailable' }] }, { status: 500 })))
    renderWithProviders(<ImportLayoutsPage />)
    expect(await screen.findByRole('alert')).toBeInTheDocument()
  })

  it('renders the layouts returned by the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/import-layouts`, () =>
        HttpResponse.json({ success: true, data: [layout] }),
      ),
    )
    renderWithProviders(<ImportLayoutsPage />)
    expect(await screen.findByText('Nubank OFX')).toBeInTheDocument()
    expect(screen.getByText('OFX · Card')).toBeInTheDocument()
    expect(screen.getByText('NUBANK_OFX')).toBeInTheDocument()
  })
})
