import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
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
  fileFormat: 'ofx',
  accountType: 'card',
  isSystemLayout: true,
  createdAt: '2026-06-13T00:00:00Z',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('ImportLayoutsPage', () => {
  it('renders the layouts returned by the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/import-layouts`, () =>
        HttpResponse.json({ success: true, data: [layout] }),
      ),
    )
    renderWithProviders(<ImportLayoutsPage />)
    expect(await screen.findByText('Nubank OFX')).toBeInTheDocument()
    // file format rendered uppercased in a tag
    expect(screen.getByText('OFX')).toBeInTheDocument()
    expect(screen.getByText('NUBANK_OFX')).toBeInTheDocument()
  })
})
