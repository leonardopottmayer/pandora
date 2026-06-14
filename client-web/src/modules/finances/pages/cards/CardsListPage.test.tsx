import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CardDto } from '../../models'
import { CardsListPage } from './CardsListPage'

const card: CardDto = {
  id: 'k1',
  name: 'Nubank',
  brand: 'Mastercard',
  lastFour: '1234',
  creditLimit: 5000,
  closingDay: 5,
  dueDay: 12,
  currency: 'BRL',
  defaultPaymentAccountId: null,
  archivedAt: null,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('CardsListPage', () => {
  it('renders cards from the API', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/cards`, () => HttpResponse.json({ success: true, data: [card] })),
    )
    renderWithProviders(<CardsListPage />)
    expect(await screen.findByText('Nubank')).toBeInTheDocument()
    expect(screen.getByText('····1234')).toBeInTheDocument()
  })
})
